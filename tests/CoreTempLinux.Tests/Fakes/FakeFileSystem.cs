using CoreTempLinux.Sensors;

namespace CoreTempLinux.Tests.Fakes;

/// <summary>
/// <see cref="IFileSystem"/> en memoria para las pruebas. Se configura con un mapa
/// ruta → contenido; cualquier ruta ausente se comporta como un sensor inexistente
/// (igual que <see cref="LinuxFileSystem"/> ante un fallo esperado).
/// </summary>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirs = new(StringComparer.Ordinal);

    /// <summary>Registra un archivo con su contenido.</summary>
    public FakeFileSystem AddFile(string path, string content)
    {
        _files[path] = content;
        return this;
    }

    /// <summary>Registra un directorio (para <see cref="GetDirectories"/>/<see cref="DirectoryExists"/>).</summary>
    public FakeFileSystem AddDirectory(string path)
    {
        _dirs.Add(path.TrimEnd('/'));
        return this;
    }

    public bool FileExists(string path) => _files.ContainsKey(path);

    public bool DirectoryExists(string path) => _dirs.Contains(path.TrimEnd('/'));

    public string? ReadText(string path) =>
        _files.TryGetValue(path, out var v) ? v.Trim() : null;

    public IReadOnlyList<string> ReadLines(string path) =>
        _files.TryGetValue(path, out var v)
            ? v.Replace("\r\n", "\n").Split('\n')
            : Array.Empty<string>();

    public IReadOnlyList<string> GetDirectories(string path)
    {
        var prefix = path.TrimEnd('/') + "/";
        // Solo hijos directos: un único segmento tras el prefijo.
        return _dirs
            .Where(d => d.StartsWith(prefix, StringComparison.Ordinal)
                        && !d[prefix.Length..].Contains('/'))
            .ToList();
    }

    public IReadOnlyList<string> GetFiles(string dir, string pattern)
    {
        var prefix = dir.TrimEnd('/') + "/";
        var regex = GlobToRegex(pattern);
        return _files.Keys
            .Where(f => f.StartsWith(prefix, StringComparison.Ordinal)
                        && !f[prefix.Length..].Contains('/')
                        && regex.IsMatch(Path.GetFileName(f)))
            .ToList();
    }

    private static System.Text.RegularExpressions.Regex GlobToRegex(string pattern)
    {
        var escaped = System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".");
        return new System.Text.RegularExpressions.Regex($"^{escaped}$");
    }
}
