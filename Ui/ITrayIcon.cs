namespace CoreTempLinux.Ui;

/// <summary>
/// Icono en la bandeja del sistema que muestra la temperatura de la CPU como número,
/// al estilo de Core Temp. Es un seam: la implementación real habla D-Bus, pero puede
/// sustituirse por una nula (sin escritorio compatible) o falsa (en pruebas).
/// </summary>
public interface ITrayIcon : IDisposable
{
    /// <summary>Actualiza el número y el tooltip. <paramref name="tempC"/> nula = "--".</summary>
    void Update(double? tempC, double? criticalC, string tooltip);
}

/// <summary>Implementación nula: no hace nada. Se usa como respaldo si no hay bandeja.</summary>
public sealed class NullTrayIcon : ITrayIcon
{
    public void Update(double? tempC, double? criticalC, string tooltip) { }
    public void Dispose() { }
}
