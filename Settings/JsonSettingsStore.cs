using System.Text.Json;
using CoreTempLinux.Diagnostics;

namespace CoreTempLinux.Settings;

/// <summary>
/// Guarda las preferencias como JSON (por defecto en
/// <c>$XDG_CONFIG_HOME/coretemplinux/settings.json</c>). La escritura es atómica
/// (archivo temporal + rename) para no dejar un JSON a medias si la app muere.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;
    private readonly IAppLogger _log;

    public JsonSettingsStore(string path, IAppLogger log)
    {
        _path = path;
        _log = log;
    }

    /// <summary>Ruta estándar según la especificación XDG.</summary>
    public static string DefaultPath()
    {
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(config))
            config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(config, "coretemplinux", "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
            return new AppSettings();

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options);
            return (settings ?? new AppSettings()).Normalized();
        }
        catch (Exception ex)
        {
            _log.Warning($"No se pudo leer la configuración ({_path}); uso valores por defecto.", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings.Normalized(), Options));
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.Warning($"No se pudo guardar la configuración ({_path}).", ex);
        }
    }
}
