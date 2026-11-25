using CoreTempLinux.Alerts;
using CoreTempLinux.Diagnostics;
using CoreTempLinux.Sensors;
using CoreTempLinux.Settings;
using CoreTempLinux.Ui;

// CoreTempLinux es exclusivamente para Linux: dependemos de /sys y /proc.
if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("CoreTempLinux solo funciona en Linux.");
    return 1;
}

// --- Composition root -----------------------------------------------------
// Aquí (y solo aquí) se construyen las implementaciones concretas y se inyectan
// por constructor. El nivel de registro se puede subir con CORETEMP_LOG=debug.
var logLevel = ConsoleAppLogger.ParseLevel(Environment.GetEnvironmentVariable("CORETEMP_LOG"))
    ?? LogLevel.Info;
var logger = new ConsoleAppLogger(logLevel);
var fileSystem = new LinuxFileSystem(logger);

// --minimized: arranca solo en la bandeja, sin ventana (lo usa el autostart al iniciar sesión).
var startMinimized = args.Contains("--minimized");

var app = Gtk.Application.New("org.coretemplinux.App", Gio.ApplicationFlags.FlagsNone);

// GtkApplication es de instancia única: si ya está corriendo (p. ej. la arrancó el
// autostart) y se vuelve a lanzar desde el menú, la instancia primaria recibe otra
// activación. En ese caso solo traemos al frente la ventana existente.
MainWindow? window = null;

app.OnActivate += (sender, _) =>
{
    var application = (Gtk.Application)sender;
    if (window is not null)
    {
        window.Present();
        return;
    }

    try
    {
        var monitor = new SensorMonitor(
            new HwmonReader(fileSystem),
            new CpuFrequency(fileSystem),
            new CpuLoad(fileSystem),
            CpuInfo.Read(fileSystem));

        // La bandeja necesita reactivar la ventana al hacer clic; el clic llega desde el
        // hilo de D-Bus, así que re-entramos al bucle de GTK con IdleAdd.
        // Las acciones llegan desde el hilo de D-Bus: se ejecutan en el bucle de GTK.
        Action OnGtk(Action action) => () => GLib.Functions.IdleAdd(0, () =>
        {
            action();
            return false;
        });

        var tray = new DBusTrayIcon(
            logger,
            onActivate: OnGtk(() => window?.ToggleFromTray()),
            menu: new[]
            {
                new TrayMenuItem("Mostrar ventana", OnGtk(() => window?.Present())),
                new TrayMenuItem("Ocultar en la bandeja", OnGtk(() => window?.Hide())),
                TrayMenuItem.Separator,
                new TrayMenuItem("Salir", OnGtk(application.Quit)),
            });

        window = new MainWindow(
            application,
            monitor,
            new AudioAlert(logger),
            new GtkNotifier(application, logger),
            tray,
            new AlertStateMachine(),
            new JsonSettingsStore(JsonSettingsStore.DefaultPath(), logger),
            logger);

        if (startMinimized)
            window.StartInTray();
        else
            window.Present();
    }
    catch (Exception ex)
    {
        logger.Error("No se pudo iniciar la ventana principal.", ex);
        application.Quit();
    }
};

return app.RunWithSynchronizationContext(null);
