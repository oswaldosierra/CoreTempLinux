using CoreTempLinux.Sensors;
using CoreTempLinux.Tests.Fakes;
using Xunit;

namespace CoreTempLinux.Tests;

public class SensorMonitorTests
{
    private static CpuInfo MakeCpu(int cores)
    {
        // CpuInfo.Read cae a Environment.ProcessorCount si no ve "processor";
        // construimos /proc/cpuinfo con el número de núcleos deseado.
        var fs = new FakeFileSystem();
        var lines = new List<string> { "model name : Test CPU" };
        for (var i = 0; i < cores; i++)
            lines.Add($"processor : {i}");
        fs.AddFile("/proc/cpuinfo", string.Join("\n", lines));
        return CpuInfo.Read(fs);
    }

    private static SensorReading Temp(string chip, string label, double value, double? crit = null) =>
        new(chip, SensorKind.Temperature, label, value, "°C", crit);

    private static SensorMonitor Monitor(
        SensorReading[] hwmon,
        double[]? freq = null,
        double[]? load = null,
        int cores = 4) =>
        new(new StubHwmonReader(hwmon),
            new StubFrequencyReader(freq ?? new double[cores]),
            new StubLoadReader(load ?? new double[cores]),
            MakeCpu(cores));

    [Fact]
    public void SinTemperaturaCpu_DevuelveNoDisponible()
    {
        var snap = Monitor(Array.Empty<SensorReading>()).Collect();

        Assert.Null(snap.CpuTempC);
        Assert.Equal("N/D", snap.CpuTempLabel);
        Assert.Empty(snap.CpuCoreTemps);
    }

    [Fact]
    public void PrefiereEtiquetaTctl_SobreElNucleoMasCaliente()
    {
        var snap = Monitor(new[]
        {
            Temp("k10temp", "Tctl", 60),
            Temp("k10temp", "Tdie", 95), // más caliente, pero Tctl gana por preferencia
        }).Collect();

        Assert.Equal(60, snap.CpuTempC);
        Assert.Equal("Tctl", snap.CpuTempLabel);
    }

    [Fact]
    public void SinEtiquetaDePaquete_TomaElNucleoMasCaliente()
    {
        var snap = Monitor(new[]
        {
            Temp("coretemp", "Core 0", 55),
            Temp("coretemp", "Core 1", 70),
            Temp("coretemp", "Core 2", 62),
        }).Collect();

        Assert.Equal(70, snap.CpuTempC);
        Assert.Equal("Core 1", snap.CpuTempLabel);
    }

    [Fact]
    public void Critico_CaeAlDeOtroNucleoSiElPrimarioNoLoTiene()
    {
        var snap = Monitor(new[]
        {
            Temp("k10temp", "Tctl", 60, crit: null),
            Temp("k10temp", "Core 0", 55, crit: 95),
        }).Collect();

        Assert.Equal(95, snap.CpuCritC);
    }

    [Fact]
    public void SensoresNoCpu_VanAExtraSensors()
    {
        var snap = Monitor(new[]
        {
            Temp("k10temp", "Tctl", 60),
            new SensorReading("amdgpu", SensorKind.Temperature, "edge", 50, "°C", null),
            new SensorReading("nct6775", SensorKind.Fan, "fan1", 1200, "RPM", null),
        }).Collect();

        Assert.Equal(2, snap.ExtraSensors.Count);
        Assert.DoesNotContain(snap.ExtraSensors, s => s.Chip == "k10temp");
    }

    [Fact]
    public void MinMax_SeAcumulanEntreLecturas()
    {
        var monitor = Monitor(new[] { Temp("k10temp", "Tctl", 60) });

        monitor.Collect(); // 60

        // Cambiamos la lectura mutando... el stub es fijo; usamos varios monitores no sirve.
        // En su lugar comprobamos que una sola lectura fija min=max=valor.
        Assert.Equal(60, monitor.MinTempC);
        Assert.Equal(60, monitor.MaxTempC);
    }

    [Fact]
    public void MinMax_EvolucionanConValoresCambiantes()
    {
        // Un lector de hwmon que devuelve temperaturas distintas en cada llamada.
        var temps = new Queue<double>(new double[] { 60, 75, 50 });
        var reader = new SequenceHwmonReader(temps);
        var monitor = new SensorMonitor(
            reader,
            new StubFrequencyReader(new double[4]),
            new StubLoadReader(new double[4]),
            MakeCpu(4));

        monitor.Collect(); // 60
        monitor.Collect(); // 75
        monitor.Collect(); // 50

        Assert.Equal(50, monitor.MinTempC);
        Assert.Equal(75, monitor.MaxTempC);
    }

    [Fact]
    public void CoreCount_EsElMaximoEntreFrecuenciaYCpuinfo()
    {
        // freq reporta 2, cpuinfo reporta 8 -> 8
        var monitor = new SensorMonitor(
            new StubHwmonReader(),
            new StubFrequencyReader(new double[2]),
            new StubLoadReader(new double[2]),
            MakeCpu(8));

        Assert.Equal(8, monitor.CoreCount);
    }

    [Fact]
    public void Snapshot_ReexponeFrecuenciaYCarga()
    {
        var snap = Monitor(
            new[] { Temp("k10temp", "Tctl", 60) },
            freq: new[] { 3200.0, 3300.0 },
            load: new[] { 25.0, 50.0 },
            cores: 2).Collect();

        Assert.Equal(new[] { 3200.0, 3300.0 }, snap.FreqMhz);
        Assert.Equal(new[] { 25.0, 50.0 }, snap.LoadPct);
    }

    [Fact]
    public void PackageFreq_EsLaMediaIgnorandoNaN()
    {
        var snap = Monitor(
            new[] { Temp("k10temp", "Tctl", 60) },
            freq: new[] { 3000.0, double.NaN, 4000.0 },
            cores: 3).Collect();

        Assert.Equal(3500.0, snap.PackageFreqMhz);
    }

    [Fact]
    public void PackageFreq_EsNaNSiNingunNucleoLegible()
    {
        var snap = Monitor(
            new[] { Temp("k10temp", "Tctl", 60) },
            freq: new[] { double.NaN, double.NaN },
            cores: 2).Collect();

        Assert.True(double.IsNaN(snap.PackageFreqMhz));
    }

    [Fact]
    public void CoreTempStats_UnaEntradaPorSensorConCriticoYValorActual()
    {
        var snap = Monitor(new[]
        {
            Temp("coretemp", "Core 0", 55, crit: 100),
            Temp("coretemp", "Core 1", 70, crit: 100),
        }).Collect();

        Assert.Equal(2, snap.CoreTempStats.Count);
        var c1 = snap.CoreTempStats[1];
        Assert.Equal("Core 1", c1.Label);
        Assert.Equal(70, c1.Current);
        Assert.Equal(100, c1.Critical);
    }

    [Fact]
    public void CoreTempStats_MinMaxSeAcumulanPorEtiqueta()
    {
        // Un sensor "Tctl" cuyo valor cambia entre lecturas: 60 -> 75 -> 50.
        var temps = new Queue<double>(new double[] { 60, 75, 50 });
        var monitor = new SensorMonitor(
            new SequenceHwmonReader(temps),
            new StubFrequencyReader(new double[4]),
            new StubLoadReader(new double[4]),
            MakeCpu(4));

        monitor.Collect();
        monitor.Collect();
        var snap = monitor.Collect(); // actual = 50

        var stat = Assert.Single(snap.CoreTempStats);
        Assert.Equal("Tctl", stat.Label);
        Assert.Equal(50, stat.Current);
        Assert.Equal(50, stat.Min);
        Assert.Equal(75, stat.Max);
    }

    private sealed class SequenceHwmonReader : IHwmonReader
    {
        private readonly Queue<double> _temps;
        public SequenceHwmonReader(Queue<double> temps) => _temps = temps;

        public IReadOnlyList<SensorReading> ReadAll() =>
            new[] { new SensorReading("k10temp", SensorKind.Temperature, "Tctl", _temps.Dequeue(), "°C", null) };
    }
}
