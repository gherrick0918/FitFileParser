using Dynastream.Fit;
using FitFileParser.Models;
using FitDateTime = Dynastream.Fit.DateTime;
using SysDateTime = System.DateTime;

namespace FitFileParser.Parsing;

/// <summary>
/// Decodes a FIT file stream into a normalized <see cref="ActivitySummary"/> using the
/// Garmin FIT SDK's event-driven broadcaster model.
/// </summary>
public sealed class FitParser
{
    private const float MetersPerMile = 1609.344f;
    private const float MetersPerFoot = 0.3048f;
    private const float MmPerInch = 25.4f;
    private const float MetersPerYard = 0.9144f;

    /// <summary>
    /// Parses the provided stream as a FIT activity file.
    /// </summary>
    /// <param name="stream">A readable, seekable stream containing FIT binary data.</param>
    /// <returns>A normalized <see cref="ActivitySummary"/>.</returns>
    /// <exception cref="FitParseException">
    /// Thrown when the stream is not a valid FIT file, fails integrity checks, or contains
    /// no recognizable session data.
    /// </exception>
    public ActivitySummary Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
            throw new ArgumentException("Stream must be readable and seekable.", nameof(stream));

        ValidateStream(stream);

        var decoder = new Decode();
        var broadcaster = new MesgBroadcaster();

        decoder.MesgEvent += broadcaster.OnMesg;
        decoder.MesgDefinitionEvent += broadcaster.OnMesgDefinition;

        SessionMesg? sessionMesg = null;
        var lapMesgs = new List<LapMesg>();

        broadcaster.SessionMesgEvent += (_, e) => sessionMesg = e.mesg as SessionMesg;
        broadcaster.LapMesgEvent += (_, e) => { if (e.mesg is LapMesg lap) lapMesgs.Add(lap); };

        stream.Position = 0;
        try
        {
            decoder.Read(stream);
        }
        catch (FitException ex)
        {
            throw new FitParseException("The FIT file could not be decoded.", ex);
        }

        if (sessionMesg is null)
            throw new FitParseException("No session message found in the FIT file.");

        return BuildActivitySummary(sessionMesg, lapMesgs);
    }

    private static void ValidateStream(Stream stream)
    {
        var decoder = new Decode();

        stream.Position = 0;
        if (!decoder.IsFIT(stream))
            throw new FitParseException("The file does not appear to be a valid FIT file.");

        stream.Position = 0;
        if (!decoder.CheckIntegrity(stream))
            throw new FitParseException("The FIT file failed the integrity check (header or CRC mismatch).");
    }

    private static ActivitySummary BuildActivitySummary(SessionMesg session, List<LapMesg> lapMesgs)
    {
        var laps = lapMesgs
            .Select((lap, index) => BuildLapSummary(lap, index + 1))
            .ToList();

        return new ActivitySummary
        {
            // Identity
            Sport = session.GetSport()?.ToString() ?? string.Empty,
            SubSport = session.GetSubSport()?.ToString() ?? string.Empty,
            SportProfileName = session.GetSportProfileNameAsString()
                is string s && !string.IsNullOrWhiteSpace(s) ? s : null,
            StartTime = session.GetStartTime()?.GetDateTime() ?? SysDateTime.MinValue,
            NumLaps = session.GetNumLaps(),

            // Time
            TotalElapsedTime = TimeSpan.FromSeconds(session.GetTotalElapsedTime() ?? 0f),
            TotalTimerTime = TimeSpan.FromSeconds(session.GetTotalTimerTime() ?? 0f),
            TotalMovingTime = session.GetTotalMovingTime().HasValue
                ? TimeSpan.FromSeconds(session.GetTotalMovingTime()!.Value) : null,
            ActiveTime = session.GetActiveTime().HasValue
                ? TimeSpan.FromSeconds(session.GetActiveTime()!.Value) : null,

            // Distance / Speed / Pace
            TotalDistanceMiles = session.GetTotalDistance().HasValue
                ? MetersToMiles(session.GetTotalDistance()!.Value) : null,
            AvgSpeedMph = SpeedMpsToMph(session.GetEnhancedAvgSpeed() ?? session.GetAvgSpeed()),
            MaxSpeedMph = SpeedMpsToMph(session.GetEnhancedMaxSpeed() ?? session.GetMaxSpeed()),
            AvgPacePerMile = SpeedMpsToPacePerMile(session.GetEnhancedAvgSpeed() ?? session.GetAvgSpeed()),

            // Heart Rate
            MinHeartRate = session.GetMinHeartRate(),
            AvgHeartRate = session.GetAvgHeartRate(),
            MaxHeartRate = session.GetMaxHeartRate(),

            // Cadence
            AvgCadence = session.GetAvgCadence(),
            MaxCadence = session.GetMaxCadence(),
            AvgRunningCadence = session.GetAvgRunningCadence(),
            MaxRunningCadence = session.GetMaxRunningCadence(),

            // Calories
            TotalCalories = session.GetTotalCalories().HasValue
                ? (int)session.GetTotalCalories()!.Value : null,
            TotalFatCalories = session.GetTotalFatCalories().HasValue
                ? session.GetTotalFatCalories()!.Value : null,

            // Power
            AvgPower = session.GetAvgPower(),
            MaxPower = session.GetMaxPower(),
            NormalizedPower = session.GetNormalizedPower(),
            ThresholdPower = session.GetThresholdPower(),
            TotalWorkJoules = session.GetTotalWork(),

            // Training Load
            TotalTrainingEffect = session.GetTotalTrainingEffect(),
            TotalAnaerobicTrainingEffect = session.GetTotalAnaerobicTrainingEffect(),
            TrainingStressScore = session.GetTrainingStressScore(),
            IntensityFactor = session.GetIntensityFactor(),
            TrainingLoadPeak = session.GetTrainingLoadPeak(),

            // Elevation (imperial)
            TotalAscentFt = UshortMetersToFeet(session.GetTotalAscent()),
            TotalDescentFt = UshortMetersToFeet(session.GetTotalDescent()),
            MinAltitudeFt = MetersToFeet(session.GetEnhancedMinAltitude() ?? session.GetMinAltitude()),
            MaxAltitudeFt = MetersToFeet(session.GetEnhancedMaxAltitude() ?? session.GetMaxAltitude()),

            // Temperature (imperial)
            MinTemperatureF = SbyteToFahrenheit(session.GetMinTemperature()),
            AvgTemperatureF = SbyteToFahrenheit(session.GetAvgTemperature()),
            MaxTemperatureF = SbyteToFahrenheit(session.GetMaxTemperature()),

            // Running Form
            TotalStrides = session.GetTotalStrides(),
            AvgVerticalOscillationIn = session.GetAvgVerticalOscillation().HasValue
                ? session.GetAvgVerticalOscillation()!.Value / MmPerInch : null,
            AvgStanceTimeMs = session.GetAvgStanceTime(),
            AvgStanceTimePercent = session.GetAvgStanceTimePercent(),
            AvgVerticalRatio = session.GetAvgVerticalRatio(),
            AvgStepLengthFt = session.GetAvgStepLength().HasValue
                ? session.GetAvgStepLength()!.Value / (MmPerInch * 12f) : null,
            AvgGrade = session.GetAvgGrade(),
            MaxPosGrade = session.GetMaxPosGrade(),
            MaxNegGrade = session.GetMaxNegGrade(),

            // Cycling
            LeftRightBalance = session.GetLeftRightBalance(),
            AvgLeftTorqueEffectiveness = session.GetAvgLeftTorqueEffectiveness(),
            AvgRightTorqueEffectiveness = session.GetAvgRightTorqueEffectiveness(),
            AvgLeftPedalSmoothness = session.GetAvgLeftPedalSmoothness(),
            AvgRightPedalSmoothness = session.GetAvgRightPedalSmoothness(),
            AvgCombinedPedalSmoothness = session.GetAvgCombinedPedalSmoothness(),

            // Swimming
            SwimStroke = session.GetSwimStroke()?.ToString(),
            PoolLengthYards = session.GetPoolLength().HasValue
                ? session.GetPoolLength()!.Value / MetersPerYard : null,
            TotalStrokes = session.GetTotalStrokes(),
            TotalCycles = session.GetTotalCycles(),
            AvgStrokeDistanceYards = session.GetAvgStrokeDistance().HasValue
                ? session.GetAvgStrokeDistance()!.Value / MetersPerYard : null,

            // Physiology / Wellness
            AvgRespirationRate = session.GetEnhancedAvgRespirationRate() ?? session.GetAvgRespirationRate(),
            AvgSpo2 = session.GetAvgSpo2(),
            RmssdHrv = session.GetRmssdHrv(),

            // Workout Feedback
            WorkoutFeel = session.GetWorkoutFeel(),
            WorkoutRpe = session.GetWorkoutRpe(),

            Laps = laps,
        };
    }

    private static LapSummary BuildLapSummary(LapMesg lap, int lapNumber)
    {
        return new LapSummary
        {
            // Identity
            LapNumber = lapNumber,
            StartTime = lap.GetStartTime()?.GetDateTime() ?? SysDateTime.MinValue,

            // Time
            TotalElapsedTime = TimeSpan.FromSeconds(lap.GetTotalElapsedTime() ?? 0f),
            TotalTimerTime = TimeSpan.FromSeconds(lap.GetTotalTimerTime() ?? 0f),
            TotalMovingTime = lap.GetTotalMovingTime().HasValue
                ? TimeSpan.FromSeconds(lap.GetTotalMovingTime()!.Value) : null,

            // Distance / Speed / Pace
            TotalDistanceMiles = lap.GetTotalDistance().HasValue
                ? MetersToMiles(lap.GetTotalDistance()!.Value) : null,
            AvgSpeedMph = SpeedMpsToMph(lap.GetEnhancedAvgSpeed() ?? lap.GetAvgSpeed()),
            MaxSpeedMph = SpeedMpsToMph(lap.GetEnhancedMaxSpeed() ?? lap.GetMaxSpeed()),
            AvgPacePerMile = SpeedMpsToPacePerMile(lap.GetEnhancedAvgSpeed() ?? lap.GetAvgSpeed()),

            // Heart Rate
            MinHeartRate = lap.GetMinHeartRate(),
            AvgHeartRate = lap.GetAvgHeartRate(),
            MaxHeartRate = lap.GetMaxHeartRate(),

            // Cadence
            AvgCadence = lap.GetAvgCadence(),
            MaxCadence = lap.GetMaxCadence(),
            AvgRunningCadence = lap.GetAvgRunningCadence(),
            MaxRunningCadence = lap.GetMaxRunningCadence(),

            // Calories
            TotalCalories = lap.GetTotalCalories().HasValue
                ? lap.GetTotalCalories()!.Value : null,
            TotalFatCalories = lap.GetTotalFatCalories().HasValue
                ? lap.GetTotalFatCalories()!.Value : null,

            // Power
            AvgPower = lap.GetAvgPower(),
            MaxPower = lap.GetMaxPower(),
            NormalizedPower = lap.GetNormalizedPower(),
            TotalWorkJoules = lap.GetTotalWork(),

            // Elevation (imperial)
            TotalAscentFt = UshortMetersToFeet(lap.GetTotalAscent()),
            TotalDescentFt = UshortMetersToFeet(lap.GetTotalDescent()),
            MinAltitudeFt = MetersToFeet(lap.GetEnhancedMinAltitude() ?? lap.GetMinAltitude()),
            MaxAltitudeFt = MetersToFeet(lap.GetEnhancedMaxAltitude() ?? lap.GetMaxAltitude()),

            // Temperature (imperial)
            MinTemperatureF = SbyteToFahrenheit(lap.GetMinTemperature()),
            AvgTemperatureF = SbyteToFahrenheit(lap.GetAvgTemperature()),
            MaxTemperatureF = SbyteToFahrenheit(lap.GetMaxTemperature()),

            // Running Form
            TotalStrides = lap.GetTotalStrides(),
            AvgVerticalOscillationIn = lap.GetAvgVerticalOscillation().HasValue
                ? lap.GetAvgVerticalOscillation()!.Value / MmPerInch : null,
            AvgStanceTimeMs = lap.GetAvgStanceTime(),
            AvgStanceTimePercent = lap.GetAvgStanceTimePercent(),
            AvgVerticalRatio = lap.GetAvgVerticalRatio(),
            AvgStepLengthFt = lap.GetAvgStepLength().HasValue
                ? lap.GetAvgStepLength()!.Value / (MmPerInch * 12f) : null,
            AvgGrade = lap.GetAvgGrade(),
            MaxPosGrade = lap.GetMaxPosGrade(),
            MaxNegGrade = lap.GetMaxNegGrade(),

            // Cycling
            LeftRightBalance = lap.GetLeftRightBalance(),
            AvgLeftTorqueEffectiveness = lap.GetAvgLeftTorqueEffectiveness(),
            AvgRightTorqueEffectiveness = lap.GetAvgRightTorqueEffectiveness(),
            AvgLeftPedalSmoothness = lap.GetAvgLeftPedalSmoothness(),
            AvgRightPedalSmoothness = lap.GetAvgRightPedalSmoothness(),
            AvgCombinedPedalSmoothness = lap.GetAvgCombinedPedalSmoothness(),

            // Swimming
            TotalStrokes = lap.GetTotalStrokes(),
            TotalCycles = lap.GetTotalCycles(),
            AvgStrokeDistanceYards = lap.GetAvgStrokeDistance().HasValue
                ? lap.GetAvgStrokeDistance()!.Value / MetersPerYard : null,

            // Physiology
            AvgRespirationRate = lap.GetEnhancedAvgRespirationRate() ?? lap.GetAvgRespirationRate(),
        };
    }

    // ── Unit conversion helpers ───────────────────────────────────────────

    private static float MetersToMiles(float meters) => meters / MetersPerMile;

    private static float? MetersToFeet(float? meters) =>
        meters.HasValue ? meters.Value / MetersPerFoot : null;

    private static float? UshortMetersToFeet(ushort? meters) =>
        meters.HasValue ? meters.Value / MetersPerFoot : null;

    private static float? SbyteToFahrenheit(sbyte? celsius) =>
        celsius.HasValue ? celsius.Value * 9f / 5f + 32f : null;

    private static float? SpeedMpsToMph(float? speedMps)
    {
        if (speedMps is null or 0f) return null;
        return speedMps.Value * 2.23693629f;
    }

    private static TimeSpan? SpeedMpsToPacePerMile(float? speedMps)
    {
        if (speedMps is null or <= 0f) return null;
        var secondsPerMile = MetersPerMile / speedMps.Value;
        return TimeSpan.FromSeconds(secondsPerMile);
    }
}
