namespace CoreTempLinux.Sensors;

/// <summary>
/// Información estática de la CPU leída una sola vez desde /proc/cpuinfo.
/// </summary>
public sealed class CpuInfo
{
    public string ModelName { get; }
    public int LogicalCores { get; }

    private CpuInfo(string model, int cores)
    {
        ModelName = model;
        LogicalCores = cores;
    }

    public static CpuInfo Read()
    {
        var model = "CPU desconocida";
        var cores = 0;

        try
        {
            foreach (var line in File.ReadLines("/proc/cpuinfo"))
            {
                if (line.StartsWith("model name", StringComparison.Ordinal))
                {
                    var idx = line.IndexOf(':');
                    if (idx >= 0)
                        model = line[(idx + 1)..].Trim();
                }
                else if (line.StartsWith("processor", StringComparison.Ordinal))
                {
                    cores++;
                }
            }
        }
        catch
        {
            // Si /proc/cpuinfo no está disponible caemos al valor por defecto.
        }

        if (cores == 0)
            cores = Environment.ProcessorCount;

        return new CpuInfo(model, cores);
    }
}
