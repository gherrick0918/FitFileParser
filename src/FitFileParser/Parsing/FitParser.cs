using Dynastream.Fit;
using FitFileParser.Models;
using System.Reflection;
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
    private const float LbsPerKg = 2.20462f;

    /// <summary>Maximum seconds difference when correlating a <see cref="SetMesg"/> to a <see cref="LapMesg"/>.</summary>
    private const int SetLapCorrelationToleranceSec = 10;

    // ── Exercise name lookup: (ExerciseCategory, CategorySubtype) → readable name ──────
    // Built once via reflection from the Garmin FIT SDK exercise-name constant types.
    private static readonly IReadOnlyDictionary<(ushort Category, ushort Subtype), string> ExerciseNameMap
        = BuildExerciseNameMap();

    private static Dictionary<(ushort, ushort), string> BuildExerciseNameMap()
    {
        var map = new Dictionary<(ushort, ushort), string>();
        var assembly = typeof(ExerciseCategory).Assembly;
        var exCatType = typeof(ExerciseCategory);

        foreach (var catField in exCatType.GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f.FieldType == typeof(ushort)))
        {
            ushort catValue = (ushort)catField.GetValue(null)!;
            if (catValue >= 65534) continue; // Unknown / Invalid

            string nameTypeName = $"Dynastream.Fit.{catField.Name}ExerciseName";
            var nameType = assembly.GetType(nameTypeName);
            if (nameType is null) continue;

            foreach (var nameField in nameType.GetFields(BindingFlags.Public | BindingFlags.Static)
                         .Where(f => f.FieldType == typeof(ushort)))
            {
                if (nameField.Name == "Invalid") continue;
                ushort subtypeValue = (ushort)nameField.GetValue(null)!;
                if (subtypeValue == 65535) continue;
                var key = (catValue, subtypeValue);
                if (!map.ContainsKey(key))
                    map[key] = PascalToWords(nameField.Name);
            }
        }

        return map;
    }

    /// <summary>Inserts a space before each uppercase letter that follows a lower-case letter.</summary>
    private static string PascalToWords(string pascal)
    {
        if (string.IsNullOrEmpty(pascal)) return pascal;
        var sb = new System.Text.StringBuilder(pascal.Length + 8);
        sb.Append(pascal[0]);
        for (int i = 1; i < pascal.Length; i++)
        {
            if (char.IsUpper(pascal[i]) && char.IsLower(pascal[i - 1]))
                sb.Append(' ');
            sb.Append(pascal[i]);
        }
        return sb.ToString();
    }

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
        var setMesgs = new List<SetMesg>();
        var eventMesgs = new List<EventMesg>();

        broadcaster.SessionMesgEvent += (_, e) => sessionMesg = e.mesg as SessionMesg;
        broadcaster.LapMesgEvent += (_, e) => { if (e.mesg is LapMesg lap) lapMesgs.Add(lap); };
        broadcaster.SetMesgEvent += (_, e) => { if (e.mesg is SetMesg set) setMesgs.Add(set); };
        broadcaster.EventMesgEvent += (_, e) => { if (e.mesg is EventMesg evt) eventMesgs.Add(evt); };

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

        return BuildActivitySummary(sessionMesg, lapMesgs, setMesgs, eventMesgs);
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

    private static ActivitySummary BuildActivitySummary(SessionMesg session, List<LapMesg> lapMesgs,
                                                        List<SetMesg> setMesgs, List<EventMesg> eventMesgs)
    {
        List<LapSummary> laps;

        if (lapMesgs.Count == 0 && setMesgs.Count > 0)
        {
            // Some Garmin firmware emits only SetMesg records for strength training,
            // with no corresponding LapMesg. Build lap summaries directly from sets.
            var sortedSets = setMesgs
                .OrderBy(s => s.GetTimestamp()?.GetDateTime() ?? SysDateTime.MinValue)
                .ToList();
            laps = sortedSets
                .Select((set, index) => BuildLapSummaryFromSet(set, index + 1))
                .ToList();
        }
        else if (setMesgs.Count > 0 && lapMesgs.Count == setMesgs.Count)
        {
            // Counts match — use positional (time-sorted) 1:1 correlation.
            // This is more reliable than the fuzzy ±10 s timestamp lookup, which can
            // map multiple laps to the same set when sets are close together.
            var sortedLaps = lapMesgs
                .OrderBy(l => l.GetTimestamp()?.GetDateTime() ?? SysDateTime.MinValue)
                .ToList();
            var sortedSets = setMesgs
                .OrderBy(s => s.GetTimestamp()?.GetDateTime() ?? SysDateTime.MinValue)
                .ToList();
            laps = sortedLaps
                .Select((lap, index) => BuildLapSummary(lap, index + 1, sortedSets[index]))
                .ToList();
        }
        else
        {
            // Counts differ — fall back to timestamp-based correlation.
            var setByTime = BuildSetByTimeLookup(setMesgs);
            laps = lapMesgs
                .Select((lap, index) => BuildLapSummary(lap, index + 1, FindMatchingSet(lap, setByTime)))
                .ToList();
        }

        // Extract notes from various potential sources
        string? notes = ExtractNotes(session, eventMesgs);

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
                ? (int)session.GetTotalFatCalories()!.Value : null,

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

            // Notes
            Notes = notes,

            Laps = laps,
        };
    }

    /// <summary>
    /// Attempts to extract notes/comments from various potential sources in the FIT file.
    /// Note: This is a placeholder implementation as the Garmin FIT SDK version may not
    /// expose developer fields. Users can manually add notes to the ActivitySummary if needed.
    /// </summary>
    private static string? ExtractNotes(SessionMesg session, List<EventMesg> eventMesgs)
    {
        // The Garmin FIT SDK used in this project may not expose methods to access
        // developer fields or custom data fields where notes are typically stored.
        // This method is a placeholder for future enhancement when:
        // 1. The FIT SDK is updated to a version with developer field support
        // 2. We discover the specific message types that contain notes
        
        // For now, users can add notes by:
        // - Manually editing the output after generation
        // - Extending this method with SDK-specific note extraction logic
        // - Using the ActivitySummary.Notes property programmatically

        return null;
    }

    private static LapSummary BuildLapSummary(LapMesg lap, int lapNumber, SetMesg? matchedSet)
    {
        ExtractSetFields(matchedSet,
            out bool? isActiveSet, out ushort? numReps,
            out float? weightKg, out float? weightLbs,
            out string? exerciseCategoryName, out string? exerciseName);

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
            NumLengths = lap.GetNumLengths(),
            NumActiveLengths = lap.GetNumActiveLengths(),

            // Physiology
            AvgRespirationRate = lap.GetEnhancedAvgRespirationRate() ?? lap.GetAvgRespirationRate(),

            // Strength Training
            IsActiveSet = isActiveSet,
            NumReps = numReps,
            WeightKg = weightKg,
            WeightLbs = weightLbs,
            ExerciseCategoryName = exerciseCategoryName,
            ExerciseName = exerciseName,
        };
    }

    /// <summary>
    /// Builds a <see cref="LapSummary"/> purely from a <see cref="SetMesg"/> when no
    /// corresponding <see cref="LapMesg"/> is present in the file (some Garmin firmware
    /// omits <see cref="LapMesg"/> records for strength training activities).
    /// </summary>
    private static LapSummary BuildLapSummaryFromSet(SetMesg set, int lapNumber)
    {
        ExtractSetFields(set,
            out bool? isActiveSet, out ushort? numReps,
            out float? weightKg, out float? weightLbs,
            out string? exerciseCategoryName, out string? exerciseName);

        float duration = set.GetDuration() ?? 0f;

        return new LapSummary
        {
            LapNumber = lapNumber,
            StartTime = set.GetStartTime()?.GetDateTime() ?? SysDateTime.MinValue,
            TotalElapsedTime = TimeSpan.FromSeconds(duration),
            TotalTimerTime = TimeSpan.FromSeconds(duration),
            IsActiveSet = isActiveSet,
            NumReps = numReps,
            WeightKg = weightKg,
            WeightLbs = weightLbs,
            ExerciseCategoryName = exerciseCategoryName,
            ExerciseName = exerciseName,
        };
    }

    /// <summary>
    /// Extracts strength-training fields from a <see cref="SetMesg"/> into out parameters.
    /// All out parameters are set to null when <paramref name="set"/> is null.
    /// </summary>
    private static void ExtractSetFields(
        SetMesg? set,
        out bool? isActiveSet,
        out ushort? numReps,
        out float? weightKg,
        out float? weightLbs,
        out string? exerciseCategoryName,
        out string? exerciseName)
    {
        if (set is null)
        {
            isActiveSet = null;
            numReps = null;
            weightKg = null;
            weightLbs = null;
            exerciseCategoryName = null;
            exerciseName = null;
            return;
        }

        isActiveSet = set.GetSetType() == SetType.Active;
        numReps = set.GetRepetitions();
        weightKg = set.GetWeight();
        weightLbs = weightKg.HasValue ? weightKg.Value * LbsPerKg : null;

        ushort? category = set.GetNumCategory() > 0 ? set.GetCategory(0) : null;
        ushort? subtype  = set.GetNumCategorySubtype() > 0 ? set.GetCategorySubtype(0) : null;

        if (category.HasValue)
        {
            exerciseCategoryName = ResolveCategoryName(category.Value);
            exerciseName = subtype.HasValue &&
                           ExerciseNameMap.TryGetValue((category.Value, subtype.Value), out string? resolved)
                ? resolved : null;
        }
        else
        {
            exerciseCategoryName = null;
            exerciseName = null;
        }
    }

    // ── Set ↔ Lap correlation helpers ────────────────────────────────────

    /// <summary>
    /// Builds a lookup from <see cref="SetMesg"/> end-timestamp (rounded to second)
    /// to the message itself. When multiple sets share the same second (rare), the last
    /// one wins – actual data should never collide.
    /// </summary>
    private static Dictionary<SysDateTime, SetMesg> BuildSetByTimeLookup(List<SetMesg> sets)
    {
        var dict = new Dictionary<SysDateTime, SetMesg>();
        foreach (var s in sets)
        {
            var ts = s.GetTimestamp()?.GetDateTime();
            if (ts.HasValue)
                dict[RoundToSecond(ts.Value)] = s;
        }
        return dict;
    }

    /// <summary>
    /// Finds the <see cref="SetMesg"/> whose timestamp is closest to the lap's
    /// end-timestamp and is within a 10-second tolerance.
    /// </summary>
    private static SetMesg? FindMatchingSet(LapMesg lap, Dictionary<SysDateTime, SetMesg> setByTime)
    {
        if (setByTime.Count == 0) return null;

        var lapEndTime = lap.GetTimestamp()?.GetDateTime();
        if (!lapEndTime.HasValue) return null;

        SysDateTime lapRounded = RoundToSecond(lapEndTime.Value);

        // Exact match first.
        if (setByTime.TryGetValue(lapRounded, out var exact)) return exact;

        // Nearest within ±SetLapCorrelationToleranceSec seconds.
        SetMesg? best = null;
        double bestDiff = double.MaxValue;

        foreach (var kvp in setByTime)
        {
            double diff = Math.Abs((kvp.Key - lapRounded).TotalSeconds);
            if (diff < bestDiff && diff <= SetLapCorrelationToleranceSec)
            {
                bestDiff = diff;
                best = kvp.Value;
            }
        }

        return best;
    }

    private static SysDateTime RoundToSecond(SysDateTime dt) =>
        new SysDateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);

    /// <summary>
    /// Returns the human-readable name for an <see cref="ExerciseCategory"/> value,
    /// or null for unknown/invalid values.
    /// </summary>
    private static string? ResolveCategoryName(ushort categoryValue)
    {
        if (categoryValue >= 65534) return null;
        foreach (var f in typeof(ExerciseCategory).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.FieldType == typeof(ushort) && (ushort)f.GetValue(null)! == categoryValue)
                return PascalToWords(f.Name);
        }
        return null;
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
