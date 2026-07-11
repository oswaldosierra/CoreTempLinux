using CoreTempLinux.Diagnostics;

namespace CoreTempLinux.Ui;

/// <summary>
/// Notificaciones de escritorio a través de <see cref="Gio.Notification"/>. Aísla a
/// <see cref="MainWindow"/> de GIO y evita que un fallo al notificar interrumpa el ciclo.
/// </summary>
public sealed class GtkNotifier : INotifier
{
    private const string NotificationId = "coretemp-alert";

    private readonly Gtk.Application _app;
    private readonly IAppLogger _log;

    public GtkNotifier(Gtk.Application app, IAppLogger log)
    {
        _app = app;
        _log = log;
    }

    public void Notify(string title, string body)
    {
        try
        {
            var notification = Gio.Notification.New(title);
            notification.SetBody(body);
            _app.SendNotification(NotificationId, notification);
        }
        catch (Exception ex)
        {
            _log.Warning("No se pudo enviar la notificación de escritorio.", ex);
        }
    }
}
