using CoreTempLinux.Diagnostics;

namespace CoreTempLinux.Tests.Fakes;

/// <summary>Logger que guarda cada entrada para poder afirmar sobre ellas.</summary>
public sealed class RecordingLogger : IAppLogger
{
    public readonly record struct Entry(LogLevel Level, string Message, Exception? Exception);

    public List<Entry> Entries { get; } = new();

    public void Log(LogLevel level, string message, Exception? exception = null) =>
        Entries.Add(new Entry(level, message, exception));

    public IEnumerable<Entry> OfLevel(LogLevel level) => Entries.Where(e => e.Level == level);
}

/// <summary>
/// Lectores de sensores triviales para componer un <see cref="CoreTempLinux.Sensors.SensorMonitor"/>
/// sin acceder al sistema real.
/// </summary>
public sealed class StubHwmonReader : CoreTempLinux.Sensors.IHwmonReader
{
    private readonly IReadOnlyList<CoreTempLinux.Sensors.SensorReading> _readings;
    public StubHwmonReader(params CoreTempLinux.Sensors.SensorReading[] readings) => _readings = readings;
    public IReadOnlyList<CoreTempLinux.Sensors.SensorReading> ReadAll() => _readings;
}

public sealed class StubFrequencyReader : CoreTempLinux.Sensors.ICpuFrequencyReader
{
    private readonly double[] _mhz;
    public StubFrequencyReader(params double[] mhz) => _mhz = mhz;
    public int CoreCount => _mhz.Length;
    public double[] ReadMhz() => _mhz;
}

public sealed class StubLoadReader : CoreTempLinux.Sensors.ICpuLoadReader
{
    private readonly double[] _pct;
    public StubLoadReader(params double[] pct) => _pct = pct;
    public double[] ReadPercent() => _pct;
}
