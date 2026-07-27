namespace FitFileParser.Models;

/// <summary>Normalized summary of a single lap within a FIT activity.</summary>
public sealed class LapSummary
{
    public int LapNumber { get; init; }
    public DateTime StartTime { get; init; }
    public TimeSpan TotalElapsedTime { get; init; }
    public TimeSpan TotalTimerTime { get; init; }
    public float? TotalDistanceKm { get; init; }
    public byte? AvgHeartRate { get; init; }
    public byte? MaxHeartRate { get; init; }

    /// <summary>Average pace per kilometre, derived from average speed.</summary>
    public TimeSpan? AvgPacePerKm { get; init; }
    public float? AvgSpeedKph { get; init; }
    public ushort? AvgPower { get; init; }
    public ushort? MaxPower { get; init; }
    public ushort? NormalizedPower { get; init; }
    public ushort? TotalAscent { get; init; }
    public ushort? TotalDescent { get; init; }
}
