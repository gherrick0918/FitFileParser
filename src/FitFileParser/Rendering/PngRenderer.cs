using FitFileParser.Models;
using SkiaSharp;

namespace FitFileParser.Rendering;

/// <summary>
/// Renders an <see cref="ActivitySummary"/> as one or more letter-size PNG pages
/// suitable for uploading to coaching platforms.
/// </summary>
public sealed class PngRenderer
{
    // Letter paper at 150 DPI: 8.5" × 11"
    internal const int PageWidthPx = 1275;
    internal const int PageHeightPx = 1650;

    private const float Margin = 60f;
    private const float LineHeight = 36f;
    private const float SectionGap = 24f;
    private const float HeaderHeight = 80f;

    // Colour palette
    private static readonly SKColor ColorBackground = SKColors.White;
    private static readonly SKColor ColorAccent = new(0x1A, 0x73, 0xE8);
    private static readonly SKColor ColorText = new(0x21, 0x21, 0x21);
    private static readonly SKColor ColorMuted = new(0x75, 0x75, 0x75);
    private static readonly SKColor ColorDivider = new(0xE0, 0xE0, 0xE0);
    private static readonly SKColor ColorRowAlt = new(0xF5, 0xF7, 0xFF);

    /// <summary>
    /// Renders the activity into PNG files written to <paramref name="outputDirectory"/>.
    /// </summary>
    /// <returns>Paths to the generated PNG files, one per page.</returns>
    public IReadOnlyList<string> Render(ActivitySummary activity, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var pages = Paginate(activity);
        var outputPaths = new List<string>(pages.Count);

        for (int i = 0; i < pages.Count; i++)
        {
            var path = Path.Combine(outputDirectory, $"activity-page{i + 1}.png");
            RenderPage(activity, pages[i], i + 1, pages.Count, path);
            outputPaths.Add(path);
        }

        return outputPaths;
    }

    // -----------------------------------------------------------------
    // Pagination
    // -----------------------------------------------------------------

    private static List<IReadOnlyList<int>> Paginate(ActivitySummary activity)
    {
        float overview = OverviewBlockHeight(activity) + SectionGap;
        float lapHead = LapTableHeaderHeight();
        float rowH = LapRowHeight();
        float usable = PageHeightPx - Margin * 2 - HeaderHeight - SectionGap;

        int lapsPerPage1 = Math.Max(1, (int)((usable - overview - lapHead) / rowH));
        int lapsPerPageN = Math.Max(1, (int)((usable - lapHead) / rowH));

        var pages = new List<IReadOnlyList<int>>();
        var remaining = Enumerable.Range(0, activity.Laps.Count).ToList();

        pages.Add(remaining.Take(lapsPerPage1).ToList());
        remaining = remaining.Skip(lapsPerPage1).ToList();

        while (remaining.Count > 0)
        {
            pages.Add(remaining.Take(lapsPerPageN).ToList());
            remaining = remaining.Skip(lapsPerPageN).ToList();
        }

        return pages;
    }

    // -----------------------------------------------------------------
    // Page rendering
    // -----------------------------------------------------------------

    private void RenderPage(ActivitySummary activity, IReadOnlyList<int> lapIndices,
                            int pageNum, int totalPages, string outputPath)
    {
        using var bitmap = new SKBitmap(PageWidthPx, PageHeightPx);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(ColorBackground);

        float y = Margin;
        y = DrawHeader(canvas, activity, pageNum, totalPages, y);
        y += SectionGap;

        if (pageNum == 1)
        {
            y = DrawOverviewBlock(canvas, activity, y);
            y += SectionGap;
        }

        if (lapIndices.Count > 0)
            DrawLapTable(canvas, activity, lapIndices, y);

        DrawFooter(canvas);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.OpenWrite(outputPath);
        data.SaveTo(file);
    }

    // -----------------------------------------------------------------
    // Header
    // -----------------------------------------------------------------

    private static float DrawHeader(SKCanvas canvas, ActivitySummary activity,
                                    int pageNum, int totalPages, float y)
    {
        DrawText(canvas, FormatTitle(activity), Margin, y + 28f,
                 ColorAccent, 28f, bold: true);
        DrawText(canvas,
                 $"{activity.StartTime:dddd, MMMM d, yyyy}  ·  {FormatSport(activity)}",
                 Margin, y + 54f, ColorMuted, 16f);

        if (totalPages > 1)
        {
            string pageLabel = $"Page {pageNum} of {totalPages}";
            float tw = MeasureText(pageLabel, 14f);
            DrawText(canvas, pageLabel, PageWidthPx - Margin - tw, y + 28f, ColorMuted, 14f);
        }

        float divY = y + HeaderHeight - 8f;
        DrawLine(canvas, Margin, divY, PageWidthPx - Margin, divY, ColorDivider);
        return divY + 8f;
    }

    // -----------------------------------------------------------------
    // Overview stats block
    // -----------------------------------------------------------------

    private static float OverviewBlockHeight(ActivitySummary activity)
    {
        const int cols = 4;
        int rows = (int)Math.Ceiling(BuildOverviewStats(activity).Count / (double)cols);
        return rows * (LineHeight * 2 + SectionGap);
    }

    private static float DrawOverviewBlock(SKCanvas canvas, ActivitySummary activity, float y)
    {
        var stats = BuildOverviewStats(activity);

        const int cols = 4;
        float colWidth = (PageWidthPx - Margin * 2) / cols;
        int rows = (int)Math.Ceiling(stats.Count / (double)cols);

        for (int i = 0; i < stats.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float x = Margin + col * colWidth;
            float baseY = y + row * (LineHeight * 2 + SectionGap);

            DrawText(canvas, stats[i].Label, x, baseY + 16f, ColorMuted, 13f);
            DrawText(canvas, stats[i].Value, x, baseY + 16f + LineHeight, ColorText, 22f, bold: true);
        }

        return y + rows * (LineHeight * 2 + SectionGap);
    }

    /// <summary>
    /// Builds the overview stat tiles. Only tiles with real data (non-dash) are included
    /// so sport-specific metrics don't clutter reports where they're absent.
    /// </summary>
    private static IReadOnlyList<(string Label, string Value)> BuildOverviewStats(ActivitySummary activity)
    {
        var allPossibleStats = new List<(string Label, string? Value)>
        {
            // ── Timing ────────────────────────────────────────────────────
            ("TIME",              FormatElapsed(activity.TotalTimerTime)),
            ("MOVING TIME",       activity.TotalMovingTime.HasValue
                                      ? FormatElapsed(activity.TotalMovingTime.Value) : null),
            ("ELAPSED TIME",      FormatElapsed(activity.TotalElapsedTime)),
            ("ACTIVE TIME",       activity.ActiveTime.HasValue
                                      ? FormatElapsed(activity.ActiveTime.Value) : null),

            // ── Distance / Speed / Pace ───────────────────────────────────
            ("DISTANCE",          activity.TotalDistanceMiles.HasValue
                                      ? $"{activity.TotalDistanceMiles.Value:F2} mi" : null),
            ("AVG PACE",          activity.AvgPacePerMile.HasValue
                                      ? FormatPace(activity.AvgPacePerMile.Value) : null),
            ("AVG SPEED",         activity.AvgSpeedMph.HasValue
                                      ? $"{activity.AvgSpeedMph.Value:F1} mph" : null),
            ("MAX SPEED",         activity.MaxSpeedMph.HasValue
                                      ? $"{activity.MaxSpeedMph.Value:F1} mph" : null),

            // ── Heart Rate ────────────────────────────────────────────────
            ("MIN HR",            activity.MinHeartRate.HasValue
                                      ? $"{activity.MinHeartRate} bpm" : null),
            ("AVG HR",            activity.AvgHeartRate.HasValue
                                      ? $"{activity.AvgHeartRate} bpm" : null),
            ("MAX HR",            activity.MaxHeartRate.HasValue
                                      ? $"{activity.MaxHeartRate} bpm" : null),

            // ── Cadence ───────────────────────────────────────────────────
            ("AVG CADENCE",       activity.AvgRunningCadence.HasValue || activity.AvgCadence.HasValue
                                      ? $"{activity.AvgRunningCadence ?? activity.AvgCadence} spm" : null),
            ("MAX CADENCE",       activity.MaxRunningCadence.HasValue || activity.MaxCadence.HasValue
                                      ? $"{activity.MaxRunningCadence ?? activity.MaxCadence} spm" : null),

            // ── Calories ──────────────────────────────────────────────────
            ("CALORIES",          activity.TotalCalories.HasValue
                                      ? $"{activity.TotalCalories} kcal" : null),
            ("FAT CALORIES",      activity.TotalFatCalories.HasValue
                                      ? $"{activity.TotalFatCalories} kcal" : null),

            // ── Power ─────────────────────────────────────────────────────
            ("AVG POWER",         activity.AvgPower.HasValue
                                      ? $"{activity.AvgPower} W" : null),
            ("MAX POWER",         activity.MaxPower.HasValue
                                      ? $"{activity.MaxPower} W" : null),
            ("NP",                activity.NormalizedPower.HasValue
                                      ? $"{activity.NormalizedPower} W" : null),
            ("FTP",               activity.ThresholdPower.HasValue
                                      ? $"{activity.ThresholdPower} W" : null),
            ("WORK",              activity.TotalWorkJoules.HasValue
                                      ? $"{activity.TotalWorkJoules.Value / 1000f:F1} kJ" : null),

            // ── Training Load ─────────────────────────────────────────────
            ("TRAINING EFFECT",   activity.TotalTrainingEffect.HasValue
                                      ? $"{activity.TotalTrainingEffect.Value:F1}" : null),
            ("ANAEROBIC TE",      activity.TotalAnaerobicTrainingEffect.HasValue
                                      ? $"{activity.TotalAnaerobicTrainingEffect.Value:F1}" : null),
            ("TSS",               activity.TrainingStressScore.HasValue
                                      ? $"{activity.TrainingStressScore.Value:F1}" : null),
            ("INTENSITY FACTOR",  activity.IntensityFactor.HasValue
                                      ? $"{activity.IntensityFactor.Value:F2}" : null),
            ("TRAINING LOAD",     activity.TrainingLoadPeak.HasValue
                                      ? $"{activity.TrainingLoadPeak.Value:F1}" : null),

            // ── Elevation (imperial) ──────────────────────────────────────
            ("ASCENT",            activity.TotalAscentFt.HasValue
                                      ? $"+{activity.TotalAscentFt.Value:F0} ft" : null),
            ("DESCENT",           activity.TotalDescentFt.HasValue
                                      ? $"-{activity.TotalDescentFt.Value:F0} ft" : null),
            ("MIN ALTITUDE",      activity.MinAltitudeFt.HasValue
                                      ? $"{activity.MinAltitudeFt.Value:F0} ft" : null),
            ("MAX ALTITUDE",      activity.MaxAltitudeFt.HasValue
                                      ? $"{activity.MaxAltitudeFt.Value:F0} ft" : null),

            // ── Temperature (imperial) ────────────────────────────────────
            ("MIN TEMP",          activity.MinTemperatureF.HasValue
                                      ? $"{activity.MinTemperatureF.Value:F0}°F" : null),
            ("AVG TEMP",          activity.AvgTemperatureF.HasValue
                                      ? $"{activity.AvgTemperatureF.Value:F0}°F" : null),
            ("MAX TEMP",          activity.MaxTemperatureF.HasValue
                                      ? $"{activity.MaxTemperatureF.Value:F0}°F" : null),

            // ── Running Form ──────────────────────────────────────────────
            ("STRIDES",           activity.TotalStrides.HasValue
                                      ? $"{activity.TotalStrides}" : null),
            ("VERT OSC",          activity.AvgVerticalOscillationIn.HasValue
                                      ? $"{activity.AvgVerticalOscillationIn.Value:F2} in" : null),
            ("STANCE TIME",       activity.AvgStanceTimeMs.HasValue
                                      ? $"{activity.AvgStanceTimeMs.Value:F0} ms" : null),
            ("STANCE %",          activity.AvgStanceTimePercent.HasValue
                                      ? $"{activity.AvgStanceTimePercent.Value:F1}%" : null),
            ("VERT RATIO",        activity.AvgVerticalRatio.HasValue
                                      ? $"{activity.AvgVerticalRatio.Value:F1}%" : null),
            ("STEP LENGTH",       activity.AvgStepLengthFt.HasValue
                                      ? $"{activity.AvgStepLengthFt.Value:F2} ft" : null),
            ("AVG GRADE",         activity.AvgGrade.HasValue
                                      ? $"{activity.AvgGrade.Value:F1}%" : null),
            ("MAX GRADE",         activity.MaxPosGrade.HasValue
                                      ? $"{activity.MaxPosGrade.Value:F1}%" : null),
            ("MIN GRADE",         activity.MaxNegGrade.HasValue
                                      ? $"{activity.MaxNegGrade.Value:F1}%" : null),

            // ── Cycling ───────────────────────────────────────────────────
            ("L/R BALANCE",       activity.LeftRightBalance.HasValue
                                      ? FormatLeftRightBalance(activity.LeftRightBalance.Value) : null),
            ("L TORQUE EFF",      activity.AvgLeftTorqueEffectiveness.HasValue
                                      ? $"{activity.AvgLeftTorqueEffectiveness.Value:F1}%" : null),
            ("R TORQUE EFF",      activity.AvgRightTorqueEffectiveness.HasValue
                                      ? $"{activity.AvgRightTorqueEffectiveness.Value:F1}%" : null),
            ("L PEDAL SMOOTH",    activity.AvgLeftPedalSmoothness.HasValue
                                      ? $"{activity.AvgLeftPedalSmoothness.Value:F1}%" : null),
            ("R PEDAL SMOOTH",    activity.AvgRightPedalSmoothness.HasValue
                                      ? $"{activity.AvgRightPedalSmoothness.Value:F1}%" : null),
            ("PEDAL SMOOTH",      activity.AvgCombinedPedalSmoothness.HasValue
                                      ? $"{activity.AvgCombinedPedalSmoothness.Value:F1}%" : null),

            // ── Swimming ──────────────────────────────────────────────────
            ("SWIM STROKE",       !string.IsNullOrWhiteSpace(activity.SwimStroke)
                                      ? activity.SwimStroke : null),
            ("POOL LENGTH",       activity.PoolLengthYards.HasValue
                                      ? $"{activity.PoolLengthYards.Value:F1} yd" : null),
            ("TOTAL STROKES",     activity.TotalStrokes.HasValue
                                      ? $"{activity.TotalStrokes}" : null),
            ("TOTAL CYCLES",      activity.TotalCycles.HasValue
                                      ? $"{activity.TotalCycles}" : null),
            ("STROKE DIST",       activity.AvgStrokeDistanceYards.HasValue
                                      ? $"{activity.AvgStrokeDistanceYards.Value:F2} yd" : null),

            // ── Physiology / Wellness ─────────────────────────────────────
            ("AVG RESPIRATION",   activity.AvgRespirationRate.HasValue
                                      ? $"{activity.AvgRespirationRate.Value:F1} br/m" : null),
            ("AVG SPO2",          activity.AvgSpo2.HasValue
                                      ? $"{activity.AvgSpo2.Value:F1}%" : null),
            ("HRV (RMSSD)",       activity.RmssdHrv.HasValue
                                      ? $"{activity.RmssdHrv.Value:F1} ms" : null),

            // ── Workout Feedback ──────────────────────────────────────────
            ("WORKOUT FEEL",      activity.WorkoutFeel.HasValue
                                      ? $"{activity.WorkoutFeel}" : null),
            ("WORKOUT RPE",       activity.WorkoutRpe.HasValue
                                      ? $"{activity.WorkoutRpe}/20" : null),

            // ── Meta ──────────────────────────────────────────────────────
            ("LAPS",              activity.NumLaps.HasValue
                                      ? $"{activity.NumLaps}" : null),
            ("PROFILE",           !string.IsNullOrWhiteSpace(activity.SportProfileName)
                                      ? activity.SportProfileName : null),
        };

        // Only return tiles that have actual data
        return allPossibleStats
            .Where(stat => stat.Value is not null)
            .Select(stat => (stat.Label, Value: stat.Value!))
            .ToList();    }

    // -----------------------------------------------------------------
    // Lap table
    // -----------------------------------------------------------------

    private static float LapTableHeaderHeight() => 28f + 28f; // section heading + column headers
    private static float LapRowHeight() => 32f;

    private static float DrawLapTable(SKCanvas canvas, ActivitySummary activity,
                                      IReadOnlyList<int> lapIndices, float y)
    {
        DrawText(canvas, "LAP DETAILS", Margin, y + 18f, ColorAccent, 14f, bold: true);
        y += 28f;

        // 14 columns – fractions must sum to 1.0
        var cols = new (string Header, float Fraction, bool Right)[]
        {
            ("LAP",      0.04f, false),
            ("TIME",     0.10f, true),
            ("DIST mi",  0.10f, true),
            ("PACE",     0.11f, true),
            ("SPD mph",  0.08f, true),
            ("AVG HR",   0.07f, true),
            ("MAX HR",   0.07f, true),
            ("CAD",      0.06f, true),
            ("AVG PWR",  0.08f, true),
            ("NP",       0.07f, true),
            ("↑ ft",     0.06f, true),
            ("↓ ft",     0.06f, true),
            ("CALS",     0.05f, true),
            ("TEMP °F",  0.05f, true),
        };

        float tableWidth = PageWidthPx - Margin * 2;
        var colX = new float[cols.Length];
        colX[0] = Margin;
        for (int i = 1; i < cols.Length; i++)
            colX[i] = colX[i - 1] + cols[i - 1].Fraction * tableWidth;

        float rowH = LapRowHeight();

        // Column header row
        DrawRowBackground(canvas, y, ColorRowAlt);
        for (int c = 0; c < cols.Length; c++)
        {
            float cellX = cols[c].Right
                ? colX[c] + cols[c].Fraction * tableWidth - MeasureText(cols[c].Header, 12f) - 4f
                : colX[c] + 4f;
            DrawText(canvas, cols[c].Header, cellX, y + 20f, ColorMuted, 12f, bold: true);
        }
        y += rowH;

        // Data rows
        for (int li = 0; li < lapIndices.Count; li++)
        {
            var lap = activity.Laps[lapIndices[li]];
            if (li % 2 == 1) DrawRowBackground(canvas, y, ColorRowAlt);

            var cadence = lap.AvgRunningCadence ?? lap.AvgCadence;

            var values = new[]
            {
                lap.LapNumber.ToString(),
                FormatElapsed(lap.TotalTimerTime),
                lap.TotalDistanceMiles.HasValue  ? $"{lap.TotalDistanceMiles.Value:F2}" : "—",
                lap.AvgPacePerMile.HasValue      ? FormatPace(lap.AvgPacePerMile.Value) : "—",
                lap.AvgSpeedMph.HasValue         ? $"{lap.AvgSpeedMph.Value:F1}" : "—",
                lap.AvgHeartRate.HasValue        ? lap.AvgHeartRate.ToString()! : "—",
                lap.MaxHeartRate.HasValue        ? lap.MaxHeartRate.ToString()! : "—",
                cadence.HasValue                 ? cadence.ToString()! : "—",
                lap.AvgPower.HasValue            ? lap.AvgPower.ToString()! : "—",
                lap.NormalizedPower.HasValue     ? lap.NormalizedPower.ToString()! : "—",
                lap.TotalAscentFt.HasValue       ? $"+{lap.TotalAscentFt.Value:F0}" : "—",
                lap.TotalDescentFt.HasValue      ? $"-{lap.TotalDescentFt.Value:F0}" : "—",
                lap.TotalCalories.HasValue       ? lap.TotalCalories.ToString()! : "—",
                lap.AvgTemperatureF.HasValue     ? $"{lap.AvgTemperatureF.Value:F0}°" : "—",
            };

            for (int c = 0; c < cols.Length; c++)
            {
                float cellX = cols[c].Right
                    ? colX[c] + cols[c].Fraction * tableWidth - MeasureText(values[c], 14f) - 4f
                    : colX[c] + 4f;
                DrawText(canvas, values[c], cellX, y + 22f, ColorText, 14f);
            }

            DrawLine(canvas, Margin, y + rowH, PageWidthPx - Margin, y + rowH, ColorDivider);
            y += rowH;
        }

        return y;
    }

    // -----------------------------------------------------------------
    // Footer
    // -----------------------------------------------------------------

    private static void DrawFooter(SKCanvas canvas)
    {
        const string note = "FIT files may contain location data, timestamps, and health-related metrics.";
        DrawText(canvas, note, Margin, PageHeightPx - Margin / 2f, ColorMuted, 11f);
    }

    // -----------------------------------------------------------------
    // Low-level drawing helpers
    // -----------------------------------------------------------------

    private static void DrawText(SKCanvas canvas, string text, float x, float y,
                                 SKColor color, float size, bool bold = false)
    {
        using var typeface = SKTypeface.FromFamilyName(
            null, bold ? SKFontStyle.Bold : SKFontStyle.Normal);
        using var font = new SKFont(typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
    }

    private static void DrawLine(SKCanvas canvas, float x1, float y1, float x2, float y2,
                                 SKColor color)
    {
        using var paint = new SKPaint { Color = color, StrokeWidth = 1f };
        canvas.DrawLine(x1, y1, x2, y2, paint);
    }

    private static void DrawRowBackground(SKCanvas canvas, float y, SKColor color)
    {
        using var paint = new SKPaint { Color = color };
        canvas.DrawRect(Margin, y, PageWidthPx - Margin * 2, LapRowHeight(), paint);
    }

    private static float MeasureText(string text, float size)
    {
        using var typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Normal);
        using var font = new SKFont(typeface, size);
        using var paint = new SKPaint();
        return font.MeasureText(text, paint);
    }

    // -----------------------------------------------------------------
    // Formatting helpers
    // -----------------------------------------------------------------

    private static string FormatTitle(ActivitySummary activity) =>
        $"{FormatSport(activity)} — {activity.StartTime:MMM d, yyyy}";

    private static string FormatSport(ActivitySummary activity) =>
        !string.IsNullOrWhiteSpace(activity.SubSport) &&
        !activity.SubSport.Equals("Generic", StringComparison.OrdinalIgnoreCase)
            ? $"{activity.Sport} / {activity.SubSport}"
            : activity.Sport;

    private static string FormatElapsed(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";

    private static string FormatPace(TimeSpan pace) =>
        $"{(int)pace.TotalMinutes}:{pace.Seconds:D2} /mi";

    /// <summary>
    /// Decodes the FIT left-right balance raw value (bit 15 = right dominant,
    /// bits 14:0 = percentage × 100) into a human-readable string.
    /// </summary>
    private static string FormatLeftRightBalance(ushort raw)
    {
        bool rightDominant = (raw & 0x8000) != 0;
        float pct = (raw & 0x7FFF) / 100f;
        float leftPct = rightDominant ? 100f - pct : pct;
        float rightPct = 100f - leftPct;
        return $"L{leftPct:F0}%/R{rightPct:F0}%";
    }
}
