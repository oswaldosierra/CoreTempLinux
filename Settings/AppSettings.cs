namespace CoreTempLinux.Settings;

/// <summary>Preferencias del usuario que sobreviven entre ejecuciones.</summary>
public sealed record AppSettings
{
    public const double MinThreshold = 40;
    public const double MaxThreshold = 110;
    public const double DefaultThreshold = 85;

    /// <summary>Temperatura (°C) a partir de la cual salta la alerta.</summary>
    public double AlertThresholdC { get; init; } = DefaultThreshold;

    /// <summary>Copia con los valores llevados a rangos válidos (archivo editado a mano, etc.).</summary>
    public AppSettings Normalized() => this with
    {
        AlertThresholdC = double.IsFinite(AlertThresholdC)
            ? Math.Clamp(AlertThresholdC, MinThreshold, MaxThreshold)
            : DefaultThreshold,
    };
}

/// <summary>
/// Persistencia de <see cref="AppSettings"/>. Nunca lanza: si no se puede leer devuelve
/// los valores por defecto, y si no se puede escribir lo deja registrado.
/// </summary>
public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
