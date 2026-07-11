namespace CoreTempLinux.Sensors;

/// <summary>Una lectura completa del sistema en un instante dado.</summary>
public sealed record Snapshot(
    double? CpuTempC,
    string CpuTempLabel,
    double? CpuCritC,
    double? MinTempC,
    double? MaxTempC,
    IReadOnlyList<SensorReading> CpuCoreTemps,
    double[] FreqMhz,
    double[] LoadPct,
    IReadOnlyList<SensorReading> ExtraSensors);

/// <summary>
/// Orquesta a los distintos lectores, mantiene el mínimo/máximo de la sesión
/// y produce un <see cref="Snapshot"/> en cada llamada a <see cref="Collect"/>.
/// </summary>
public sealed class SensorMonitor
{
    private static readonly string[] CpuChips = { "k10temp", "coretemp", "zenpower" };

    private readonly CpuFrequency _freq = new();
    private readonly CpuLoad _load = new();

    public CpuInfo Cpu { get; } = CpuInfo.Read();

    public double? MinTempC { get; private set; }
    public double? MaxTempC { get; private set; }

    public int CoreCount => Math.Max(_freq.CoreCount, Cpu.LogicalCores);

    public Snapshot Collect()
    {
        var hwmon = HwmonReader.ReadAll();

        var (cpuTemp, label, crit, coreTemps) = SelectCpuTemp(hwmon);

        if (cpuTemp is double t)
        {
            MinTempC = MinTempC is null ? t : Math.Min(MinTempC.Value, t);
            MaxTempC = MaxTempC is null ? t : Math.Max(MaxTempC.Value, t);
        }

        var extra = hwmon
            .Where(r => !IsCpuChip(r.Chip))
            .ToList();

        return new Snapshot(
            cpuTemp, label, crit, MinTempC, MaxTempC,
            coreTemps, _freq.ReadMhz(), _load.ReadPercent(), extra);
    }

    private static bool IsCpuChip(string chip) =>
        CpuChips.Contains(chip, StringComparer.OrdinalIgnoreCase);

    private static (double?, string, double?, List<SensorReading>) SelectCpuTemp(
        List<SensorReading> all)
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
