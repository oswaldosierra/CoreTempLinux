using System.Diagnostics;
using CoreTempLinux.Diagnostics;

namespace CoreTempLinux.Ui;

/// <summary>
/// Reproduce un sonido de alerta lanzando un reproductor de audio del sistema
/// (paplay/pw-play). Evita solapar reproducciones: si la anterior sigue sonando
/// no lanza otra, de modo que llamarlo cada segundo produce un tono continuo.
/// </summary>
public sealed class AudioAlert : IAudioAlert
{
    private static readonly string? SoundFile = FirstExisting(
        "/usr/share/sounds/freedesktop/stereo/alarm-clock-elapsed.oga",
        "/usr/share/sounds/freedesktop/stereo/dialog-warning.oga",
        "/usr/share/sounds/freedesktop/stereo/bell.oga");

    private static readonly string? Player = FirstOnPath("paplay", "pw-play", "ffplay");

    private readonly IAppLogger _log;

    private Process? _current;

    public bool Available => Player != null && SoundFile != null;

    public AudioAlert(IAppLogger log)
    {
        _log = log;
        if (!Available)
            _log.Info("Alerta sonora deshabilitada: no se encontró reproductor o sonido del sistema.");
    }

    /// <summary>Reproduce el sonido si no hay ya uno sonando.</summary>
    public void Play()
    {
        if (Player is null || SoundFile is null)
            return;

        if (_current is { HasExited: false })
            return; // Todavía sonando: no solapamos.

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Player,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add(SoundFile);
            _current = Process.Start(psi);
        }
        catch (Exception ex)
        {
            _current = null;
            _log.Warning($"No se pudo reproducir la alerta sonora con «{Player}».", ex);
        }
    }

    /// <summary>Corta inmediatamente el sonido en curso.</summary>
    public void Stop()
    {
        try
        {
            if (_current is { HasExited: false })
                _current.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            // El proceso pudo terminar entre la comprobación y el Kill.
            _log.Debug($"No se pudo detener la alerta sonora: {ex.GetType().Name}.");
        }
        finally
        {
            _current = null;
        }
    }

    private static string? FirstExisting(params string[] paths) =>
        paths.FirstOrDefault(File.Exists);

    private static string? FirstOnPath(params string[] names)
    {
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(':', StringSplitOptions.RemoveEmptyEntries);

        foreach (var name in names)
            foreach (var dir in dirs)
                if (File.Exists(Path.Combine(dir, name)))
                    return Path.Combine(dir, name);

        return null;
    }
}
