using CoreTempLinux.Diagnostics;
using CoreTempLinux.Sensors;
using CoreTempLinux.Tests.Fakes;
using Xunit;

namespace CoreTempLinux.Tests;

/// <summary>
/// <see cref="LinuxFileSystem"/> golpea el disco real, así que estas pruebas usan un
/// directorio temporal propio que se limpia al terminar.
/// </summary>
public sealed class LinuxFileSystemTests : IDisposable
{
    private readonly string _root;
    private readonly RecordingLogger _log = new();
    private readonly LinuxFileSystem _fs;

    public LinuxFileSystemTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "coretemp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _fs = new LinuxFileSystem(_log);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { /* limpieza best-effort */ }
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ReadText_RecortaEspacios()
    {
        var path = Write("temp", "  45000\n");

        Assert.Equal("45000", _fs.ReadText(path));
    }

    [Fact]
    public void ReadText_ArchivoAusente_DevuelveNull()
    {
        Assert.Null(_fs.ReadText(Path.Combine(_root, "no-existe")));
    }

    [Fact]
    public void ReadLines_DevuelveCadaLinea()
    {
        var path = Write("stat", "cpu0 1 2 3\ncpu1 4 5 6");

        var lines = _fs.ReadLines(path);

        Assert.Equal(new[] { "cpu0 1 2 3", "cpu1 4 5 6" }, lines);
    }

    [Fact]
    public void ReadLines_ArchivoAusente_DevuelveVacio()
    {
        Assert.Empty(_fs.ReadLines(Path.Combine(_root, "no-existe")));
    }

    [Fact]
    public void FileExists_DistingueExistente()
    {
        var path = Write("f", "x");

        Assert.True(_fs.FileExists(path));
        Assert.False(_fs.FileExists(Path.Combine(_root, "otro")));
    }

    [Fact]
    public void DirectoryExists_DistingueExistente()
    {
        Assert.True(_fs.DirectoryExists(_root));
        Assert.False(_fs.DirectoryExists(Path.Combine(_root, "sub-inexistente")));
    }

    [Fact]
    public void GetDirectories_DevuelveSubdirectorios()
    {
        Directory.CreateDirectory(Path.Combine(_root, "a"));
        Directory.CreateDirectory(Path.Combine(_root, "b"));

        var dirs = _fs.GetDirectories(_root).Select(Path.GetFileName).OrderBy(x => x);

        Assert.Equal(new[] { "a", "b" }, dirs);
    }

    [Fact]
    public void GetDirectories_RutaAusente_DevuelveVacio()
    {
        Assert.Empty(_fs.GetDirectories(Path.Combine(_root, "no-existe")));
    }

    [Fact]
    public void GetFiles_FiltraPorPatron()
    {
        Write("temp1_input", "1");
        Write("temp2_input", "2");
        Write("fan1_input", "3");

        var files = _fs.GetFiles(_root, "temp*_input").Select(Path.GetFileName).OrderBy(x => x);

        Assert.Equal(new[] { "temp1_input", "temp2_input" }, files);
    }

    [Fact]
    public void GetFiles_DirAusente_DevuelveVacio()
    {
        Assert.Empty(_fs.GetFiles(Path.Combine(_root, "no-existe"), "*"));
    }

    [Fact]
    public void FallosEsperados_NoGeneranWarning()
    {
        _fs.ReadText(Path.Combine(_root, "no-existe"));
        _fs.ReadLines(Path.Combine(_root, "no-existe"));
        _fs.GetDirectories(Path.Combine(_root, "no-existe"));

        Assert.Empty(_log.OfLevel(LogLevel.Warning));
    }
}
