using FitFileParser.Parsing;
using FitFileParser.Tests.Helpers;

namespace FitFileParser.Tests.Parsing;

public sealed class FitParserTests
{
    private readonly FitParser _parser = new();

    // ------------------------------------------------------------------
    // Valid file detection
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_ValidRunningActivity_ReturnsSummaryWithCorrectSport()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Running)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Equal("Running", summary.Sport);
    }

    [Fact]
    public void Parse_ValidActivity_ReturnsCorrectDuration()
    {
        using var stream = new FitFileBuilder()
            .WithDuration(3600f)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Equal(TimeSpan.FromHours(1), summary.TotalTimerTime);
    }

    [Fact]
    public void Parse_ValidActivity_ReturnsCorrectDistanceMiles()
    {
        using var stream = new FitFileBuilder()
            .WithDistance(10_000f)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.NotNull(summary.TotalDistanceMiles);
        Assert.Equal(6.21f, summary.TotalDistanceMiles!.Value, precision: 2);
    }

    [Fact]
    public void Parse_ValidActivity_ReturnsCorrectCalories()
    {
        using var stream = new FitFileBuilder()
            .WithCalories(550)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Equal(550, summary.TotalCalories);
    }

    // ------------------------------------------------------------------
    // Heart rate
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_ActivityWithHeartRate_ReturnsHeartRate()
    {
        using var stream = new FitFileBuilder()
            .WithAvgSpeed(3.0f)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.NotNull(summary.AvgHeartRate);
        Assert.Equal((byte)155, summary.AvgHeartRate);
        Assert.Equal((byte)185, summary.MaxHeartRate);
    }

    [Fact]
    public void Parse_ActivityWithoutHeartRate_ReturnsNullHeartRate()
    {
        using var stream = new FitFileBuilder()
            .WithNoHeartRate()
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Null(summary.AvgHeartRate);
        Assert.Null(summary.MaxHeartRate);
    }

    // ------------------------------------------------------------------
    // Power
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_ActivityWithoutPower_ReturnsNullPower()
    {
        using var stream = new FitFileBuilder()
            .WithNoPower()
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Null(summary.AvgPower);
    }

    [Fact]
    public void Parse_ActivityWithPower_ReturnsPower()
    {
        using var stream = new FitFileBuilder()
            .WithPower(250)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Equal((ushort)250, summary.AvgPower);
    }

    // ------------------------------------------------------------------
    // Pace calculation
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_ActivityWithSpeed_ReturnsPacePerMile()
    {
        // 2.778 m/s ≈ 10 km/h → ~9:39/mi
        using var stream = new FitFileBuilder()
            .WithAvgSpeed(2.778f)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.NotNull(summary.AvgPacePerMile);
        Assert.InRange(summary.AvgPacePerMile!.Value.TotalSeconds, 575, 585); // ~9:39/mi
    }

    // ------------------------------------------------------------------
    // Multiple laps
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_ActivityWithMultipleLaps_ReturnsCorrectLapCount()
    {
        using var stream = new FitFileBuilder()
            .AddLap(360f, 1000f, avgHr: 150, avgSpeedMps: 2.78f)
            .AddLap(360f, 1000f, avgHr: 160, avgSpeedMps: 2.78f)
            .AddLap(360f, 1000f, avgHr: 165, avgSpeedMps: 2.78f)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Equal(3, summary.Laps.Count);
    }

    [Fact]
    public void Parse_LapData_ContainsCorrectLapNumbers()
    {
        using var stream = new FitFileBuilder()
            .AddLap(300f, 1000f)
            .AddLap(300f, 1000f)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Equal(1, summary.Laps[0].LapNumber);
        Assert.Equal(2, summary.Laps[1].LapNumber);
    }

    [Fact]
    public void Parse_LapData_ContainsCorrectDistances()
    {
        using var stream = new FitFileBuilder()
            .AddLap(360f, 1000f)
            .AddLap(720f, 2000f)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Equal(0.62f, summary.Laps[0].TotalDistanceMiles!.Value, precision: 2);
        Assert.Equal(1.24f, summary.Laps[1].TotalDistanceMiles!.Value, precision: 2);
    }

    [Fact]
    public void Parse_SingleLap_LapDurationMatchesSession()
    {
        using var stream = new FitFileBuilder()
            .WithDuration(1800f)
            .WithDistance(5000f)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Single(summary.Laps);
        Assert.Equal(summary.TotalTimerTime, summary.Laps[0].TotalTimerTime);
    }

    // ------------------------------------------------------------------
    // Training effect
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_ActivityWithTrainingEffect_ReturnsTe()
    {
        using var stream = new FitFileBuilder()
            .WithTrainingEffect(3.8f)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.NotNull(summary.TotalTrainingEffect);
        Assert.Equal(3.8f, summary.TotalTrainingEffect!.Value, precision: 1);
    }

    [Fact]
    public void Parse_ActivityWithoutTrainingEffect_ReturnsNull()
    {
        using var stream = new FitFileBuilder().Build();

        var summary = _parser.Parse(stream);

        Assert.Null(summary.TotalTrainingEffect);
    }

    // ------------------------------------------------------------------
    // Different sports
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_CyclingActivity_ReturnsCyclingSport()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Cycling)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Equal("Cycling", summary.Sport);
    }

    // ------------------------------------------------------------------
    // Strength training
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_StrengthTrainingWithSets_ReturnsCorrectSetCount()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .AddStrengthSet(60f, reps: 10, weightKg: 60f,
                            category: Dynastream.Fit.ExerciseCategory.Squat,
                            categorySubtype: Dynastream.Fit.SquatExerciseName.BarbellBackSquat)
            .AddStrengthSet(60f, reps: 8, weightKg: 80f,
                            category: Dynastream.Fit.ExerciseCategory.BenchPress,
                            categorySubtype: Dynastream.Fit.BenchPressExerciseName.BarbellBenchPress)
            .AddStrengthSet(30f, reps: 0, weightKg: 0f, isRest: true)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Equal(3, summary.Laps.Count);
    }

    [Fact]
    public void Parse_StrengthTrainingWithSets_ReturnsRepsAndWeight()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .AddStrengthSet(60f, reps: 12, weightKg: 50f,
                            category: Dynastream.Fit.ExerciseCategory.Squat,
                            categorySubtype: Dynastream.Fit.SquatExerciseName.BarbellBackSquat)
            .Build();

        var summary = _parser.Parse(stream);
        var lap = summary.Laps[0];

        Assert.Equal((ushort)12, lap.NumReps);
        Assert.NotNull(lap.WeightKg);
        Assert.Equal(50f, lap.WeightKg!.Value, precision: 1);
        Assert.NotNull(lap.WeightLbs);
        Assert.InRange(lap.WeightLbs!.Value, 110f, 112f); // 50 kg ≈ 110.23 lbs
    }

    [Fact]
    public void Parse_StrengthTrainingWithSets_ResolvesExerciseName()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .AddStrengthSet(60f, reps: 10, weightKg: 60f,
                            category: Dynastream.Fit.ExerciseCategory.Squat,
                            categorySubtype: Dynastream.Fit.SquatExerciseName.BarbellBackSquat)
            .Build();

        var summary = _parser.Parse(stream);
        var lap = summary.Laps[0];

        Assert.NotNull(lap.ExerciseCategoryName);
        Assert.Contains("Squat", lap.ExerciseCategoryName, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(lap.ExerciseName);
        Assert.Contains("Barbell", lap.ExerciseName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Back Squat", lap.ExerciseName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_StrengthTrainingRestSet_IsMarkedAsRest()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .AddStrengthSet(30f, reps: 0, weightKg: 0f, isRest: true)
            .Build();

        var summary = _parser.Parse(stream);
        var lap = summary.Laps[0];

        Assert.NotNull(lap.IsActiveSet);
        Assert.False(lap.IsActiveSet!.Value);
    }

    [Fact]
    public void Parse_StrengthTrainingActiveSet_IsMarkedAsActive()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .AddStrengthSet(60f, reps: 10, weightKg: 60f,
                            category: Dynastream.Fit.ExerciseCategory.Squat,
                            categorySubtype: Dynastream.Fit.SquatExerciseName.BarbellBackSquat)
            .Build();

        var summary = _parser.Parse(stream);
        var lap = summary.Laps[0];

        Assert.NotNull(lap.IsActiveSet);
        Assert.True(lap.IsActiveSet!.Value);
    }

    // ------------------------------------------------------------------
    // Strength training — SetMesg-only (no LapMesg) — some Garmin firmware
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_StrengthTrainingSetsOnly_ReturnsCorrectSetCount()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .WithSetsOnly()
            .AddStrengthSet(60f, reps: 10, weightKg: 60f,
                            category: Dynastream.Fit.ExerciseCategory.Squat,
                            categorySubtype: Dynastream.Fit.SquatExerciseName.BarbellBackSquat)
            .AddStrengthSet(60f, reps: 8, weightKg: 80f,
                            category: Dynastream.Fit.ExerciseCategory.BenchPress,
                            categorySubtype: Dynastream.Fit.BenchPressExerciseName.BarbellBenchPress)
            .AddStrengthSet(30f, reps: 0, weightKg: 0f, isRest: true)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.Equal(3, summary.Laps.Count);
    }

    [Fact]
    public void Parse_StrengthTrainingSetsOnly_ReturnsRepsAndWeight()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .WithSetsOnly()
            .AddStrengthSet(60f, reps: 12, weightKg: 50f,
                            category: Dynastream.Fit.ExerciseCategory.Squat,
                            categorySubtype: Dynastream.Fit.SquatExerciseName.BarbellBackSquat)
            .Build();

        var summary = _parser.Parse(stream);
        var lap = summary.Laps[0];

        Assert.Equal((ushort)12, lap.NumReps);
        Assert.NotNull(lap.WeightKg);
        Assert.Equal(50f, lap.WeightKg!.Value, precision: 1);
        Assert.NotNull(lap.WeightLbs);
        Assert.InRange(lap.WeightLbs!.Value, 110f, 112f);
    }

    [Fact]
    public void Parse_StrengthTrainingSetsOnly_ResolvesExerciseName()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .WithSetsOnly()
            .AddStrengthSet(60f, reps: 10, weightKg: 60f,
                            category: Dynastream.Fit.ExerciseCategory.Squat,
                            categorySubtype: Dynastream.Fit.SquatExerciseName.BarbellBackSquat)
            .Build();

        var summary = _parser.Parse(stream);
        var lap = summary.Laps[0];

        Assert.NotNull(lap.ExerciseCategoryName);
        Assert.Contains("Squat", lap.ExerciseCategoryName, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(lap.ExerciseName);
        Assert.Contains("Barbell", lap.ExerciseName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Back Squat", lap.ExerciseName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_StrengthTrainingSetsOnly_DurationPopulated()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .WithSetsOnly()
            .AddStrengthSet(45f, reps: 10, weightKg: 20f,
                            category: Dynastream.Fit.ExerciseCategory.Squat,
                            categorySubtype: Dynastream.Fit.SquatExerciseName.AirSquat)
            .Build();

        var summary = _parser.Parse(stream);
        var lap = summary.Laps[0];

        Assert.Equal(TimeSpan.FromSeconds(45), lap.TotalTimerTime);
    }

    [Fact]
    public void Parse_StrengthTrainingSetsOnly_RestSetMarkedAsRest()
    {
        using var stream = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .WithSetsOnly()
            .AddStrengthSet(30f, reps: 0, weightKg: 0f, isRest: true)
            .Build();

        var summary = _parser.Parse(stream);

        Assert.NotNull(summary.Laps[0].IsActiveSet);
        Assert.False(summary.Laps[0].IsActiveSet!.Value);
    }

    // ------------------------------------------------------------------
    // Error cases: corrupt / invalid files
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_EmptyStream_ThrowsFitParseException()
    {
        using var stream = new MemoryStream();

        Assert.Throws<FitParseException>(() => _parser.Parse(stream));
    }

    [Fact]
    public void Parse_NotAFitFile_ThrowsFitParseException()
    {
        using var stream = new MemoryStream("This is not a FIT file."u8.ToArray());

        Assert.Throws<FitParseException>(() => _parser.Parse(stream));
    }

    [Fact]
    public void Parse_TruncatedFile_ThrowsFitParseException()
    {
        using var stream = new FitFileBuilder()
            .WithTruncation()
            .Build();

        Assert.Throws<FitParseException>(() => _parser.Parse(stream));
    }

    [Fact]
    public void Parse_CorruptCrc_ThrowsFitParseException()
    {
        using var stream = new FitFileBuilder()
            .WithCorruptCrc()
            .Build();

        Assert.Throws<FitParseException>(() => _parser.Parse(stream));
    }

    [Fact]
    public void Parse_NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _parser.Parse(null!));
    }
}
