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
/// Escaneo genérico de /sys/class/hwmon: temperaturas, potencia, ventiladores y frecuencias.
/// Generaliza la lógica que originalmente vivía en Program.cs.
/// </summary>
public static class HwmonReader
{
    private const string Root = "/sys/class/hwmon";

    public static List<SensorReading> ReadAll()
    {
        var result = new List<SensorReading>();

        string[] dirs;
        try
        {
            dirs = Directory.GetDirectories(Root);
        }
        catch
        {
            return result;
        }

        foreach (var dir in dirs.OrderBy(d => d, StringComparer.Ordinal))
        {
            var chip = TryRead(Path.Combine(dir, "name")) ?? Path.GetFileName(dir);

            foreach (var f in SafeGlob(dir, "temp*_input"))
                Add(result, chip, f, SensorKind.Temperature, "°C", 1000.0, readCrit: true);

            foreach (var f in SafeGlob(dir, "power*_input"))
                Add(result, chip, f, SensorKind.Power, "W", 1_000_000.0, readCrit: false);

            foreach (var f in SafeGlob(dir, "fan*_input"))
                Add(result, chip, f, SensorKind.Fan, "RPM", 1.0, readCrit: false);

            foreach (var f in SafeGlob(dir, "freq*_input"))
                Add(result, chip, f, SensorKind.Frequency, "MHz", 1_000_000.0, readCrit: false);

            foreach (var f in SafeGlob(dir, "in*_input"))
                Add(result, chip, f, SensorKind.Voltage, "V", 1000.0, readCrit: false);
        }

        return result;
    }

    private static void Add(List<SensorReading> list, string chip, string inputPath,
        SensorKind kind, string unit, double divisor, bool readCrit)
    {
        if (!TryReadDouble(inputPath, out var raw))
            return;

        var value = raw / divisor;
        var baseName = inputPath[..^"_input".Length];

        var label = TryRead(baseName + "_label");
        if (string.IsNullOrEmpty(label))
            label = Path.GetFileName(baseName);

        double? crit = null;
        if (readCrit)
        {
            var critPath = File.Exists(baseName + "_crit") ? baseName + "_crit" : baseName + "_max";
            if (TryReadDouble(critPath, out var c))
                crit = c / divisor;
        }

        list.Add(new SensorReading(chip, kind, label, value, unit, crit));
    }

    private static IEnumerable<string> SafeGlob(string dir, string pattern)
    {
        try
        {
            return Directory.GetFiles(dir, pattern).OrderBy(x => x, StringComparer.Ordinal);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryReadDouble(string path, out double value)
    {
        value = 0;
        var raw = TryRead(path);
        return raw != null
            && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
