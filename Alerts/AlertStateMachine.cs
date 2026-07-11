namespace CoreTempLinux.Alerts;

/// <summary>Resultado de evaluar la temperatura frente al umbral.</summary>
public enum AlertPhase
{
    /// <summary>Por debajo del umbral y sin alerta en curso: nada que hacer.</summary>
    Idle,

    /// <summary>Se acaba de cruzar el umbral: comienza un episodio nuevo.</summary>
    Started,

    /// <summary>Se sigue por encima del umbral dentro del mismo episodio.</summary>
    Active,

    /// <summary>Se ha bajado del umbral: el episodio termina.</summary>
    Ended,
}

/// <summary>
/// Modelo de alertas por <em>episodios</em>, extraído de la ventana para poder
/// razonarlo y probarlo sin GTK. No conoce banners, sonidos ni notificaciones:
/// solo decide en qué fase estamos.
/// <list type="bullet">
/// <item>Cruzar el umbral inicia un episodio (<see cref="AlertPhase.Started"/>).</item>
/// <item><see cref="Silence"/> silencia únicamente el episodio actual.</item>
/// <item>Bajar del umbral termina el episodio y rearma el silenciado, de modo que
///       el siguiente cruce vuelve a alertar aunque el anterior se hubiera silenciado.</item>
/// </list>
/// </summary>
public sealed class AlertStateMachine
{
    private bool _alerting;   // ¿hay un episodio activo ahora mismo?
    private bool _silenced;   // ¿el usuario silenció ESTE episodio?

    /// <summary>¿Hay un episodio de alerta activo?</summary>
    public bool IsAlerting => _alerting;

    /// <summary>¿El episodio actual está silenciado por el usuario?</summary>
    public bool IsSilenced => _silenced;

    /// <summary>
    /// Evalúa una temperatura frente al umbral y avanza la máquina de estados.
    /// </summary>
    /// <param name="temp">Temperatura actual, o <c>null</c> si no se pudo leer.</param>
    /// <param name="threshold">Umbral configurado (°C).</param>
    public AlertPhase Evaluate(double? temp, double threshold)
    {
        var over = temp is double t && t >= threshold;

        if (over)
        {
            if (_alerting)
                return AlertPhase.Active;

            _alerting = true;
            _silenced = false;
            return AlertPhase.Started;
        }

        if (_alerting)
        {
            _alerting = false;
            _silenced = false;
            return AlertPhase.Ended;
        }

        return AlertPhase.Idle;
    }

    /// <summary>Silencia el episodio actual (no afecta a episodios futuros).</summary>
    public void Silence() => _silenced = true;
}
