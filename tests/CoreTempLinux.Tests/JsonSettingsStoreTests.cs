using CoreTempLinux.Settings;
using CoreTempLinux.Tests.Fakes;
using Xunit;

namespace CoreTempLinux.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _root;
    private readonly string _path;
    private readonly RecordingLogger _log = new();

    public JsonSettingsStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "coretemp-settings-" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_root, "sub", "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { /* limpieza best-effort */ }
    }

    [Fact]
    public void Load_SinArchivo_DevuelveValoresPorDefecto()
    {
        var store = new JsonSettingsStore(_path, _log);

        Assert.Equal(AppSettings.DefaultThreshold, store.Load().AlertThresholdC);
    }

    [Fact]
    public void Save_Y_Load_ConservanElUmbral()
    {
        new JsonSettingsStore(_path, _log).Save(new AppSettings { AlertThresholdC = 72 });

        Assert.Equal(72, new JsonSettingsStore(_path, _log).Load().AlertThresholdC);
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void Load_ArchivoCorrupto_DevuelveValoresPorDefectoSinLanzar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, "{ esto no es json");

        Assert.Equal(AppSettings.DefaultThreshold, new JsonSettingsStore(_path, _log).Load().AlertThresholdC);
    }

    [Theory]
    [InlineData(5, AppSettings.MinThreshold)]
    [InlineData(500, AppSettings.MaxThreshold)]
    public void Load_UmbralFueraDeRango_SeAjusta(double stored, double expected)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, $"{{ \"AlertThresholdC\": {stored} }}");

        Assert.Equal(expected, new JsonSettingsStore(_path, _log).Load().AlertThresholdC);
    }
}
