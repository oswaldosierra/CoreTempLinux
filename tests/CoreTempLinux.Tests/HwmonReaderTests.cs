using CoreTempLinux.Sensors;
using CoreTempLinux.Tests.Fakes;
using Xunit;

namespace CoreTempLinux.Tests;

public class HwmonReaderTests
{
    private const string Root = "/sys/class/hwmon";

    [Fact]
    public void SinChips_DevuelveListaVacia()
    {
        var fs = new FakeFileSystem();

        var readings = new HwmonReader(fs).ReadAll();

        Assert.Empty(readings);
    }

    [Fact]
    public void Temperatura_SeConvierteDeMilligradosYLeeEtiqueta()
    {
        var fs = new FakeFileSystem()
            .AddDirectory($"{Root}/hwmon0")
            .AddFile($"{Root}/hwmon0/name", "k10temp")
            .AddFile($"{Root}/hwmon0/temp1_input", "45000")
            .AddFile($"{Root}/hwmon0/temp1_label", "Tctl");

        var r = Assert.Single(new HwmonReader(fs).ReadAll());

        Assert.Equal("k10temp", r.Chip);
        Assert.Equal(SensorKind.Temperature, r.Kind);
        Assert.Equal("Tctl", r.Label);
        Assert.Equal(45.0, r.Value);
        Assert.Equal("°C", r.Unit);
    }

    [Fact]
    public void SinEtiqueta_UsaElNombreDelArchivo()
    {
        var fs = new FakeFileSystem()
            .AddDirectory($"{Root}/hwmon0")
            .AddFile($"{Root}/hwmon0/name", "coretemp")
            .AddFile($"{Root}/hwmon0/temp2_input", "50000");

        var r = Assert.Single(new HwmonReader(fs).ReadAll());

        Assert.Equal("temp2", r.Label);
    }

    [Fact]
    public void SinArchivoName_UsaElNombreDelDirectorio()
    {
        var fs = new FakeFileSystem()
            .AddDirectory($"{Root}/hwmon3")
            .AddFile($"{Root}/hwmon3/temp1_input", "40000");

        var r = Assert.Single(new HwmonReader(fs).ReadAll());

        Assert.Equal("hwmon3", r.Chip);
    }

    [Fact]
    public void Temperatura_PrefiereCritSobreMax()
    {
        var fs = new FakeFileSystem()
            .AddDirectory($"{Root}/hwmon0")
            .AddFile($"{Root}/hwmon0/name", "k10temp")
            .AddFile($"{Root}/hwmon0/temp1_input", "45000")
            .AddFile($"{Root}/hwmon0/temp1_crit", "95000")
            .AddFile($"{Root}/hwmon0/temp1_max", "90000");

        var r = Assert.Single(new HwmonReader(fs).ReadAll());

        Assert.Equal(95.0, r.Critical);
    }

    [Fact]
    public void Temperatura_UsaMaxSiNoHayCrit()
    {
        var fs = new FakeFileSystem()
            .AddDirectory($"{Root}/hwmon0")
            .AddFile($"{Root}/hwmon0/name", "k10temp")
            .AddFile($"{Root}/hwmon0/temp1_input", "45000")
            .AddFile($"{Root}/hwmon0/temp1_max", "90000");

        var r = Assert.Single(new HwmonReader(fs).ReadAll());

        Assert.Equal(90.0, r.Critical);
    }

    [Fact]
    public void Potencia_SeConvierteDeMicrovatios()
    {
        var fs = new FakeFileSystem()
            .AddDirectory($"{Root}/hwmon0")
            .AddFile($"{Root}/hwmon0/name", "amdgpu")
            .AddFile($"{Root}/hwmon0/power1_input", "15000000");

        var r = Assert.Single(new HwmonReader(fs).ReadAll());

        Assert.Equal(SensorKind.Power, r.Kind);
        Assert.Equal(15.0, r.Value);
        Assert.Equal("W", r.Unit);
        Assert.Null(r.Critical); // La potencia no lee crítico.
    }

    [Fact]
    public void Ventilador_NoSeDivide()
    {
        var fs = new FakeFileSystem()
            .AddDirectory($"{Root}/hwmon0")
            .AddFile($"{Root}/hwmon0/name", "nct6775")
            .AddFile($"{Root}/hwmon0/fan1_input", "1200");

        var r = Assert.Single(new HwmonReader(fs).ReadAll());

        Assert.Equal(SensorKind.Fan, r.Kind);
        Assert.Equal(1200.0, r.Value);
        Assert.Equal("RPM", r.Unit);
    }

    [Fact]
    public void Voltaje_SeConvierteDeMilivoltios()
    {
        var fs = new FakeFileSystem()
            .AddDirectory($"{Root}/hwmon0")
            .AddFile($"{Root}/hwmon0/name", "nct6775")
            .AddFile($"{Root}/hwmon0/in0_input", "1200");

        var r = Assert.Single(new HwmonReader(fs).ReadAll());

        Assert.Equal(SensorKind.Voltage, r.Kind);
        Assert.Equal(1.2, r.Value);
        Assert.Equal("V", r.Unit);
    }

    [Fact]
    public void ValorNoNumerico_SeOmite()
    {
        var fs = new FakeFileSystem()
            .AddDirectory($"{Root}/hwmon0")
            .AddFile($"{Root}/hwmon0/name", "k10temp")
            .AddFile($"{Root}/hwmon0/temp1_input", "N/A");

        Assert.Empty(new HwmonReader(fs).ReadAll());
    }

    [Fact]
    public void VariosChips_SeLeenEnOrdenOrdinal()
    {
        var fs = new FakeFileSystem()
            .AddDirectory($"{Root}/hwmon1")
            .AddFile($"{Root}/hwmon1/name", "segundo")
            .AddFile($"{Root}/hwmon1/temp1_input", "40000")
            .AddDirectory($"{Root}/hwmon0")
            .AddFile($"{Root}/hwmon0/name", "primero")
            .AddFile($"{Root}/hwmon0/temp1_input", "30000");

        var readings = new HwmonReader(fs).ReadAll();

        Assert.Equal(new[] { "primero", "segundo" }, readings.Select(r => r.Chip));
    }

    [Fact]
    public void VariasTemperaturas_SeLeenTodasEnOrden()
    {
        var fs = new FakeFileSystem()
            .AddDirectory($"{Root}/hwmon0")
            .AddFile($"{Root}/hwmon0/name", "coretemp")
            .AddFile($"{Root}/hwmon0/temp1_input", "40000")
            .AddFile($"{Root}/hwmon0/temp2_input", "41000")
            .AddFile($"{Root}/hwmon0/temp3_input", "42000");

        var readings = new HwmonReader(fs).ReadAll();

        Assert.Equal(3, readings.Count);
        Assert.All(readings, r => Assert.Equal(SensorKind.Temperature, r.Kind));
    }
}
