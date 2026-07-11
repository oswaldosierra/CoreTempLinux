using System.Globalization;

namespace CoreTempLinux.Sensors;

/// <summary>
/// Información estática de la CPU leída una sola vez desde /proc/cpuinfo y cpufreq.
/// </summary>
public sealed class CpuInfo
{
    public string ModelName { get; }
    public int LogicalCores { get; }

    /// <summary>Fabricante (p.ej. "AuthenticAMD", "GenuineIntel"); vacío si se desconoce.</summary>
    public string VendorId { get; }

    /// <summary>Familia/modelo/stepping tal como los da /proc/cpuinfo (decimal, -1 si faltan).</summary>
    public int Family { get; }
    public int ModelId { get; }
    public int Stepping { get; }

    /// <summary>Núcleos físicos por zócalo (0 si se desconoce).</summary>
    public int PhysicalCores { get; }

    /// <summary>Número de zócalos (distintos "physical id"); 1 por defecto.</summary>
    public int Sockets { get; }

    /// <summary>Frecuencia máxima anunciada (MHz), o NaN si cpufreq no la expone.</summary>
    public double MaxFreqMhz { get; }

    /// <summary>Plataforma/placa (best-effort desde DMI), o cadena vacía.</summary>
    public string Platform { get; }

    /// <summary>¿Es una CPU AMD?</summary>
    public bool IsAmd => VendorId.Contains("AMD", StringComparison.OrdinalIgnoreCase);

    /// <summary>¿Es una CPU Intel?</summary>
    public bool IsIntel => VendorId.Contains("Intel", StringComparison.OrdinalIgnoreCase);

    private CpuInfo(
        string model, int cores, string vendor, int family, int modelId, int stepping,
        int physicalCores, int sockets, double maxFreqMhz, string platform)
    {
        ModelName = model;
        LogicalCores = cores;
        VendorId = vendor;
        Family = family;
        ModelId = modelId;
        Stepping = stepping;
        PhysicalCores = physicalCores;
        Sockets = sockets;
        MaxFreqMhz = maxFreqMhz;
        Platform = platform;
    }

    public static CpuInfo Read(IFileSystem fs)
    {
        var model = "CPU desconocida";
        var vendor = "";
        var cores = 0;
        var family = -1;
        var modelId = -1;
        var stepping = -1;
        var physicalCores = 0;
        var sockets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in fs.ReadLines("/proc/cpuinfo"))
        {
            if (StartsWith(line, "model name"))
                model = Field(line) ?? model;
            else if (StartsWith(line, "vendor_id"))
                vendor = Field(line) ?? vendor;
            else if (StartsWith(line, "processor"))
                cores++;
            else if (StartsWith(line, "cpu family"))
                family = ParseInt(Field(line), family);
            else if (StartsWith(line, "model") && !StartsWith(line, "model name"))
                modelId = ParseInt(Field(line), modelId);
            else if (StartsWith(line, "stepping"))
                stepping = ParseInt(Field(line), stepping);
            else if (StartsWith(line, "cpu cores"))
                physicalCores = ParseInt(Field(line), physicalCores);
            else if (StartsWith(line, "physical id"))
            {
                var id = Field(line);
                if (!string.IsNullOrEmpty(id))
                    sockets.Add(id);
            }
        }

        if (cores == 0)
            cores = Environment.ProcessorCount;

        return new CpuInfo(
            model, cores, vendor, family, modelId, stepping,
            physicalCores, Math.Max(1, sockets.Count),
            ReadMaxFreqMhz(fs), ReadPlatform(fs));
    }

    /// <summary>cpufreq expone kHz; preferimos base_frequency y caemos a cpuinfo_max_freq.</summary>
    private static double ReadMaxFreqMhz(IFileSystem fs)
    {
        const string dir = "/sys/devices/system/cpu/cpu0/cpufreq";
        foreach (var name in new[] { "base_frequency", "cpuinfo_max_freq" })
        {
            var txt = fs.ReadText(Path.Combine(dir, name));
            if (txt != null
                && long.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, out var khz)
                && khz > 0)
            {
                return khz / 1000.0;
            }
        }

        return double.NaN;
    }

    /// <summary>Nombre de placa desde DMI (no siempre legible sin privilegios); vacío si no.</summary>
    private static string ReadPlatform(IFileSystem fs) =>
        fs.ReadText("/sys/class/dmi/id/board_name") ?? "";

    private static bool StartsWith(string line, string key) =>
        line.StartsWith(key, StringComparison.Ordinal);

    private static string? Field(string line)
    {
        var idx = line.IndexOf(':');
        return idx >= 0 ? line[(idx + 1)..].Trim() : null;
    }

    private static int ParseInt(string? s, int fallback) =>
        int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
