namespace FitFileParser.Models;

/// <summary>Normalized summary of a single lap within a FIT activity.</summary>
public sealed class LapSummary
{
    public int LapNumber { get; init; }
    public DateTime StartTime { get; init; }
    public TimeSpan TotalElapsedTime { get; init; }
    public TimeSpan TotalTimerTime { get; init; }
    public TimeSpan? TotalMovingTime { get; init; }
    public float? TotalDistanceMiles { get; init; }
    public int? TotalCalories { get; init; }
    public int? TotalFatCalories { get; init; }
    public byte? AvgHeartRate { get; init; }
    public byte? MaxHeartRate { get; init; }
    public byte? AvgCadence { get; init; }
    public byte? MaxCadence { get; init; }
    public byte? AvgRunningCadence { get; init; }
    public byte? MaxRunningCadence { get; init; }
    public sbyte? AvgTemperatureC { get; init; }
    public sbyte? MaxTemperatureC { get; init; }

    /// <summary>Average pace per mile, derived from average speed.</summary>
    public TimeSpan? AvgPacePerMile { get; init; }
    public float? AvgSpeedMph { get; init; }
    public float? MaxSpeedMph { get; init; }
    public ushort? AvgPower { get; init; }
    public ushort? MaxPower { get; init; }
    public ushort? NormalizedPower { get; init; }
    public uint? TotalWorkJoules { get; init; }
    public uint? TotalStrides { get; init; }
    public float? MinAltitudeM { get; init; }
    public float? MaxAltitudeM { get; init; }
    public ushort? TotalAscent { get; init; }
    public ushort? TotalDescent { get; init; }
}
