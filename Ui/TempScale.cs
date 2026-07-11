namespace CoreTempLinux.Ui;

/// <summary>Nivel térmico de la CPU, al estilo de los colores de Core Temp.</summary>
public enum TempLevel
{
    Cool,
    Warm,
    Hot,
    Crit,
}

/// <summary>
/// Clasifica una temperatura en un <see cref="TempLevel"/> y traduce ese nivel a
/// clase CSS (para la UI) o a color RGB (para el icono de la bandeja). Al concentrar
/// aquí el criterio, la ventana y la bandeja muestran siempre el mismo color.
/// </summary>
public static class TempScale
{
    /// <summary>
    /// Si conocemos el crítico (Tj.Max), clasificamos por cercanía a él; si no
    /// (típico en AMD/Linux), usamos umbrales absolutos razonables.
    /// </summary>
    public static TempLevel Classify(double tempC, double? criticalC)
    {
        if (criticalC is double c && c > 0)
        {
            var ratio = tempC / c;
            return ratio < 0.70 ? TempLevel.Cool
                : ratio < 0.85 ? TempLevel.Warm
                : ratio < 0.95 ? TempLevel.Hot
                : TempLevel.Crit;
        }

        return tempC < 65 ? TempLevel.Cool
            : tempC < 80 ? TempLevel.Warm
            : tempC < 90 ? TempLevel.Hot
            : TempLevel.Crit;
    }

    /// <summary>Nombre de clase CSS asociada al nivel (ver <c>MainWindow.LoadCss</c>).</summary>
    public static string CssClass(TempLevel level) => level switch
    {
        TempLevel.Cool => "temp-cool",
        TempLevel.Warm => "temp-warm",
        TempLevel.Hot => "temp-hot",
        _ => "temp-crit",
    };

    /// <summary>Color RGB del nivel (mismos tonos que el CSS).</summary>
    public static (byte R, byte G, byte B) Rgb(TempLevel level) => level switch
    {
        TempLevel.Cool => (0x4c, 0xd1, 0x37), // verde
        TempLevel.Warm => (0xf1, 0xc4, 0x0f), // amarillo
        TempLevel.Hot => (0xe6, 0x7e, 0x22),  // naranja
        _ => (0xe7, 0x4c, 0x3c),              // rojo
    };

    /// <summary>Todas las clases CSS de temperatura (para limpiarlas antes de aplicar una).</summary>
    public static readonly string[] AllCssClasses =
        { "temp-cool", "temp-warm", "temp-hot", "temp-crit" };
}
