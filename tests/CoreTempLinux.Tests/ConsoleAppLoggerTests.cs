using CoreTempLinux.Diagnostics;
using Xunit;

namespace CoreTempLinux.Tests;

public class ConsoleAppLoggerTests
{
    [Theory]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData("DEBUG", LogLevel.Debug)]
    [InlineData("Info", LogLevel.Info)]
    [InlineData("warning", LogLevel.Warning)]
    [InlineData("Error", LogLevel.Error)]
    public void ParseLevel_ReconoceNivelesSinImportarMayusculas(string value, LogLevel expected)
    {
        Assert.Equal(expected, ConsoleAppLogger.ParseLevel(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("verbose")]
    public void ParseLevel_DevuelveNullSiNoReconoce(string? value)
    {
        Assert.Null(ConsoleAppLogger.ParseLevel(value));
    }

    [Fact]
    public void PorDebajoDelNivelMinimo_NoEscribe()
    {
        var sw = new StringWriter();
        var log = new ConsoleAppLogger(LogLevel.Warning, sw);

        log.Debug("no debería aparecer");
        log.Info("tampoco");

        Assert.Equal(string.Empty, sw.ToString());
    }

    [Fact]
    public void AlNivelOSuperior_Escribe()
    {
        var sw = new StringWriter();
        var log = new ConsoleAppLogger(LogLevel.Info, sw);

        log.Info("hola");

        Assert.Contains("hola", sw.ToString());
        Assert.Contains("INFO", sw.ToString());
    }

    [Fact]
    public void ConExcepcion_LaIncluyeEnLaSalida()
    {
        var sw = new StringWriter();
        var log = new ConsoleAppLogger(LogLevel.Info, sw);

        log.Warning("falló", new InvalidOperationException("boom"));

        Assert.Contains("falló", sw.ToString());
        Assert.Contains("boom", sw.ToString());
    }
}
