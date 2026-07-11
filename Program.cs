using CoreTempLinux.Alerts;
using CoreTempLinux.Diagnostics;
using CoreTempLinux.Sensors;
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

var app = Gtk.Application.New("org.coretemplinux.App", Gio.ApplicationFlags.FlagsNone);

app.OnActivate += (sender, _) =>
{
    var application = (Gtk.Application)sender;
    try
    {
        var monitor = new SensorMonitor(
            new HwmonReader(fileSystem),
            new CpuFrequency(fileSystem),
            new CpuLoad(fileSystem),
            CpuInfo.Read(fileSystem));

        // La bandeja necesita reactivar la ventana al hacer clic; el clic llega desde el
        // hilo de D-Bus, así que re-entramos al bucle de GTK con IdleAdd.
        MainWindow? window = null;
        var tray = new DBusTrayIcon(logger, () =>
            GLib.Functions.IdleAdd(0, () =>
            {
                window?.Present();
                return false;
            }));

        window = new MainWindow(
            application,
            monitor,
            new AudioAlert(logger),
            new GtkNotifier(application, logger),
            tray,
            new AlertStateMachine(),
            logger);

        window.Present();
    }
    catch (Exception ex)
    {
        logger.Error("No se pudo iniciar la ventana principal.", ex);
        application.Quit();
    }
};

return app.RunWithSynchronizationContext(null);
