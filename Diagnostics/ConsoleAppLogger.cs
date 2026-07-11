using System.Globalization;

namespace CoreTempLinux.Diagnostics;

/// <summary>
/// Implementación de <see cref="IAppLogger"/> que escribe a un <see cref="TextWriter"/>
/// (por defecto, la salida de error estándar). Filtra por nivel mínimo y serializa
/// la escritura para no entremezclar líneas entre hilos.
/// </summary>
public sealed class ConsoleAppLogger : IAppLogger
{
    private readonly LogLevel _minLevel;
    private readonly TextWriter _output;
    private readonly object _gate = new();

    public ConsoleAppLogger(LogLevel minLevel = LogLevel.Info, TextWriter? output = null)
    {
        _minLevel = minLevel;
        _output = output ?? Console.Error;
    }

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (level < _minLevel)
            return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        var line = $"[{timestamp}] {level.ToString().ToUpperInvariant(),-7} {message}";

        lock (_gate)
        {
            _output.WriteLine(line);
            if (exception is not null)
                _output.WriteLine(exception);
        }
    }

    /// <summary>
    /// Traduce el valor de una variable de entorno a un <see cref="LogLevel"/>.
    /// Devuelve <c>null</c> si el texto no corresponde a ningún nivel conocido.
    /// </summary>
    public static LogLevel? ParseLevel(string? value) =>
        Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level) ? level : null;
}
