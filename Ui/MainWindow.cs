using CoreTempLinux.Sensors;

namespace CoreTempLinux.Ui;

/// <summary>
/// Ventana principal GTK4. Construye los widgets una vez y los actualiza cada
/// segundo con los datos de <see cref="SensorMonitor"/>.
/// </summary>
public sealed class MainWindow
{
    private readonly SensorMonitor _monitor = new();
    private readonly Gtk.Application _app;
    private readonly Gtk.ApplicationWindow _window;

    private readonly Gtk.SpinButton _thresholdSpin = Gtk.SpinButton.NewWithRange(40, 110, 1);
    private readonly Gtk.Box _alertBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);
    private readonly Gtk.Label _alertBanner = Gtk.Label.New(null);
    private readonly Gtk.Button _silenceButton = Gtk.Button.NewWithLabel("🔇 Silenciar");
    private readonly AudioAlert _audio = new();

    private bool _alerting;   // ¿hay una alerta activa ahora mismo?
    private bool _silenced;   // ¿el usuario silenció ESTA alerta? (se resetea al bajar la temp)

    private readonly Gtk.Label _tempValue = Gtk.Label.New(null);
    private readonly Gtk.Label _tempMinMax = Gtk.Label.New(null);
    private readonly Gtk.LevelBar _tempBar = Gtk.LevelBar.New();

    private readonly Gtk.Grid _coreGrid = Gtk.Grid.New();
    private readonly List<(Gtk.Label Freq, Gtk.LevelBar Load, Gtk.Label Pct)> _coreRows = new();

    private readonly Gtk.Box _extraBox = Gtk.Box.New(Gtk.Orientation.Vertical, 4);

    // Mantenemos una referencia al callback para que no lo recoja el GC.
    private readonly GLib.SourceFunc _tick;

    public MainWindow(Gtk.Application app)
    {
        _app = app;
        LoadCss();

        // El icono de la barra de tareas se resuelve por el ID de la aplicación
        // (org.coretemplinux.App) a través del archivo .desktop y el tema de
        // iconos "hicolor". Fijamos también el icono por defecto como respaldo.
        Gtk.Window.SetDefaultIconName("org.coretemplinux.App");

        _window = Gtk.ApplicationWindow.New(app);
        _window.SetTitle("CoreTemp Linux");
        _window.SetIconName("org.coretemplinux.App");
        _window.SetDefaultSize(480, 640);

        var root = Gtk.Box.New(Gtk.Orientation.Vertical, 12);
        root.SetMarginTop(16);
        root.SetMarginBottom(16);
        root.SetMarginStart(16);
        root.SetMarginEnd(16);

        // --- Cabecera ---
        var title = Gtk.Label.New(null);
        title.SetMarkup(
            $"<span size='large' weight='bold'>{Escape(_monitor.Cpu.ModelName)}</span>");
        title.SetHalign(Gtk.Align.Start);
        root.Append(title);

        var sub = Gtk.Label.New($"{_monitor.Cpu.LogicalCores} núcleos lógicos");
        sub.SetHalign(Gtk.Align.Start);
        root.Append(sub);

        root.Append(NewSeparator());

        // --- Temperatura CPU ---
        _tempValue.SetHalign(Gtk.Align.Start);
        root.Append(_tempValue);

        _tempBar.SetMinValue(0);
        _tempBar.SetMaxValue(100);
        root.Append(_tempBar);

        _tempMinMax.SetHalign(Gtk.Align.Start);
        _tempMinMax.AddCssClass("dim-label");
        root.Append(_tempMinMax);

        // --- Alerta configurable ---
        var alertRow = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);
        var alertLabel = Gtk.Label.New("Alertar a partir de (°C):");
        alertLabel.SetHalign(Gtk.Align.Start);
        _thresholdSpin.SetValue(85);
        alertRow.Append(alertLabel);
        alertRow.Append(_thresholdSpin);
        root.Append(alertRow);

        _alertBanner.SetHexpand(true);
        _alertBanner.SetHalign(Gtk.Align.Start);
        _silenceButton.OnClicked += (_, _) =>
        {
            _silenced = true;
            _audio.Stop();
            _silenceButton.SetVisible(false);
        };
        _alertBox.AddCssClass("alert");
        _alertBox.SetVisible(false);
        _alertBox.Append(_alertBanner);
        _alertBox.Append(_silenceButton);
        root.Append(_alertBox);

        root.Append(NewSeparator());

        // --- Núcleos ---
        var coresTitle = Gtk.Label.New(null);
        coresTitle.SetMarkup("<b>Núcleos</b>");
        coresTitle.SetHalign(Gtk.Align.Start);
        root.Append(coresTitle);

        _coreGrid.SetRowSpacing(6);
        _coreGrid.SetColumnSpacing(12);
        BuildCoreRows();
        root.Append(_coreGrid);

        root.Append(NewSeparator());

        // --- Otros sensores ---
        var extraTitle = Gtk.Label.New(null);
        extraTitle.SetMarkup("<b>Otros sensores</b>");
        extraTitle.SetHalign(Gtk.Align.Start);
        root.Append(extraTitle);
        root.Append(_extraBox);

        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(root);
        scroll.SetVexpand(true);
        _window.SetChild(scroll);

        // Primera lectura + refresco periódico (1 s).
        Refresh();
        _tick = () =>
        {
            Refresh();
            return true;
        };
        GLib.Functions.TimeoutAdd(0, 1000, _tick);
    }

    public void Present() => _window.Present();

    private void BuildCoreRows()
    {
        var n = _monitor.CoreCount;
        for (var i = 0; i < n; i++)
        {
            var name = Gtk.Label.New($"Núcleo {i}");
            name.SetHalign(Gtk.Align.Start);

            var freq = Gtk.Label.New("— MHz");
            freq.SetHalign(Gtk.Align.End);
            freq.SetSizeRequest(90, -1);

            var bar = Gtk.LevelBar.New();
            bar.SetMinValue(0);
            bar.SetMaxValue(100);
            bar.SetHexpand(true);
            bar.SetValign(Gtk.Align.Center);

            var pct = Gtk.Label.New("0 %");
            pct.SetHalign(Gtk.Align.End);
            pct.SetSizeRequest(48, -1);

            _coreGrid.Attach(name, 0, i, 1, 1);
            _coreGrid.Attach(freq, 1, i, 1, 1);
            _coreGrid.Attach(bar, 2, i, 1, 1);
            _coreGrid.Attach(pct, 3, i, 1, 1);

            _coreRows.Add((freq, bar, pct));
        }
    }

    private void Refresh()
    {
        var s = _monitor.Collect();

        // Temperatura principal
        if (s.CpuTempC is double t)
        {
            var crit = s.CpuCritC is double c ? $" · Tj.Max {c:F0} °C" : "";
            _tempValue.SetMarkup(
                $"<span size='xx-large' weight='bold'>{t:F1} °C</span>  " +
                $"<span size='small'>{Escape(s.CpuTempLabel)}{crit}</span>");
            _tempBar.SetMaxValue(s.CpuCritC ?? 100);
            _tempBar.SetValue(Math.Min(t, s.CpuCritC ?? 100));
        }
        else
        {
            _tempValue.SetMarkup("<span size='xx-large'>N/D</span>");
        }

        EvaluateAlert(s.CpuTempC);

        _tempMinMax.SetText($"Mín {FmtTemp(s.MinTempC)}    Máx {FmtTemp(s.MaxTempC)}");

        // Núcleos
        for (var i = 0; i < _coreRows.Count; i++)
        {
            var (freq, bar, pct) = _coreRows[i];

            var f = i < s.FreqMhz.Length ? s.FreqMhz[i] : double.NaN;
            var l = i < s.LoadPct.Length ? s.LoadPct[i] : 0;

            freq.SetText(double.IsNaN(f) ? "— MHz" : $"{f:F0} MHz");
            bar.SetValue(l);
            pct.SetText($"{l:F0} %");
        }

        // Otros sensores (se reconstruyen porque el conjunto puede variar)
        ClearBox(_extraBox);
        foreach (var r in s.ExtraSensors)
        {
            var row = Gtk.Label.New($"{r.Chip} · {r.Label}: {FmtValue(r)}");
            row.SetHalign(Gtk.Align.Start);
            _extraBox.Append(row);
        }
    }

    /// <summary>
    /// Gestiona el ciclo de vida de una alerta por "episodios":
    /// <list type="bullet">
    /// <item>Al superar el umbral empieza un episodio: banner, notificación y sonido.</item>
    /// <item>El botón Silenciar corta el sonido solo de ESTE episodio.</item>
    /// <item>Al bajar del umbral el episodio termina y se resetea el silencio.</item>
    /// <item>Si vuelve a superarse, es un episodio nuevo: suena otra vez aunque
    ///       el anterior se hubiera silenciado.</item>
    /// </list>
    /// </summary>
    private void EvaluateAlert(double? temp)
    {
        var threshold = _thresholdSpin.GetValue();
        var over = temp is double t0 && t0 >= threshold;

        if (over)
        {
            var t = (double)temp!;

            if (!_alerting)
            {
                // Comienza un episodio nuevo.
                _alerting = true;
                _silenced = false;
                _silenceButton.SetVisible(true);
                Notify(t, threshold);
            }

            _alertBanner.SetText($"⚠ Temperatura alta: {t:F1} °C (umbral {threshold:F0} °C)");
            _alertBox.SetVisible(true);

            if (!_silenced)
                _audio.Play();
        }
        else if (_alerting)
        {
            // El episodio termina: se limpia todo y se rearma el silencio.
            _alerting = false;
            _silenced = false;
            _audio.Stop();
            _alertBox.SetVisible(false);
        }
    }

    private void Notify(double temp, double threshold)
    {
        var notification = Gio.Notification.New("CoreTemp Linux — alerta de temperatura");
        notification.SetBody($"La CPU alcanzó {temp:F1} °C (umbral {threshold:F0} °C).");
        _app.SendNotification("coretemp-alert", notification);
    }

    private static void LoadCss()
    {
        var provider = Gtk.CssProvider.New();
        provider.LoadFromData(
            ".alert { background-color: #c0392b; color: #ffffff; font-weight: bold; " +
            "padding: 8px; border-radius: 6px; }",
            -1);

        var display = Gdk.Display.GetDefault();
        if (display != null)
            Gtk.StyleContext.AddProviderForDisplay(display, provider, 800);
    }

    private static void ClearBox(Gtk.Box box)
    {
        var child = box.GetFirstChild();
        while (child != null)
        {
            var next = child.GetNextSibling();
            box.Remove(child);
            child = next;
        }
    }

    private static string FmtTemp(double? v) => v is double d ? $"{d:F1} °C" : "—";

    private static string FmtValue(SensorReading r) =>
        r.Kind == SensorKind.Fan ? $"{r.Value:F0} {r.Unit}" : $"{r.Value:F1} {r.Unit}";

    private static Gtk.Separator NewSeparator() =>
        Gtk.Separator.New(Gtk.Orientation.Horizontal);

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
