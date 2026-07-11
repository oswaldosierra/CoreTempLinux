using System.Globalization;
using CoreTempLinux.Alerts;
using CoreTempLinux.Diagnostics;
using CoreTempLinux.Sensors;

namespace CoreTempLinux.Ui;

/// <summary>
/// Ventana principal GTK4 con estética inspirada en Core Temp de Windows: panel de
/// información de CPU, lectura de temperatura grande y coloreada, tabla de sensores con
/// Mín/Máx de sesión y rejilla de frecuencia/carga por núcleo. Construye los widgets una
/// vez y solo actualiza sus valores cada segundo con los datos de <see cref="ISensorMonitor"/>.
/// </summary>
public sealed class MainWindow
{
    // BCLK/bus asumido (Hz→100 MHz) para derivar el multiplicador estilo Core Temp.
    private const double Bclk = 100.0;

    private readonly ISensorMonitor _monitor;
    private readonly IAudioAlert _audio;
    private readonly INotifier _notifier;
    private readonly ITrayIcon _tray;
    private readonly AlertStateMachine _alerts;
    private readonly IAppLogger _log;

    private readonly Gtk.ApplicationWindow _window;

    private readonly Gtk.SpinButton _thresholdSpin = Gtk.SpinButton.NewWithRange(40, 110, 1);
    private readonly Gtk.Box _alertBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);
    private readonly Gtk.Label _alertBanner = Gtk.Label.New(null);
    private readonly Gtk.Button _silenceButton = Gtk.Button.NewWithLabel("🔇 Silenciar");

    private readonly Gtk.Label _freqValue = Gtk.Label.New("—");

    private readonly Gtk.Label _tempValue = Gtk.Label.New("N/D");
    private readonly Gtk.Label _tempLabel = Gtk.Label.New("");
    private readonly Gtk.LevelBar _tempBar = Gtk.LevelBar.New();
    private readonly Gtk.Box _coreTempBox = Gtk.Box.New(Gtk.Orientation.Vertical, 2);

    private readonly Gtk.Grid _coreGrid = Gtk.Grid.New();
    private readonly List<(Gtk.Label Freq, Gtk.LevelBar Load, Gtk.Label Pct)> _coreRows = new();

    private readonly Gtk.Box _extraBox = Gtk.Box.New(Gtk.Orientation.Vertical, 4);

    // Mantenemos una referencia al callback para que no lo recoja el GC.
    private readonly GLib.SourceFunc _tick;

    public MainWindow(
        Gtk.Application app,
        ISensorMonitor monitor,
        IAudioAlert audio,
        INotifier notifier,
        ITrayIcon tray,
        AlertStateMachine alerts,
        IAppLogger log)
    {
        _monitor = monitor;
        _audio = audio;
        _notifier = notifier;
        _tray = tray;
        _alerts = alerts;
        _log = log;

        ForceDarkTheme();
        LoadCss();

        Gtk.Window.SetDefaultIconName("org.coretemplinux.App");

        _window = Gtk.ApplicationWindow.New(app);
        _window.SetTitle("CoreTemp Linux");
        _window.SetIconName("org.coretemplinux.App");
        _window.SetDefaultSize(460, 620);

        var root = Gtk.Box.New(Gtk.Orientation.Vertical, 12);
        root.AddCssClass("ct-root");
        root.SetMarginTop(16);
        root.SetMarginBottom(16);
        root.SetMarginStart(16);
        root.SetMarginEnd(16);

        root.Append(BuildCpuPanel());
        root.Append(NewSeparator());
        root.Append(BuildTempSection());
        root.Append(BuildAlertSection());
        root.Append(NewSeparator());
        root.Append(BuildCoreSection());
        root.Append(NewSeparator());
        root.Append(BuildExtraSection());

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

    // --- Construcción de secciones -------------------------------------------

    private Gtk.Widget BuildCpuPanel()
    {
        var cpu = _monitor.Cpu;

        var panel = Gtk.Box.New(Gtk.Orientation.Vertical, 6);

        // Cabecera: badge del fabricante + modelo.
        var header = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);

        var badgeText = cpu.IsAmd ? "AMD" : cpu.IsIntel ? "INTEL" : "CPU";
        var badge = Gtk.Label.New(badgeText);
        badge.AddCssClass("vendor-badge");
        badge.AddCssClass(cpu.IsAmd ? "vendor-amd" : cpu.IsIntel ? "vendor-intel" : "vendor-generic");
        badge.SetValign(Gtk.Align.Center);
        header.Append(badge);

        var model = Gtk.Label.New(null);
        model.SetMarkup($"<span size='large' weight='bold'>{Escape(cpu.ModelName)}</span>");
        model.SetHalign(Gtk.Align.Start);
        model.SetValign(Gtk.Align.Center);
        header.Append(model);

        panel.Append(header);

        // Filas de información clave:valor.
        var info = Gtk.Grid.New();
        info.SetRowSpacing(3);
        info.SetColumnSpacing(12);

        var row = 0;
        if (!string.IsNullOrEmpty(cpu.Platform))
            AddInfoRow(info, ref row, "Plataforma", cpu.Platform);
        AddInfoRow(info, ref row, "Identificación", CpuIdText(cpu));
        AddInfoRow(info, ref row, "Núcleos", CoreText(cpu));
        AddInfoRow(info, ref row, "Frecuencia", "—", _freqValue);

        panel.Append(info);
        return panel;
    }

    private Gtk.Widget BuildTempSection()
    {
        var section = Gtk.Box.New(Gtk.Orientation.Vertical, 6);

        var readout = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        _tempValue.AddCssClass("ct-temp");
        _tempValue.SetHalign(Gtk.Align.Start);
        _tempValue.SetValign(Gtk.Align.End);
        readout.Append(_tempValue);

        _tempLabel.AddCssClass("dim-label");
        _tempLabel.SetHalign(Gtk.Align.Start);
        _tempLabel.SetValign(Gtk.Align.End);
        _tempLabel.SetMarginBottom(6);
        readout.Append(_tempLabel);
        section.Append(readout);

        _tempBar.SetMinValue(0);
        _tempBar.SetMaxValue(100);
        section.Append(_tempBar);

        // Tabla de temperaturas por sensor con Mín/Máx (rellenada en cada tick).
        section.Append(_coreTempBox);

        return section;
    }

    private Gtk.Widget BuildAlertSection()
    {
        var section = Gtk.Box.New(Gtk.Orientation.Vertical, 6);

        var alertRow = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);
        var alertLabel = Gtk.Label.New("Alertar a partir de (°C):");
        alertLabel.SetHalign(Gtk.Align.Start);
        _thresholdSpin.SetValue(85);
        alertRow.Append(alertLabel);
        alertRow.Append(_thresholdSpin);
        section.Append(alertRow);

        _alertBanner.SetHexpand(true);
        _alertBanner.SetHalign(Gtk.Align.Start);
        _silenceButton.OnClicked += (_, _) =>
        {
            _alerts.Silence();
            _audio.Stop();
            _silenceButton.SetVisible(false);
        };
        _alertBox.AddCssClass("alert");
        _alertBox.SetVisible(false);
        _alertBox.Append(_alertBanner);
        _alertBox.Append(_silenceButton);
        section.Append(_alertBox);

        return section;
    }

    private Gtk.Widget BuildCoreSection()
    {
        var section = Gtk.Box.New(Gtk.Orientation.Vertical, 6);

        var title = Gtk.Label.New(null);
        title.SetMarkup("<b>Frecuencia y carga por núcleo</b>");
        title.SetHalign(Gtk.Align.Start);
        section.Append(title);

        _coreGrid.SetRowSpacing(6);
        _coreGrid.SetColumnSpacing(12);
        BuildCoreRows();
        section.Append(_coreGrid);

        return section;
    }

    private Gtk.Widget BuildExtraSection()
    {
        var section = Gtk.Box.New(Gtk.Orientation.Vertical, 6);

        var title = Gtk.Label.New(null);
        title.SetMarkup("<b>Otros sensores</b>");
        title.SetHalign(Gtk.Align.Start);
        section.Append(title);
        section.Append(_extraBox);

        return section;
    }

    private void BuildCoreRows()
    {
        var n = _monitor.CoreCount;
        for (var i = 0; i < n; i++)
        {
            var name = Gtk.Label.New($"Núcleo {i}");
            name.SetHalign(Gtk.Align.Start);

            var freq = Gtk.Label.New("— MHz");
            freq.AddCssClass("ct-value");
            freq.SetHalign(Gtk.Align.End);
            freq.SetSizeRequest(90, -1);

            var bar = Gtk.LevelBar.New();
            bar.SetMinValue(0);
            bar.SetMaxValue(100);
            bar.SetHexpand(true);
            bar.SetValign(Gtk.Align.Center);

            var pct = Gtk.Label.New("0 %");
            pct.AddCssClass("ct-value");
            pct.SetHalign(Gtk.Align.End);
            pct.SetSizeRequest(48, -1);

            _coreGrid.Attach(name, 0, i, 1, 1);
            _coreGrid.Attach(freq, 1, i, 1, 1);
            _coreGrid.Attach(bar, 2, i, 1, 1);
            _coreGrid.Attach(pct, 3, i, 1, 1);

            _coreRows.Add((freq, bar, pct));
        }
    }

    // --- Refresco ------------------------------------------------------------

    private void Refresh()
    {
        // El refresco corre en un temporizador; un fallo puntual no debe tumbar el
        // ciclo ni la aplicación. Lo registramos y esperamos al siguiente tick.
        try
        {
            RefreshCore();
        }
        catch (Exception ex)
        {
            _log.Error("Fallo al refrescar la interfaz; se reintentará en el próximo ciclo.", ex);
        }
    }

    private void RefreshCore()
    {
        var s = _monitor.Collect();

        // Frecuencia con multiplicador estilo Core Temp.
        _freqValue.SetText(FormatFrequency(s.PackageFreqMhz));

        // Lectura principal de temperatura, coloreada por nivel.
        if (s.CpuTempC is double t)
        {
            var level = TempScale.Classify(t, s.CpuCritC);
            _tempValue.SetText($"{t:F1} °C");
            SetTempClass(_tempValue, level);

            var crit = s.CpuCritC is double c ? $" · Tj.Max {c:F0} °C" : "";
            _tempLabel.SetText($"{s.CpuTempLabel}{crit}");

            _tempBar.SetMaxValue(s.CpuCritC ?? 100);
            _tempBar.SetValue(Math.Min(t, s.CpuCritC ?? 100));
        }
        else
        {
            _tempValue.SetText("N/D");
            SetTempClass(_tempValue, TempLevel.Cool);
            _tempLabel.SetText("");
        }

        EvaluateAlert(s.CpuTempC);

        // Tabla de temperaturas por sensor con Mín/Máx.
        RefreshCoreTemps(s.CoreTempStats, s.MinTempC, s.MaxTempC);

        // Frecuencia y carga por núcleo.
        for (var i = 0; i < _coreRows.Count; i++)
        {
            var (freq, bar, pct) = _coreRows[i];

            var f = i < s.FreqMhz.Length ? s.FreqMhz[i] : double.NaN;
            var l = i < s.LoadPct.Length ? s.LoadPct[i] : 0;

            freq.SetText(double.IsNaN(f) ? "— MHz" : $"{f:F0} MHz");
            bar.SetValue(l);
            pct.SetText($"{l:F0} %");
        }

        // Otros sensores (se reconstruyen porque el conjunto puede variar).
        ClearBox(_extraBox);
        foreach (var r in s.ExtraSensors)
        {
            var rowLabel = Gtk.Label.New($"{r.Chip} · {r.Label}: {FmtValue(r)}");
            rowLabel.SetHalign(Gtk.Align.Start);
            _extraBox.Append(rowLabel);
        }

        // Icono de la bandeja: número de temperatura + tooltip.
        _tray.Update(s.CpuTempC, s.CpuCritC, TrayTooltip(s));
    }

    private void RefreshCoreTemps(IReadOnlyList<CoreTempStat> stats, double? min, double? max)
    {
        ClearBox(_coreTempBox);

        if (stats.Count == 0)
        {
            // Sin lecturas por sensor: mostramos el Mín/Máx global de sesión.
            var line = Gtk.Label.New($"Mín {FmtTemp(min)}    Máx {FmtTemp(max)}");
            line.AddCssClass("dim-label");
            line.SetHalign(Gtk.Align.Start);
            _coreTempBox.Append(line);
            return;
        }

        var grid = Gtk.Grid.New();
        grid.SetColumnSpacing(12);
        grid.SetRowSpacing(2);

        AttachHeader(grid, 0, "Sensor");
        AttachHeader(grid, 1, "Actual");
        AttachHeader(grid, 2, "Mín");
        AttachHeader(grid, 3, "Máx");

        for (var i = 0; i < stats.Count; i++)
        {
            var st = stats[i];
            var r = i + 1;

            var name = Gtk.Label.New(st.Label);
            name.SetHalign(Gtk.Align.Start);

            var cur = Gtk.Label.New($"{st.Current:F1} °C");
            cur.AddCssClass("ct-value");
            SetTempClass(cur, TempScale.Classify(st.Current, st.Critical));
            cur.SetHalign(Gtk.Align.End);

            var lo = Gtk.Label.New($"{st.Min:F1}");
            lo.AddCssClass("ct-value");
            lo.AddCssClass("dim-label");
            lo.SetHalign(Gtk.Align.End);

            var hi = Gtk.Label.New($"{st.Max:F1}");
            hi.AddCssClass("ct-value");
            hi.AddCssClass("dim-label");
            hi.SetHalign(Gtk.Align.End);

            grid.Attach(name, 0, r, 1, 1);
            grid.Attach(cur, 1, r, 1, 1);
            grid.Attach(lo, 2, r, 1, 1);
            grid.Attach(hi, 3, r, 1, 1);
        }

        _coreTempBox.Append(grid);
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

        // La máquina de estados decide la fase; la ventana solo traduce esa fase a
        // banner, sonido y notificación.
        switch (_alerts.Evaluate(temp, threshold))
        {
            case AlertPhase.Started:
                _silenceButton.SetVisible(true);
                _notifier.Notify(
                    "CoreTemp Linux — alerta de temperatura",
                    $"La CPU alcanzó {temp!.Value:F1} °C (umbral {threshold:F0} °C).");
                goto case AlertPhase.Active;

            case AlertPhase.Active:
                _alertBanner.SetText(
                    $"⚠ Temperatura alta: {temp!.Value:F1} °C (umbral {threshold:F0} °C)");
                _alertBox.SetVisible(true);
                if (!_alerts.IsSilenced)
                    _audio.Play();
                break;

            case AlertPhase.Ended:
                _audio.Stop();
                _alertBox.SetVisible(false);
                break;

            case AlertPhase.Idle:
                break;
        }
    }

    // --- Utilidades ----------------------------------------------------------

    private static void ForceDarkTheme()
    {
        var settings = Gtk.Settings.GetDefault();
        if (settings == null)
            return;

        var dark = new GObject.Value(GObject.Type.Boolean);
        dark.SetBoolean(true);
        settings.SetProperty("gtk-application-prefer-dark-theme", dark);
    }

    private static string CpuIdText(CpuInfo cpu)
    {
        if (cpu.Family < 0 || cpu.ModelId < 0)
            return "—";
        return $"Familia {cpu.Family:X}h · Modelo {cpu.ModelId:X}h · Stepping {Math.Max(0, cpu.Stepping)}";
    }

    private static string CoreText(CpuInfo cpu)
    {
        var physical = cpu.PhysicalCores > 0 ? cpu.PhysicalCores : cpu.LogicalCores;
        var text = $"{physical} núcleos · {cpu.LogicalCores} hilos";
        return cpu.Sockets > 1 ? $"{text} · {cpu.Sockets} zócalos" : text;
    }

    private string FormatFrequency(double mhz)
    {
        if (double.IsNaN(mhz))
            return "N/D";

        var mult = mhz / Bclk;
        return string.Create(CultureInfo.InvariantCulture,
            $"{mhz:F1} MHz ({mult:F2} × {Bclk:F1})");
    }

    private string TrayTooltip(Snapshot s)
    {
        if (s.CpuTempC is not double t)
            return $"{_monitor.Cpu.ModelName}\nTemperatura no disponible";

        var crit = s.CpuCritC is double c ? $" (Tj.Max {c:F0} °C)" : "";
        return $"{_monitor.Cpu.ModelName}\n{t:F1} °C{crit}";
    }

    private static void SetTempClass(Gtk.Label label, TempLevel level)
    {
        foreach (var cls in TempScale.AllCssClasses)
            label.RemoveCssClass(cls);
        label.AddCssClass(TempScale.CssClass(level));
    }

    private static void AddInfoRow(Gtk.Grid grid, ref int row, string key, string value,
        Gtk.Label? valueLabel = null)
    {
        var k = Gtk.Label.New(key);
        k.AddCssClass("ct-key");
        k.SetHalign(Gtk.Align.Start);

        var v = valueLabel ?? Gtk.Label.New(value);
        if (valueLabel == null)
            v.SetText(value);
        v.AddCssClass("ct-value");
        v.SetHalign(Gtk.Align.Start);

        grid.Attach(k, 0, row, 1, 1);
        grid.Attach(v, 1, row, 1, 1);
        row++;
    }

    private static void AttachHeader(Gtk.Grid grid, int col, string text)
    {
        var l = Gtk.Label.New(text);
        l.AddCssClass("ct-key");
        l.SetHalign(col == 0 ? Gtk.Align.Start : Gtk.Align.End);
        grid.Attach(l, col, 0, 1, 1);
    }

    private static void LoadCss()
    {
        var provider = Gtk.CssProvider.New();
        provider.LoadFromData(
            ".ct-root { }" +
            ".ct-temp { font-size: 30px; font-weight: bold; }" +
            ".ct-value { font-family: monospace; }" +
            ".ct-key { color: #8a8f98; }" +
            ".vendor-badge { color: #ffffff; font-weight: bold; padding: 2px 10px; border-radius: 4px; }" +
            ".vendor-amd { background-color: #c0392b; }" +
            ".vendor-intel { background-color: #2d6cdf; }" +
            ".vendor-generic { background-color: #555b66; }" +
            ".temp-cool { color: #4cd137; }" +
            ".temp-warm { color: #f1c40f; }" +
            ".temp-hot  { color: #e67e22; }" +
            ".temp-crit { color: #e74c3c; }" +
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
