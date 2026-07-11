namespace CoreTempLinux.Sensors;

/// <summary>
/// Estadística de sesión de un sensor de temperatura de CPU: valor actual y el
/// mínimo/máximo acumulados desde que arrancó la aplicación.
/// </summary>
public sealed record CoreTempStat(
    string Label,
    double Current,
    double Min,
    double Max,
    double? Critical);

/// <summary>Una lectura completa del sistema en un instante dado.</summary>
public sealed record Snapshot(
    double? CpuTempC,
    string CpuTempLabel,
    double? CpuCritC,
    double? MinTempC,
    double? MaxTempC,
    IReadOnlyList<SensorReading> CpuCoreTemps,
    IReadOnlyList<CoreTempStat> CoreTempStats,
    double PackageFreqMhz,
    double[] FreqMhz,
    double[] LoadPct,
    IReadOnlyList<SensorReading> ExtraSensors);

/// <summary>
/// Orquesta a los distintos lectores, mantiene el mínimo/máximo de la sesión
/// y produce un <see cref="Snapshot"/> en cada llamada a <see cref="Collect"/>.
/// </summary>
public sealed class SensorMonitor : ISensorMonitor
{
    private static readonly string[] CpuChips = { "k10temp", "coretemp", "zenpower" };

    private readonly IHwmonReader _hwmon;
    private readonly ICpuFrequencyReader _freq;
    private readonly ICpuLoadReader _load;

    public CpuInfo Cpu { get; }

    public double? MinTempC { get; private set; }
    public double? MaxTempC { get; private set; }

    // Mín/Máx de sesión por etiqueta de sensor de CPU (p.ej. "Core 0", "Tctl").
    private readonly Dictionary<string, (double Min, double Max)> _coreMinMax = new(StringComparer.Ordinal);

    public int CoreCount => Math.Max(_freq.CoreCount, Cpu.LogicalCores);

    public SensorMonitor(
        IHwmonReader hwmon,
        ICpuFrequencyReader freq,
        ICpuLoadReader load,
        CpuInfo cpu)
    {
        _hwmon = hwmon;
        _freq = freq;
        _load = load;
        Cpu = cpu;
    }

    public Snapshot Collect()
    {
        var hwmon = _hwmon.ReadAll();

        var (cpuTemp, label, crit, coreTemps) = SelectCpuTemp(hwmon);

        if (cpuTemp is double t)
        {
            MinTempC = MinTempC is null ? t : Math.Min(MinTempC.Value, t);
            MaxTempC = MaxTempC is null ? t : Math.Max(MaxTempC.Value, t);
        }

        var extra = hwmon
            .Where(r => !IsCpuChip(r.Chip))
            .ToList();

        var freq = _freq.ReadMhz();

        return new Snapshot(
            cpuTemp, label, crit, MinTempC, MaxTempC,
            coreTemps, BuildCoreStats(coreTemps), PackageFreq(freq),
            freq, _load.ReadPercent(), extra);
    }

    /// <summary>Actualiza el mín/máx de sesión de cada sensor de CPU y lo devuelve.</summary>
    private IReadOnlyList<CoreTempStat> BuildCoreStats(IReadOnlyList<SensorReading> coreTemps)
    {
        var stats = new List<CoreTempStat>(coreTemps.Count);
        foreach (var r in coreTemps)
        {
            var (min, max) = _coreMinMax.TryGetValue(r.Label, out var prev)
                ? (Math.Min(prev.Min, r.Value), Math.Max(prev.Max, r.Value))
                : (r.Value, r.Value);
            _coreMinMax[r.Label] = (min, max);
            stats.Add(new CoreTempStat(r.Label, r.Value, min, max, r.Critical));
        }

        return stats;
    }

    /// <summary>Frecuencia "de paquete": media de los núcleos legibles (ignora NaN); NaN si ninguno.</summary>
    private static double PackageFreq(double[] freq)
    {
        double sum = 0;
        var n = 0;
        foreach (var f in freq)
        {
            if (!double.IsNaN(f))
            {
                sum += f;
                n++;
            }
        }

        return n > 0 ? sum / n : double.NaN;
    }

    private static bool IsCpuChip(string chip) =>
        CpuChips.Contains(chip, StringComparer.OrdinalIgnoreCase);

    private static (double?, string, double?, List<SensorReading>) SelectCpuTemp(
        IReadOnlyList<SensorReading> all)
    {
        var cpuTemps = all
            .Where(r => IsCpuChip(r.Chip) && r.Kind == SensorKind.Temperature)
            .ToList();

        if (cpuTemps.Count == 0)
            return (null, "N/D", null, cpuTemps);

        // Preferimos una etiqueta de paquete/control (Tctl/Tdie/Package);
        // si no existe, tomamos el núcleo más caliente.
        var primary = cpuTemps.FirstOrDefault(r =>
                Contains(r.Label, "Tctl") ||
                Contains(r.Label, "Tdie") ||
                Contains(r.Label, "Package"))
            ?? cpuTemps.MaxBy(r => r.Value)!;

        var crit = primary.Critical
            ?? cpuTemps.FirstOrDefault(r => r.Critical is not null)?.Critical;

        return (primary.Value, primary.Label, crit, cpuTemps);
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
