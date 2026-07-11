using CoreTempLinux.Sensors;
using CoreTempLinux.Tests.Fakes;
using Xunit;

namespace CoreTempLinux.Tests;

public class CpuInfoTests
{
    [Fact]
    public void LeeModeloYCuentaProcesadores()
    {
        var fs = new FakeFileSystem().AddFile("/proc/cpuinfo", string.Join("\n", new[]
        {
            "processor\t: 0",
            "model name\t: AMD Ryzen 7 5800X",
            "processor\t: 1",
            "model name\t: AMD Ryzen 7 5800X",
        }));

        var info = CpuInfo.Read(fs);

        Assert.Equal("AMD Ryzen 7 5800X", info.ModelName);
        Assert.Equal(2, info.LogicalCores);
    }

    [Fact]
    public void SinModelo_UsaValorPorDefecto()
    {
        var fs = new FakeFileSystem().AddFile("/proc/cpuinfo", "processor\t: 0");

        var info = CpuInfo.Read(fs);

        Assert.Equal("CPU desconocida", info.ModelName);
        Assert.Equal(1, info.LogicalCores);
    }

    [Fact]
    public void SinNucleos_CaeAProcessorCount()
    {
        var fs = new FakeFileSystem().AddFile("/proc/cpuinfo", "algo irrelevante");

        var info = CpuInfo.Read(fs);

        Assert.Equal(Environment.ProcessorCount, info.LogicalCores);
    }

    [Fact]
    public void ArchivoAusente_UsaValoresPorDefecto()
    {
        var info = CpuInfo.Read(new FakeFileSystem());

        Assert.Equal("CPU desconocida", info.ModelName);
        Assert.Equal(Environment.ProcessorCount, info.LogicalCores);
    }

    [Fact]
    public void LeeVendorFamiliaModeloStepping_YNucleosFisicos()
    {
        var fs = new FakeFileSystem()
            .AddFile("/proc/cpuinfo", string.Join("\n", new[]
            {
                "processor\t: 0",
                "vendor_id\t: AuthenticAMD",
                "cpu family\t: 26",
                "model\t\t: 68",
                "model name\t: AMD Ryzen 5 9600X 6-Core Processor",
                "stepping\t: 0",
                "physical id\t: 0",
                "cpu cores\t: 6",
                "processor\t: 1",
                "physical id\t: 0",
                "cpu cores\t: 6",
            }))
            .AddFile("/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq", "5390000");

        var info = CpuInfo.Read(fs);

        Assert.Equal("AuthenticAMD", info.VendorId);
        Assert.True(info.IsAmd);
        Assert.False(info.IsIntel);
        Assert.Equal(26, info.Family);   // 0x1A
        Assert.Equal(68, info.ModelId);  // 0x44
        Assert.Equal(0, info.Stepping);
        Assert.Equal(6, info.PhysicalCores);
        Assert.Equal(2, info.LogicalCores);
        Assert.Equal(1, info.Sockets);
        Assert.Equal(5390.0, info.MaxFreqMhz);
    }

    [Fact]
    public void ModelName_NoSeConfundeConModelNumerico()
    {
        var fs = new FakeFileSystem().AddFile("/proc/cpuinfo", string.Join("\n", new[]
        {
            "processor\t: 0",
            "model\t\t: 68",
            "model name\t: Intel Core i7",
            "vendor_id\t: GenuineIntel",
        }));

        var info = CpuInfo.Read(fs);

        Assert.Equal("Intel Core i7", info.ModelName);
        Assert.Equal(68, info.ModelId);
        Assert.True(info.IsIntel);
    }

    [Fact]
    public void DosZocalos_SeCuentanPorPhysicalIdDistinto()
    {
        var fs = new FakeFileSystem().AddFile("/proc/cpuinfo", string.Join("\n", new[]
        {
            "processor\t: 0",
            "physical id\t: 0",
            "processor\t: 1",
            "physical id\t: 1",
        }));

        var info = CpuInfo.Read(fs);

        Assert.Equal(2, info.Sockets);
    }

    [Fact]
    public void SinCpufreq_MaxFreqEsNaN()
    {
        var fs = new FakeFileSystem().AddFile("/proc/cpuinfo", "processor\t: 0");

        var info = CpuInfo.Read(fs);

        Assert.True(double.IsNaN(info.MaxFreqMhz));
    }
}
