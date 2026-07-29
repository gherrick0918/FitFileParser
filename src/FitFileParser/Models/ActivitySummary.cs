namespace FitFileParser.Models;

/// <summary>Normalized summary of a parsed FIT activity file.</summary>
public sealed class ActivitySummary
{
    // ── Identity ──────────────────────────────────────────────────────────
    public string Sport { get; init; } = string.Empty;
    public string SubSport { get; init; } = string.Empty;
    public string? SportProfileName { get; init; }
    public DateTime StartTime { get; init; }
    public ushort? NumLaps { get; init; }

    // ── Time ──────────────────────────────────────────────────────────────
    public TimeSpan TotalElapsedTime { get; init; }
    public TimeSpan TotalTimerTime { get; init; }
    public TimeSpan? TotalMovingTime { get; init; }
    public TimeSpan? ActiveTime { get; init; }

    // ── Distance / Speed / Pace ───────────────────────────────────────────
    public float? TotalDistanceMiles { get; init; }
    /// <summary>Average pace per mile, derived from average speed.</summary>
    public TimeSpan? AvgPacePerMile { get; init; }
    public float? AvgSpeedMph { get; init; }
    public float? MaxSpeedMph { get; init; }

    // ── Heart Rate ────────────────────────────────────────────────────────
    public byte? MinHeartRate { get; init; }
    public byte? AvgHeartRate { get; init; }
    public byte? MaxHeartRate { get; init; }

    // ── Cadence ───────────────────────────────────────────────────────────
    public byte? AvgCadence { get; init; }
    public byte? MaxCadence { get; init; }
    public byte? AvgRunningCadence { get; init; }
    public byte? MaxRunningCadence { get; init; }

    // ── Calories / Nutrition ──────────────────────────────────────────────
    public int? TotalCalories { get; init; }
    public int? TotalFatCalories { get; init; }

    // ── Power ─────────────────────────────────────────────────────────────
    public ushort? AvgPower { get; init; }
    public ushort? MaxPower { get; init; }
    public ushort? NormalizedPower { get; init; }
    public ushort? ThresholdPower { get; init; }
    public uint? TotalWorkJoules { get; init; }

    // ── Training Load ─────────────────────────────────────────────────────
    public float? TotalTrainingEffect { get; init; }
    public float? TotalAnaerobicTrainingEffect { get; init; }
    public float? TrainingStressScore { get; init; }
    public float? IntensityFactor { get; init; }
    public float? TrainingLoadPeak { get; init; }

    // ── Elevation (imperial) ──────────────────────────────────────────────
    public float? TotalAscentFt { get; init; }
    public float? TotalDescentFt { get; init; }
    public float? MinAltitudeFt { get; init; }
    public float? MaxAltitudeFt { get; init; }

    // ── Temperature (imperial) ────────────────────────────────────────────
    public float? MinTemperatureF { get; init; }
    public float? AvgTemperatureF { get; init; }
    public float? MaxTemperatureF { get; init; }

    // ── Running Form ──────────────────────────────────────────────────────
    public uint? TotalStrides { get; init; }
    /// <summary>Average vertical oscillation in inches.</summary>
    public float? AvgVerticalOscillationIn { get; init; }
    /// <summary>Average ground contact time in milliseconds.</summary>
    public float? AvgStanceTimeMs { get; init; }
    /// <summary>Average ground contact time as a percentage of stride.</summary>
    public float? AvgStanceTimePercent { get; init; }
    /// <summary>Average vertical ratio (vertical oscillation / stride length) as a percentage.</summary>
    public float? AvgVerticalRatio { get; init; }
    /// <summary>Average step length in feet.</summary>
    public float? AvgStepLengthFt { get; init; }
    public float? AvgGrade { get; init; }
    public float? MaxPosGrade { get; init; }
    public float? MaxNegGrade { get; init; }

    // ── Cycling ───────────────────────────────────────────────────────────
    /// <summary>Left-right power balance raw value (bit 15 = right dominant, bits 14:0 = pct×100).</summary>
    public ushort? LeftRightBalance { get; init; }
    public float? AvgLeftTorqueEffectiveness { get; init; }
    public float? AvgRightTorqueEffectiveness { get; init; }
    public float? AvgLeftPedalSmoothness { get; init; }
    public float? AvgRightPedalSmoothness { get; init; }
    public float? AvgCombinedPedalSmoothness { get; init; }

    // ── Swimming ──────────────────────────────────────────────────────────
    public string? SwimStroke { get; init; }
    /// <summary>Pool length in yards.</summary>
    public float? PoolLengthYards { get; init; }
    public uint? TotalStrokes { get; init; }
    public uint? TotalCycles { get; init; }
    /// <summary>Average distance per stroke in yards.</summary>
    public float? AvgStrokeDistanceYards { get; init; }

    // ── Physiology / Wellness ─────────────────────────────────────────────
    public float? AvgRespirationRate { get; init; }
    public float? AvgSpo2 { get; init; }
    /// <summary>HRV – RMSSD in milliseconds.</summary>
    public float? RmssdHrv { get; init; }

    // ── Workout Feedback ──────────────────────────────────────────────────
    public byte? WorkoutFeel { get; init; }
    /// <summary>Workout RPE on the Borg scale (6–20).</summary>
    public byte? WorkoutRpe { get; init; }

    // ── Notes / Comments ──────────────────────────────────────────────────
    /// <summary>User-entered notes or comments about the activity.</summary>
    public string? Notes { get; init; }

    public IReadOnlyList<LapSummary> Laps { get; init; } = [];
}
