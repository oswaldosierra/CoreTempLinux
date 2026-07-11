using CoreTempLinux.Sensors;
using CoreTempLinux.Tests.Fakes;
using Xunit;

namespace CoreTempLinux.Tests;

public class CpuFrequencyTests
{
    private const string Base = "/sys/devices/system/cpu";

    private static FakeFileSystem WithCpus(int count)
    {
        var fs = new FakeFileSystem();
        for (var i = 0; i < count; i++)
            fs.AddDirectory($"{Base}/cpu{i}");
        return fs;
    }

    [Fact]
    public void CoreCount_CuentaLosDirectoriosCpuN()
    {
        var fs = WithCpus(4);

        Assert.Equal(4, new CpuFrequency(fs).CoreCount);
    }

    [Fact]
    public void SinCpus_CoreCountEsCero()
    {
        Assert.Equal(0, new CpuFrequency(new FakeFileSystem()).CoreCount);
    }

    [Fact]
    public void LeeKiloherciosYConvierteAMhz()
    {
        var fs = WithCpus(2);
        fs.AddFile($"{Base}/cpu0/cpufreq/scaling_cur_freq", "3200000"); // kHz
        fs.AddFile($"{Base}/cpu1/cpufreq/scaling_cur_freq", "1600000");

        var mhz = new CpuFrequency(fs).ReadMhz();

        Assert.Equal(new[] { 3200.0, 1600.0 }, mhz);
    }

    [Fact]
    public void NucleoNoLegible_EsNaN()
    {
        var fs = WithCpus(2);
        fs.AddFile($"{Base}/cpu0/cpufreq/scaling_cur_freq", "2000000");
        // cpu1 no tiene el archivo scaling_cur_freq.

        var mhz = new CpuFrequency(fs).ReadMhz();

        Assert.Equal(2000.0, mhz[0]);
        Assert.True(double.IsNaN(mhz[1]));
    }

    [Fact]
    public void ValorNoNumerico_EsNaN()
    {
        var fs = WithCpus(1);
        fs.AddFile($"{Base}/cpu0/cpufreq/scaling_cur_freq", "<error>");

        var mhz = new CpuFrequency(fs).ReadMhz();

        Assert.True(double.IsNaN(mhz[0]));
    }

    [Fact]
    public void SeDetieneEnElPrimerHueco()
    {
        // Existen cpu0 y cpu2 pero no cpu1: el escaneo para en cpu1.
        var fs = new FakeFileSystem()
            .AddDirectory($"{Base}/cpu0")
            .AddDirectory($"{Base}/cpu2");

        Assert.Equal(1, new CpuFrequency(fs).CoreCount);
    }
}
