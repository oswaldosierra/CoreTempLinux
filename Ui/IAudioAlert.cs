namespace CoreTempLinux.Ui;

/// <summary>Reproductor del sonido de alerta, con arranque y parada bajo demanda.</summary>
public interface IAudioAlert
{
    /// <summary>¿Hay reproductor y archivo de sonido disponibles en el sistema?</summary>
    bool Available { get; }

    /// <summary>Reproduce el sonido si no hay ya uno sonando.</summary>
    void Play();

    /// <summary>Corta inmediatamente el sonido en curso.</summary>
    void Stop();
}

/// <summary>Envía notificaciones de escritorio.</summary>
public interface INotifier
{
    void Notify(string title, string body);
}
