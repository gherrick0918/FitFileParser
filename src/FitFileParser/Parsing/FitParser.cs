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
            TotalDistanceKm = session.GetTotalDistance().HasValue
                ? session.GetTotalDistance()!.Value / 1000f
                : null,
            TotalCalories = session.GetTotalCalories().HasValue
                ? (int)session.GetTotalCalories()!.Value
                : null,
            AvgHeartRate = session.GetAvgHeartRate(),
            MaxHeartRate = session.GetMaxHeartRate(),
            AvgSpeedKph = SpeedMpsToKph(session.GetEnhancedAvgSpeed() ?? session.GetAvgSpeed()),
            MaxSpeedKph = SpeedMpsToKph(session.GetEnhancedMaxSpeed() ?? session.GetMaxSpeed()),
            AvgPacePerKm = SpeedMpsToPace(session.GetEnhancedAvgSpeed() ?? session.GetAvgSpeed()),
            AvgPower = session.GetAvgPower(),
            MaxPower = session.GetMaxPower(),
            NormalizedPower = session.GetNormalizedPower(),
            TotalTrainingEffect = session.GetTotalTrainingEffect(),
            TotalAnaerobicTrainingEffect = session.GetTotalAnaerobicTrainingEffect(),
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
            TotalDistanceKm = lap.GetTotalDistance().HasValue
                ? lap.GetTotalDistance()!.Value / 1000f
                : null,
            AvgHeartRate = lap.GetAvgHeartRate(),
            MaxHeartRate = lap.GetMaxHeartRate(),
            AvgSpeedKph = SpeedMpsToKph(lap.GetEnhancedAvgSpeed() ?? lap.GetAvgSpeed()),
            AvgPacePerKm = SpeedMpsToPace(lap.GetEnhancedAvgSpeed() ?? lap.GetAvgSpeed()),
            AvgPower = lap.GetAvgPower(),
            MaxPower = lap.GetMaxPower(),
            NormalizedPower = lap.GetNormalizedPower(),
            TotalAscent = lap.GetTotalAscent(),
            TotalDescent = lap.GetTotalDescent(),
        };
    }

    private static float? SpeedMpsToKph(float? speedMps)
    {
        if (speedMps is null or 0f) return null;
        return speedMps.Value * 3.6f;
    }

    private static TimeSpan? SpeedMpsToPace(float? speedMps)
    {
        if (speedMps is null or <= 0f) return null;
        var secondsPerKm = 1000.0 / speedMps.Value;
        return TimeSpan.FromSeconds(secondsPerKm);
    }
}
