namespace FitFileParser.Models;

/// <summary>Normalized summary of a parsed FIT activity file.</summary>
public sealed class ActivitySummary
{
    public string Sport { get; init; } = string.Empty;
    public string SubSport { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public TimeSpan TotalElapsedTime { get; init; }
    public TimeSpan TotalTimerTime { get; init; }
    public float? TotalDistanceKm { get; init; }
    public int? TotalCalories { get; init; }
    public byte? AvgHeartRate { get; init; }
    public byte? MaxHeartRate { get; init; }

    /// <summary>Average pace per kilometre, derived from average speed.</summary>
    public TimeSpan? AvgPacePerKm { get; init; }
    public float? AvgSpeedKph { get; init; }
    public float? MaxSpeedKph { get; init; }
    public ushort? AvgPower { get; init; }
    public ushort? MaxPower { get; init; }
    public ushort? NormalizedPower { get; init; }
    public float? TotalTrainingEffect { get; init; }
    public float? TotalAnaerobicTrainingEffect { get; init; }
    public ushort? TotalAscent { get; init; }
    public ushort? TotalDescent { get; init; }

    public IReadOnlyList<LapSummary> Laps { get; init; } = [];
}
