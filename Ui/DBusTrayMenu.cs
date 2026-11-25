using Tmds.DBus;

namespace CoreTempLinux.Ui;

/// <summary>
/// Entrada del menú contextual (clic derecho) del icono de bandeja. <paramref name="Label"/>
/// nulo = separador. <paramref name="OnClick"/> se invoca desde el hilo de D-Bus: el
/// llamante debe re-entrar al hilo de GTK si lo necesita (p. ej. con <c>IdleAdd</c>).
/// </summary>
public sealed record TrayMenuItem(string? Label, Action? OnClick = null)
{
    public static TrayMenuItem Separator { get; } = new(Label: null);
}

/// <summary>Contrato <c>com.canonical.dbusmenu</c> con el que los hosts SNI piden el menú.</summary>
[DBusInterface("com.canonical.dbusmenu")]
public interface IDBusMenu : IDBusObject
{
    Task<(uint revision, (int, IDictionary<string, object>, object[]) layout)> GetLayoutAsync(
        int parentId, int recursionDepth, string[] propertyNames);
    Task<(int, IDictionary<string, object>)[]> GetGroupPropertiesAsync(int[] ids, string[] propertyNames);
    Task<object> GetPropertyAsync(int id, string name);
    Task EventAsync(int id, string eventId, object data, uint timestamp);
    Task<int[]> EventGroupAsync((int, string, object, uint)[] events);
    Task<bool> AboutToShowAsync(int id);
    Task<(int[] updatesNeeded, int[] idErrors)> AboutToShowGroupAsync(int[] ids);

    Task<IDisposable> WatchLayoutUpdatedAsync(Action<(uint revision, int parent)> handler);

    Task<object> GetAsync(string prop);
    Task<DBusMenuProperties> GetAllAsync();
    Task SetAsync(string prop, object val);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

[Dictionary]
public sealed class DBusMenuProperties
{
    public uint Version = 3;
    public string TextDirection = "ltr";
    public string Status = "normal";
    public string[] IconThemePath = Array.Empty<string>();
}

/// <summary>
/// Menú estático publicado en <c>/MenuBar</c>. El id 0 es la raíz; cada
/// <see cref="TrayMenuItem"/> recibe el id <c>índice + 1</c>.
/// </summary>
internal sealed class DBusTrayMenu : IDBusMenu
{
    public static readonly ObjectPath Path = new("/MenuBar");

    private readonly IReadOnlyList<TrayMenuItem> _items;

    public DBusTrayMenu(IReadOnlyList<TrayMenuItem> items) => _items = items;

    public ObjectPath ObjectPath => Path;

    // El menú no cambia, pero el host se suscribe a la señal igualmente.
    public event Action<(uint revision, int parent)>? OnLayoutUpdated;

    public Task<(uint revision, (int, IDictionary<string, object>, object[]) layout)> GetLayoutAsync(
        int parentId, int recursionDepth, string[] propertyNames)
    {
        if (parentId != 0)
            return Task.FromResult((1u, (parentId, Props(parentId), Array.Empty<object>())));

        var children = recursionDepth == 0
            ? Array.Empty<object>()
            : _items.Select((_, i) => (object)(i + 1, Props(i + 1), Array.Empty<object>())).ToArray();

        return Task.FromResult((1u, (0, Props(0), children)));
    }

    public Task<(int, IDictionary<string, object>)[]> GetGroupPropertiesAsync(int[] ids, string[] propertyNames) =>
        Task.FromResult(ids.Select(id => (id, Props(id))).ToArray());

    public Task<object> GetPropertyAsync(int id, string name) =>
        Task.FromResult(Props(id).TryGetValue(name, out var v) ? v : "");

    public Task EventAsync(int id, string eventId, object data, uint timestamp)
    {
        if (eventId == "clicked" && id >= 1 && id <= _items.Count)
            _items[id - 1].OnClick?.Invoke();
        return Task.CompletedTask;
    }

    public async Task<int[]> EventGroupAsync((int, string, object, uint)[] events)
    {
        foreach (var (id, eventId, data, ts) in events)
            await EventAsync(id, eventId, data, ts).ConfigureAwait(false);
        return Array.Empty<int>();
    }

    public Task<bool> AboutToShowAsync(int id) => Task.FromResult(false);

    public Task<(int[] updatesNeeded, int[] idErrors)> AboutToShowGroupAsync(int[] ids) =>
        Task.FromResult((Array.Empty<int>(), Array.Empty<int>()));

    public Task<IDisposable> WatchLayoutUpdatedAsync(Action<(uint revision, int parent)> handler) =>
        SignalWatcher.AddAsync(this, nameof(OnLayoutUpdated), handler);

    public Task<object> GetAsync(string prop)
    {
        var p = new DBusMenuProperties();
        object value = prop switch
        {
            nameof(DBusMenuProperties.Version) => p.Version,
            nameof(DBusMenuProperties.TextDirection) => p.TextDirection,
            nameof(DBusMenuProperties.Status) => p.Status,
            nameof(DBusMenuProperties.IconThemePath) => p.IconThemePath,
            _ => "",
        };
        return Task.FromResult(value);
    }

    public Task<DBusMenuProperties> GetAllAsync() => Task.FromResult(new DBusMenuProperties());

    public Task SetAsync(string prop, object val) => Task.CompletedTask;

    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler) =>
        Task.FromResult<IDisposable>(new NoopDisposable());

    private IDictionary<string, object> Props(int id)
    {
        if (id == 0)
            return new Dictionary<string, object> { ["children-display"] = "submenu" };

        if (id < 1 || id > _items.Count)
            return new Dictionary<string, object>();

        var item = _items[id - 1];
        return item.Label is null
            ? new Dictionary<string, object> { ["type"] = "separator" }
            : new Dictionary<string, object>
            {
                ["label"] = item.Label,
                ["enabled"] = true,
                ["visible"] = true,
            };
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
