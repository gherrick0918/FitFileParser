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
            Sport = session.GetSport()?.ToString() ?? string.Empty,
            SubSport = session.GetSubSport()?.ToString() ?? string.Empty,
            StartTime = session.GetStartTime()?.GetDateTime() ?? SysDateTime.MinValue,
            TotalElapsedTime = TimeSpan.FromSeconds(session.GetTotalElapsedTime() ?? 0f),
            TotalTimerTime = TimeSpan.FromSeconds(session.GetTotalTimerTime() ?? 0f),
            TotalMovingTime = session.GetTotalMovingTime().HasValue
                ? TimeSpan.FromSeconds(session.GetTotalMovingTime()!.Value)
                : null,
            TotalDistanceMiles = session.GetTotalDistance().HasValue
                ? MetersToMiles(session.GetTotalDistance()!.Value)
                : null,
            TotalCalories = session.GetTotalCalories().HasValue
                ? (int)session.GetTotalCalories()!.Value
                : null,
            TotalFatCalories = session.GetTotalFatCalories().HasValue
                ? session.GetTotalFatCalories()!.Value
                : null,
            AvgHeartRate = session.GetAvgHeartRate(),
            MaxHeartRate = session.GetMaxHeartRate(),
            AvgCadence = session.GetAvgCadence(),
            MaxCadence = session.GetMaxCadence(),
            AvgRunningCadence = session.GetAvgRunningCadence(),
            MaxRunningCadence = session.GetMaxRunningCadence(),
            AvgTemperatureC = session.GetAvgTemperature(),
            MaxTemperatureC = session.GetMaxTemperature(),
            AvgSpeedMph = SpeedMpsToMph(session.GetEnhancedAvgSpeed() ?? session.GetAvgSpeed()),
            MaxSpeedMph = SpeedMpsToMph(session.GetEnhancedMaxSpeed() ?? session.GetMaxSpeed()),
            AvgPacePerMile = SpeedMpsToPacePerMile(session.GetEnhancedAvgSpeed() ?? session.GetAvgSpeed()),
            AvgPower = session.GetAvgPower(),
            MaxPower = session.GetMaxPower(),
            NormalizedPower = session.GetNormalizedPower(),
            TotalWorkJoules = session.GetTotalWork(),
            TotalStrides = session.GetTotalStrides(),
            TotalTrainingEffect = session.GetTotalTrainingEffect(),
            TotalAnaerobicTrainingEffect = session.GetTotalAnaerobicTrainingEffect(),
            TrainingStressScore = session.GetTrainingStressScore(),
            IntensityFactor = session.GetIntensityFactor(),
            MinAltitudeM = session.GetEnhancedMinAltitude() ?? session.GetMinAltitude(),
            MaxAltitudeM = session.GetEnhancedMaxAltitude() ?? session.GetMaxAltitude(),
            TotalAscent = session.GetTotalAscent(),
            TotalDescent = session.GetTotalDescent(),
            Laps = laps,
        };
    }

    private static LapSummary BuildLapSummary(LapMesg lap, int lapNumber)
    {
        return new LapSummary
        {
            LapNumber = lapNumber,
            StartTime = lap.GetStartTime()?.GetDateTime() ?? SysDateTime.MinValue,
            TotalElapsedTime = TimeSpan.FromSeconds(lap.GetTotalElapsedTime() ?? 0f),
            TotalTimerTime = TimeSpan.FromSeconds(lap.GetTotalTimerTime() ?? 0f),
            TotalMovingTime = lap.GetTotalMovingTime().HasValue
                ? TimeSpan.FromSeconds(lap.GetTotalMovingTime()!.Value)
                : null,
            TotalDistanceMiles = lap.GetTotalDistance().HasValue
                ? MetersToMiles(lap.GetTotalDistance()!.Value)
                : null,
            TotalCalories = lap.GetTotalCalories().HasValue
                ? lap.GetTotalCalories()!.Value
                : null,
            TotalFatCalories = lap.GetTotalFatCalories().HasValue
                ? lap.GetTotalFatCalories()!.Value
                : null,
            AvgHeartRate = lap.GetAvgHeartRate(),
            MaxHeartRate = lap.GetMaxHeartRate(),
            AvgCadence = lap.GetAvgCadence(),
            MaxCadence = lap.GetMaxCadence(),
            AvgRunningCadence = lap.GetAvgRunningCadence(),
            MaxRunningCadence = lap.GetMaxRunningCadence(),
            AvgTemperatureC = lap.GetAvgTemperature(),
            MaxTemperatureC = lap.GetMaxTemperature(),
            AvgSpeedMph = SpeedMpsToMph(lap.GetEnhancedAvgSpeed() ?? lap.GetAvgSpeed()),
            MaxSpeedMph = SpeedMpsToMph(lap.GetEnhancedMaxSpeed() ?? lap.GetMaxSpeed()),
            AvgPacePerMile = SpeedMpsToPacePerMile(lap.GetEnhancedAvgSpeed() ?? lap.GetAvgSpeed()),
            AvgPower = lap.GetAvgPower(),
            MaxPower = lap.GetMaxPower(),
            NormalizedPower = lap.GetNormalizedPower(),
            TotalWorkJoules = lap.GetTotalWork(),
            TotalStrides = lap.GetTotalStrides(),
            MinAltitudeM = lap.GetEnhancedMinAltitude() ?? lap.GetMinAltitude(),
            MaxAltitudeM = lap.GetEnhancedMaxAltitude() ?? lap.GetMaxAltitude(),
            TotalAscent = lap.GetTotalAscent(),
            TotalDescent = lap.GetTotalDescent(),
        };
    }

    private static float MetersToMiles(float meters) => meters / MetersPerMile;

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
