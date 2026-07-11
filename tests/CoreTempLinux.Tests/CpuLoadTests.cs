using CoreTempLinux.Sensors;
using CoreTempLinux.Tests.Fakes;
using Xunit;

namespace CoreTempLinux.Tests;

public class CpuLoadTests
{
    // Formato /proc/stat: cpuN user nice system idle iowait irq softirq ...
    private static string Stat(params string[] cpuLines) =>
        string.Join("\n", cpuLines.Append("intr 12345").Append("ctxt 6789"));

    [Fact]
    public void PrimeraLectura_DevuelveCeros()
    {
        var fs = new FakeFileSystem().AddFile("/proc/stat",
            Stat("cpu  100 0 100 800 0 0 0", "cpu0 100 0 100 800 0 0 0"));

        var pct = new CpuLoad(fs).ReadPercent();

        Assert.Equal(new[] { 0.0 }, pct);
    }

    [Fact]
    public void SegundaLectura_CalculaUsoPorDelta()
    {
        var fs = new FakeFileSystem().AddFile("/proc/stat",
            Stat("cpu0 100 0 100 800 0 0 0")); // total=1000, idle=800

        var load = new CpuLoad(fs);
        load.ReadPercent(); // primera muestra

        // Nueva muestra: +100 de uso, +0 idle -> 100% ocupado en el delta.
        fs.AddFile("/proc/stat", Stat("cpu0 200 0 100 800 0 0 0")); // total=1100, idle=800
        var pct = load.ReadPercent();

        // dt = 100, di = 0 -> (100-0)*100/100 = 100
        Assert.Equal(100.0, pct[0]);
    }

    [Fact]
    public void UsoParcial_SeCalculaCorrectamente()
    {
        var fs = new FakeFileSystem().AddFile("/proc/stat",
            Stat("cpu0 100 0 100 800 0 0 0"));
        var load = new CpuLoad(fs);
        load.ReadPercent();

        // +50 de uso, +50 de idle -> dt=100, di=50 -> 50%
        fs.AddFile("/proc/stat", Stat("cpu0 150 0 100 850 0 0 0"));
        var pct = load.ReadPercent();

        Assert.Equal(50.0, pct[0]);
    }

    [Fact]
    public void IowaitCuentaComoIdle()
    {
        var fs = new FakeFileSystem().AddFile("/proc/stat",
            Stat("cpu0 100 0 100 800 0 0 0")); // idle=800, iowait=0
        var load = new CpuLoad(fs);
        load.ReadPercent();

        // Todo el incremento va a iowait (columna 5) -> cuenta como idle -> 0% uso.
        fs.AddFile("/proc/stat", Stat("cpu0 100 0 100 800 100 0 0"));
        var pct = load.ReadPercent();

        Assert.Equal(0.0, pct[0]);
    }

    [Fact]
    public void IgnoraLaLineaAgregadaCpu()
    {
        var fs = new FakeFileSystem().AddFile("/proc/stat",
            Stat("cpu  999 0 999 999 0 0 0", // agregada: espacio en pos 3, se ignora
                 "cpu0 100 0 100 800 0 0 0",
                 "cpu1 100 0 100 800 0 0 0"));

        var pct = new CpuLoad(fs).ReadPercent();

        Assert.Equal(2, pct.Length); // solo cpu0 y cpu1
    }

    [Fact]
    public void ParaAlLlegarALineasNoCpu()
    {
        var fs = new FakeFileSystem().AddFile("/proc/stat",
            "cpu0 100 0 100 800 0 0 0\nintr 123\ncpu_should_be_ignored 1 2 3");

        var pct = new CpuLoad(fs).ReadPercent();

        Assert.Single(pct);
    }

    [Fact]
    public void SinArchivo_DevuelveVacio()
    {
        var pct = new CpuLoad(new FakeFileSystem()).ReadPercent();

        Assert.Empty(pct);
    }

    [Fact]
    public void CambioEnElNumeroDeNucleos_NoRompeElCalculo()
    {
        var fs = new FakeFileSystem().AddFile("/proc/stat",
            Stat("cpu0 100 0 100 800 0 0 0"));
        var load = new CpuLoad(fs);
        load.ReadPercent(); // 1 núcleo memorizado

        // Ahora aparecen 2 núcleos: los tamaños no casan -> devuelve ceros sin lanzar.
        fs.AddFile("/proc/stat",
            Stat("cpu0 200 0 100 800 0 0 0", "cpu1 200 0 100 800 0 0 0"));
        var pct = load.ReadPercent();

        Assert.Equal(new[] { 0.0, 0.0 }, pct);
    }
}
