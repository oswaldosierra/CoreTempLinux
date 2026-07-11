namespace CoreTempLinux.Diagnostics;

/// <summary>Severidad de un mensaje de registro, de menor a mayor importancia.</summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>
/// Abstracción mínima de registro. Existe para que la aplicación pueda dejar
/// constancia de los fallos (en vez de tragarlos en silencio) sin acoplarse a
/// un destino concreto (consola, archivo, journald...).
/// </summary>
public interface IAppLogger
{
    void Log(LogLevel level, string message, Exception? exception = null);
}

/// <summary>Azúcar sintáctico para los niveles más habituales.</summary>
public static class AppLoggerExtensions
{
    public static void Debug(this IAppLogger log, string message) =>
        log.Log(LogLevel.Debug, message);

    public static void Info(this IAppLogger log, string message) =>
        log.Log(LogLevel.Info, message);

    public static void Warning(this IAppLogger log, string message, Exception? ex = null) =>
        log.Log(LogLevel.Warning, message, ex);

    public static void Error(this IAppLogger log, string message, Exception? ex = null) =>
        log.Log(LogLevel.Error, message, ex);
}
