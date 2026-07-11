using CoreTempLinux.Diagnostics;
using Tmds.DBus;

namespace CoreTempLinux.Ui;

/// <summary>
/// Icono de bandeja implementando el estándar <c>StatusNotifierItem</c> (SNI) sobre
/// D-Bus: lo usan KDE y GNOME (con la extensión "AppIndicator and KStatusNotifierItem
/// Support"). Renderiza la temperatura como número con <see cref="TrayIconRenderer"/>.
///
/// Nunca lanza: si no hay bus de sesión o no hay un host SNI, registra un aviso y se
/// comporta como <see cref="NullTrayIcon"/> (la aplicación sigue funcionando sin bandeja).
/// </summary>
public sealed class DBusTrayIcon : ITrayIcon
{
    private const string ObjPath = "/StatusNotifierItem";

    private readonly IAppLogger _log;
    private readonly Action? _onActivate;
    private readonly StatusNotifierItem _item;

    private Connection? _connection;
    private volatile bool _registered;

    /// <param name="onActivate">
    /// Se invoca cuando el usuario activa el icono (clic). El llamante es responsable de
    /// re-entrar al hilo de GTK si lo necesita (p.ej. con <c>GLib.Functions.IdleAdd</c>).
    /// </param>
    public DBusTrayIcon(IAppLogger log, Action? onActivate = null)
    {
        _log = log;
        _onActivate = onActivate;
        _item = new StatusNotifierItem(() => _onActivate?.Invoke());

        // El registro es asíncrono y tolerante a fallos; corre fuera del hilo de GTK
        // para no acoplarse a su bucle ni bloquear el arranque.
        _ = Task.Run(InitAsync);
    }

    public void Update(double? tempC, double? criticalC, string tooltip)
    {
        if (!_registered)
            return;

        try
        {
            var level = tempC is double t ? TempScale.Classify(t, criticalC) : TempLevel.Cool;
            var (w, h, argb) = TrayIconRenderer.Render(tempC, level);
            _item.UpdateIcon(w, h, argb, tooltip);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Debug, "No se pudo actualizar el icono de la bandeja.", ex);
        }
    }

    private async Task InitAsync()
    {
        try
        {
            var connection = new Connection(Address.Session!);
            var info = await connection.ConnectAsync().ConfigureAwait(false);

            await connection.RegisterObjectAsync(_item).ConfigureAwait(false);

            var watcher = connection.CreateProxy<IStatusNotifierWatcher>(
                "org.kde.StatusNotifierWatcher", "/StatusNotifierWatcher");
            await watcher.RegisterStatusNotifierItemAsync(info.LocalName).ConfigureAwait(false);

            _connection = connection;
            _registered = true;
            _log.Info("Icono de bandeja registrado (StatusNotifierItem).");
        }
        catch (Exception ex)
        {
            _log.Warning(
                "No hay bandeja del sistema compatible (StatusNotifierItem). " +
                "En GNOME requiere la extensión AppIndicator. Continúo sin icono de bandeja.",
                ex);
        }
    }

    public void Dispose()
    {
        try
        {
            _connection?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Debug, "Fallo al cerrar la conexión D-Bus de la bandeja.", ex);
        }
    }
}

/// <summary>Proxy del vigilante de iconos de estado; le anunciamos nuestro item.</summary>
[DBusInterface("org.kde.StatusNotifierWatcher")]
public interface IStatusNotifierWatcher : IDBusObject
{
    Task RegisterStatusNotifierItemAsync(string service);
}

/// <summary>Propiedades del item SNI (mapa a{sv} que expone D-Bus).</summary>
[Dictionary]
public sealed class StatusNotifierItemProperties
{
    public string Category = "Hardware";
    public string Id = "coretemplinux";
    public string Title = "CoreTemp Linux";
    public string Status = "Active";
    public uint WindowId = 0;
    public string IconName = "";
    public (int, int, byte[])[] IconPixmap = Array.Empty<(int, int, byte[])>();
    public string OverlayIconName = "";
    public (int, int, byte[])[] OverlayIconPixmap = Array.Empty<(int, int, byte[])>();
    public string AttentionIconName = "";
    public (int, int, byte[])[] AttentionIconPixmap = Array.Empty<(int, int, byte[])>();
    public string AttentionMovieName = "";
    public (string, (int, int, byte[])[], string, string) ToolTip =
        ("", Array.Empty<(int, int, byte[])>(), "CoreTemp Linux", "");
    public bool ItemIsMenu = false;
    public ObjectPath Menu = new("/NO_DBUSMENU");
    public string IconThemePath = "";
}

/// <summary>Interfaz D-Bus del item SNI (usada como objeto de servidor).</summary>
[DBusInterface("org.kde.StatusNotifierItem")]
public interface IStatusNotifierItem : IDBusObject
{
    Task ContextMenuAsync(int x, int y);
    Task ActivateAsync(int x, int y);
    Task SecondaryActivateAsync(int x, int y);
    Task ScrollAsync(int delta, string orientation);

    Task<object> GetAsync(string prop);
    Task<StatusNotifierItemProperties> GetAllAsync();
    Task SetAsync(string prop, object val);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);

    Task<IDisposable> WatchNewTitleAsync(Action handler);
    Task<IDisposable> WatchNewIconAsync(Action handler);
    Task<IDisposable> WatchNewAttentionIconAsync(Action handler);
    Task<IDisposable> WatchNewOverlayIconAsync(Action handler);
    Task<IDisposable> WatchNewToolTipAsync(Action handler);
    Task<IDisposable> WatchNewStatusAsync(Action<string> handler);
}

/// <summary>Objeto de servidor que publica el item SNI en <c>/StatusNotifierItem</c>.</summary>
internal sealed class StatusNotifierItem : IStatusNotifierItem
{
    private readonly object _gate = new();
    private readonly Action _onActivate;
    private readonly StatusNotifierItemProperties _props = new();

    public StatusNotifierItem(Action onActivate) => _onActivate = onActivate;

    public ObjectPath ObjectPath => new("/StatusNotifierItem");

    // Señales que emite el item (Tmds.DBus las conecta al registrar el objeto).
    public event Action? OnNewTitle;
    public event Action? OnNewIcon;
    public event Action? OnNewAttentionIcon;
    public event Action? OnNewOverlayIcon;
    public event Action? OnNewToolTip;
    public event Action<string>? OnNewStatus;

    /// <summary>Sustituye el pixmap y el tooltip, y avisa al host con las señales.</summary>
    public void UpdateIcon(int w, int h, byte[] argb, string tooltip)
    {
        lock (_gate)
        {
            _props.IconPixmap = new[] { (w, h, argb) };
            _props.ToolTip = ("", Array.Empty<(int, int, byte[])>(), "CoreTemp Linux", tooltip);
        }

        OnNewIcon?.Invoke();
        OnNewToolTip?.Invoke();
    }

    public Task ActivateAsync(int x, int y)
    {
        _onActivate();
        return Task.CompletedTask;
    }

    public Task SecondaryActivateAsync(int x, int y)
    {
        _onActivate();
        return Task.CompletedTask;
    }

    public Task ContextMenuAsync(int x, int y) => Task.CompletedTask;
    public Task ScrollAsync(int delta, string orientation) => Task.CompletedTask;

    public Task<object> GetAsync(string prop)
    {
        lock (_gate)
        {
            object value = prop switch
            {
                nameof(StatusNotifierItemProperties.Category) => _props.Category,
                nameof(StatusNotifierItemProperties.Id) => _props.Id,
                nameof(StatusNotifierItemProperties.Title) => _props.Title,
                nameof(StatusNotifierItemProperties.Status) => _props.Status,
                nameof(StatusNotifierItemProperties.WindowId) => _props.WindowId,
                nameof(StatusNotifierItemProperties.IconName) => _props.IconName,
                nameof(StatusNotifierItemProperties.IconPixmap) => _props.IconPixmap,
                nameof(StatusNotifierItemProperties.ToolTip) => _props.ToolTip,
                nameof(StatusNotifierItemProperties.ItemIsMenu) => _props.ItemIsMenu,
                nameof(StatusNotifierItemProperties.Menu) => _props.Menu,
                _ => "",
            };
            return Task.FromResult(value);
        }
    }

    public Task<StatusNotifierItemProperties> GetAllAsync()
    {
        lock (_gate)
        {
            // Copia superficial para que el host lea un estado coherente.
            return Task.FromResult(new StatusNotifierItemProperties
            {
                Category = _props.Category,
                Id = _props.Id,
                Title = _props.Title,
                Status = _props.Status,
                WindowId = _props.WindowId,
                IconName = _props.IconName,
                IconPixmap = _props.IconPixmap,
                ToolTip = _props.ToolTip,
                ItemIsMenu = _props.ItemIsMenu,
                Menu = _props.Menu,
            });
        }
    }

    public Task SetAsync(string prop, object val) => Task.CompletedTask;

    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler) =>
        SignalWatcher.AddAsync(this, nameof(OnNewIcon), () => { });

    public Task<IDisposable> WatchNewTitleAsync(Action handler) =>
        SignalWatcher.AddAsync(this, nameof(OnNewTitle), handler);

    public Task<IDisposable> WatchNewIconAsync(Action handler) =>
        SignalWatcher.AddAsync(this, nameof(OnNewIcon), handler);

    public Task<IDisposable> WatchNewAttentionIconAsync(Action handler) =>
        SignalWatcher.AddAsync(this, nameof(OnNewAttentionIcon), handler);

    public Task<IDisposable> WatchNewOverlayIconAsync(Action handler) =>
        SignalWatcher.AddAsync(this, nameof(OnNewOverlayIcon), handler);

    public Task<IDisposable> WatchNewToolTipAsync(Action handler) =>
        SignalWatcher.AddAsync(this, nameof(OnNewToolTip), handler);

    public Task<IDisposable> WatchNewStatusAsync(Action<string> handler) =>
        SignalWatcher.AddAsync(this, nameof(OnNewStatus), handler);
}
