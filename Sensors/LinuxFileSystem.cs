using CoreTempLinux.Diagnostics;

namespace CoreTempLinux.Sensors;

/// <summary>
/// Implementación de <see cref="IFileSystem"/> sobre el sistema real. Nunca lanza:
/// distingue los fallos <em>esperados</em> (un sensor ausente o sin permisos, algo
/// habitual porque el hardware varía entre máquinas) de los <em>inesperados</em>.
/// Los primeros se registran en nivel Debug; los segundos como Warning, y solo la
/// primera vez por ruta, para no inundar el registro cuando se lee cada segundo.
/// </summary>
public sealed class LinuxFileSystem : IFileSystem
{
    private static readonly IReadOnlyList<string> Empty = Array.Empty<string>();

    private readonly IAppLogger _log;
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public LinuxFileSystem(IAppLogger log) => _log = log;

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception ex)
        {
            ReportUnexpected(path, ex);
            return false;
        }
    }

    public bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch (Exception ex)
        {
            ReportUnexpected(path, ex);
            return false;
        }
    }

    public string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            _log.Debug($"Lectura omitida de «{path}»: {ex.GetType().Name}.");
            return null;
        }
        catch (Exception ex)
        {
            ReportUnexpected(path, ex);
            return null;
        }
    }

    public IReadOnlyList<string> ReadLines(string path)
    {
        try
        {
            return File.ReadAllLines(path);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            _log.Debug($"Lectura omitida de «{path}»: {ex.GetType().Name}.");
            return Empty;
        }
        catch (Exception ex)
        {
            ReportUnexpected(path, ex);
            return Empty;
        }
    }

    public IReadOnlyList<string> GetDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            _log.Debug($"No hay directorios en «{path}»: {ex.GetType().Name}.");
            return Empty;
        }
        catch (Exception ex)
        {
            ReportUnexpected(path, ex);
            return Empty;
        }
    }

    public IReadOnlyList<string> GetFiles(string dir, string pattern)
    {
        try
        {
            return Directory.GetFiles(dir, pattern);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            _log.Debug($"No hay archivos «{pattern}» en «{dir}»: {ex.GetType().Name}.");
            return Empty;
        }
        catch (Exception ex)
        {
            ReportUnexpected(dir, ex);
            return Empty;
        }
    }

    /// <summary>
    /// Fallos normales al sondear <c>/sys</c> y <c>/proc</c>: el archivo no existe,
    /// el núcleo devuelve un error de E/S para ese sensor, o falta permiso.
    /// </summary>
    private static bool IsExpected(Exception ex) =>
        ex is FileNotFoundException
            or DirectoryNotFoundException
            or UnauthorizedAccessException
            or IOException;

    private void ReportUnexpected(string path, Exception ex)
    {
        lock (_gate)
        {
            if (!_reported.Add(path))
                return; // Ya avisamos de esta ruta; no repetimos cada segundo.
        }

        _log.Warning($"Error inesperado al acceder a «{path}».", ex);
    }
}
