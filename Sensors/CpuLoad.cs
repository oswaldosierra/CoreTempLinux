namespace CoreTempLinux.Sensors;

/// <summary>
/// Calcula el porcentaje de uso por núcleo a partir de dos muestras de /proc/stat.
/// La primera llamada devuelve ceros (no hay muestra previa con la que comparar).
/// </summary>
public sealed class CpuLoad
{
    private long[] _prevIdle = Array.Empty<long>();
    private long[] _prevTotal = Array.Empty<long>();

    public double[] ReadPercent()
    {
        var idles = new List<long>();
        var totals = new List<long>();

        try
        {
            foreach (var line in File.ReadLines("/proc/stat"))
            {
                if (!line.StartsWith("cpu", StringComparison.Ordinal))
                    break; // Las líneas cpuN van primero; al llegar a "intr" paramos.

                // Nos quedamos solo con las líneas por núcleo ("cpu0", "cpu1", ...),
                // descartando el agregado "cpu " (que tiene un espacio en la posición 3).
                if (line.Length < 4 || !char.IsDigit(line[3]))
                    continue;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                long total = 0, idle = 0;
                // parts: cpuN user nice system idle iowait irq softirq steal guest guest_nice
                for (var i = 1; i < parts.Length; i++)
                {
                    if (!long.TryParse(parts[i], out var v))
                        continue;

                    total += v;
                    if (i == 4 || i == 5) // idle + iowait
                        idle += v;
                }

                idles.Add(idle);
                totals.Add(total);
            }
        }
        catch
        {
            // /proc/stat no disponible: devolvemos lo acumulado hasta ahora.
        }

        var n = totals.Count;
        var result = new double[n];

        if (_prevTotal.Length == n)
        {
            for (var i = 0; i < n; i++)
            {
                var dt = totals[i] - _prevTotal[i];
                var di = idles[i] - _prevIdle[i];
                result[i] = dt > 0 ? Math.Clamp((dt - di) * 100.0 / dt, 0, 100) : 0;
            }
        }

        _prevIdle = idles.ToArray();
        _prevTotal = totals.ToArray();
        return result;
    }
}
