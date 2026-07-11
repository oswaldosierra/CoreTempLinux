using System.Globalization;

namespace CoreTempLinux.Sensors;

public enum SensorKind
{
    Temperature,
    Power,
    Fan,
    Frequency,
    Voltage,
}

/// <summary>
/// Una lectura individual de un sensor hwmon.
/// </summary>
/// <param name="Chip">Nombre del driver/chip (p.ej. "k10temp", "amdgpu").</param>
/// <param name="Kind">Tipo de magnitud.</param>
/// <param name="Label">Etiqueta legible (p.ej. "Tctl", "edge").</param>
/// <param name="Value">Valor ya convertido a la unidad de <paramref name="Unit"/>.</param>
/// <param name="Unit">Unidad del valor.</param>
/// <param name="Critical">Valor crítico/máximo si aplica (solo temperatura).</param>
public sealed record SensorReading(
    string Chip,
    SensorKind Kind,
    string Label,
    double Value,
    string Unit,
    double? Critical);

/// <summary>
/// Escaneo genérico de /sys/class/hwmon: temperaturas, potencia, ventiladores,
/// frecuencias y voltajes. Delega todo acceso al disco en <see cref="IFileSystem"/>,
/// que garantiza que ninguna lectura ausente lance excepciones.
/// </summary>
public sealed class HwmonReader : IHwmonReader
{
    private const string Root = "/sys/class/hwmon";

    private readonly IFileSystem _fs;

    public HwmonReader(IFileSystem fs) => _fs = fs;

    public IReadOnlyList<SensorReading> ReadAll()
    {
        var result = new List<SensorReading>();

        foreach (var dir in _fs.GetDirectories(Root).OrderBy(d => d, StringComparer.Ordinal))
        {
            var chip = _fs.ReadText(Path.Combine(dir, "name")) ?? Path.GetFileName(dir);

            foreach (var f in Glob(dir, "temp*_input"))
                Add(result, chip, f, SensorKind.Temperature, "°C", 1000.0, readCrit: true);

            foreach (var f in Glob(dir, "power*_input"))
                Add(result, chip, f, SensorKind.Power, "W", 1_000_000.0, readCrit: false);

            foreach (var f in Glob(dir, "fan*_input"))
                Add(result, chip, f, SensorKind.Fan, "RPM", 1.0, readCrit: false);

            foreach (var f in Glob(dir, "freq*_input"))
                Add(result, chip, f, SensorKind.Frequency, "MHz", 1_000_000.0, readCrit: false);

            foreach (var f in Glob(dir, "in*_input"))
                Add(result, chip, f, SensorKind.Voltage, "V", 1000.0, readCrit: false);
        }

        return result;
    }

    private void Add(List<SensorReading> list, string chip, string inputPath,
        SensorKind kind, string unit, double divisor, bool readCrit)
    {
        if (!TryReadDouble(inputPath, out var raw))
            return;

        var value = raw / divisor;
        var baseName = inputPath[..^"_input".Length];

        var label = _fs.ReadText(baseName + "_label");
        if (string.IsNullOrEmpty(label))
            label = Path.GetFileName(baseName);

        double? crit = null;
        if (readCrit)
        {
            var critPath = _fs.FileExists(baseName + "_crit") ? baseName + "_crit" : baseName + "_max";
            if (TryReadDouble(critPath, out var c))
                crit = c / divisor;
        }

        list.Add(new SensorReading(chip, kind, label, value, unit, crit));
    }

    private IEnumerable<string> Glob(string dir, string pattern) =>
        _fs.GetFiles(dir, pattern).OrderBy(x => x, StringComparer.Ordinal);

    private bool TryReadDouble(string path, out double value)
    {
        value = 0;
        var raw = _fs.ReadText(path);
        return raw != null
            && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
