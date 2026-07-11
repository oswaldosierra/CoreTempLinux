using CoreTempLinux.Ui;

// CoreTempLinux es exclusivamente para Linux: dependemos de /sys y /proc.
if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("CoreTempLinux solo funciona en Linux.");
    return 1;
}

var app = Gtk.Application.New("org.coretemplinux.App", Gio.ApplicationFlags.FlagsNone);

app.OnActivate += (sender, _) =>
{
    try
    {
        var window = new MainWindow((Gtk.Application)sender);
        window.Present();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error al iniciar la ventana: {ex}");
        ((Gtk.Application)sender).Quit();
    }
};

return app.RunWithSynchronizationContext(null);
