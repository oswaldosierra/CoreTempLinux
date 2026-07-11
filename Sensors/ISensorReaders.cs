namespace CoreTempLinux.Sensors;

/// <summary>Escanea los chips hwmon y devuelve todas sus lecturas.</summary>
public interface IHwmonReader
{
    IReadOnlyList<SensorReading> ReadAll();
}

/// <summary>Frecuencia actual por núcleo lógico (MHz).</summary>
public interface ICpuFrequencyReader
{
    int CoreCount { get; }

    /// <summary>MHz por núcleo; <see cref="double.NaN"/> si un núcleo no es legible.</summary>
    double[] ReadMhz();
}

/// <summary>Uso porcentual por núcleo lógico.</summary>
public interface ICpuLoadReader
{
    double[] ReadPercent();
}

/// <summary>
/// Fuente única de verdad para la UI: combina los lectores en un
/// <see cref="Snapshot"/> por cada llamada a <see cref="Collect"/>.
/// </summary>
public interface ISensorMonitor
{
    CpuInfo Cpu { get; }
    int CoreCount { get; }
    double? MinTempC { get; }
    double? MaxTempC { get; }

    Snapshot Collect();
}
