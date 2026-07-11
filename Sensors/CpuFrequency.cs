using System.Globalization;

namespace CoreTempLinux.Sensors;

/// <summary>
/// Lee la frecuencia actual de cada núcleo lógico desde cpufreq (kHz → MHz).
/// </summary>
public sealed class CpuFrequency
{
    private readonly string[] _paths;

    public int CoreCount => _paths.Length;

    public CpuFrequency()
    {
        var list = new List<string>();
        for (var i = 0; ; i++)
        {
            var dir = $"/sys/devices/system/cpu/cpu{i}";
            if (!Directory.Exists(dir))
                break;

            list.Add(Path.Combine(dir, "cpufreq", "scaling_cur_freq"));
        }

        _paths = list.ToArray();
    }

    /// <summary>MHz por núcleo; NaN si un núcleo no es legible.</summary>
    public double[] ReadMhz()
    {
        var result = new double[_paths.Length];

        for (var i = 0; i < _paths.Length; i++)
        {
            result[i] = double.NaN;
            try
            {
                var txt = File.ReadAllText(_paths[i]).Trim();
                if (long.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, out var khz))
                    result[i] = khz / 1000.0;
            }
            catch
            {
                // Núcleo apagado o sin cpufreq: se queda en NaN.
            }
        }

        return result;
    }
}
