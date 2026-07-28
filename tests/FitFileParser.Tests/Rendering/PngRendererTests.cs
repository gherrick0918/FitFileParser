using Dynastream.Fit;
using FitFileParser.Rendering;
using FitFileParser.Tests.Helpers;
using FitFileParser.Parsing;
using SysFile = System.IO.File;

namespace FitFileParser.Tests.Rendering;

public sealed class PngRendererTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), $"fit-test-{Guid.NewGuid():N}");
    private readonly FitParser _parser = new();
    private readonly PngRenderer _renderer = new();

    // ------------------------------------------------------------------
    // File generation
    // ------------------------------------------------------------------

    [Fact]
    public void Render_ValidActivity_ProducesAtLeastOnePngFile()
    {
        var activity = ParseActivity(new FitFileBuilder());

        var pages = _renderer.Render(activity, _outputDir);

        Assert.NotEmpty(pages);
    }

    [Fact]
    public void Render_ValidActivity_PngFileExists()
    {
        var activity = ParseActivity(new FitFileBuilder());

        var pages = _renderer.Render(activity, _outputDir);

        foreach (var page in pages)
            Assert.True(SysFile.Exists(page), $"Expected PNG file: {page}");
    }

    [Fact]
    public void Render_ValidActivity_OutputFileHasPngExtension()
    {
        var activity = ParseActivity(new FitFileBuilder());

        var pages = _renderer.Render(activity, _outputDir);

        Assert.All(pages, p => Assert.EndsWith(".png", p, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Render_ValidActivity_OutputFileIsNonEmpty()
    {
        var activity = ParseActivity(new FitFileBuilder());

        var pages = _renderer.Render(activity, _outputDir);

        Assert.All(pages, p => Assert.True(new FileInfo(p).Length > 0));
    }

    // ------------------------------------------------------------------
    // PNG dimensions
    // ------------------------------------------------------------------

    [Fact]
    public void Render_SinglePage_ProducesLetterSizeDimensions()
    {
        var activity = ParseActivity(new FitFileBuilder());

        var pages = _renderer.Render(activity, _outputDir);

        Assert.Single(pages);
        AssertLetterSize(pages[0]);
    }

    [Fact]
    public void Render_ManyLaps_ProducesMultiplePages()
    {
        var builder = new FitFileBuilder();
        for (int i = 0; i < 80; i++)
            builder.AddLap(60f, 250f, avgHr: 160, avgSpeedMps: 3f);

        var activity = ParseActivity(builder);

        var pages = _renderer.Render(activity, _outputDir);

        Assert.True(pages.Count > 1, "Expected pagination to produce more than one page for 80 laps.");
    }

    [Fact]
    public void Render_ManyLaps_AllPagesAreLetterSize()
    {
        var builder = new FitFileBuilder();
        for (int i = 0; i < 60; i++)
            builder.AddLap(60f, 250f);

        var activity = ParseActivity(builder);

        var pages = _renderer.Render(activity, _outputDir);

        Assert.All(pages, AssertLetterSize);
    }

    [Fact]
    public void Render_AndroidPortraitLayout_UsesPortraitDimensions()
    {
        var renderer = new PngRenderer(PngRenderer.ReportLayout.AndroidPortrait);
        var activity = ParseActivity(new FitFileBuilder());

        var pages = renderer.Render(activity, _outputDir);

        Assert.NotEmpty(pages);
        AssertImageDimensions(pages[0], 1080, 1920);
    }

    // ------------------------------------------------------------------
    // Output path behaviour
    // ------------------------------------------------------------------

    [Fact]
    public void Render_CreatesOutputDirectory_IfNotExists()
    {
        string newDir = Path.Combine(_outputDir, "subdir");
        var activity = ParseActivity(new FitFileBuilder());

        _renderer.Render(activity, newDir);

        Assert.True(Directory.Exists(newDir));
    }

    [Fact]
    public void Render_PageFilesAreNamedSequentially()
    {
        var builder = new FitFileBuilder();
        for (int i = 0; i < 60; i++)
            builder.AddLap(60f, 250f);

        var activity = ParseActivity(builder);
        var pages = _renderer.Render(activity, _outputDir);

        for (int i = 0; i < pages.Count; i++)
            Assert.Contains($"activity-page{i + 1}.png", pages[i]);
    }

    // ------------------------------------------------------------------
    // Strength training rendering
    // ------------------------------------------------------------------

    [Fact]
    public void Render_StrengthTrainingActivity_ProducesPngFile()
    {
        var builder = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .AddStrengthSet(60f, reps: 10, weightKg: 60f,
                            category: Dynastream.Fit.ExerciseCategory.Squat,
                            categorySubtype: Dynastream.Fit.SquatExerciseName.BarbellBackSquat)
            .AddStrengthSet(60f, reps: 8, weightKg: 80f,
                            category: Dynastream.Fit.ExerciseCategory.BenchPress,
                            categorySubtype: Dynastream.Fit.BenchPressExerciseName.BarbellBenchPress)
            .AddStrengthSet(30f, reps: 0, weightKg: 0f, isRest: true);

        var activity = ParseActivity(builder);
        var pages = _renderer.Render(activity, _outputDir);

        Assert.NotEmpty(pages);
        Assert.All(pages, p => Assert.True(SysFile.Exists(p)));
    }

    [Fact]
    public void Render_StrengthTrainingActivity_ProducesLetterSizeOutput()
    {
        var builder = new FitFileBuilder()
            .WithSport(Dynastream.Fit.Sport.Training, Dynastream.Fit.SubSport.StrengthTraining)
            .AddStrengthSet(60f, reps: 10, weightKg: 60f,
                            category: Dynastream.Fit.ExerciseCategory.Squat,
                            categorySubtype: Dynastream.Fit.SquatExerciseName.BarbellBackSquat);

        var activity = ParseActivity(builder);
        var pages = _renderer.Render(activity, _outputDir);

        AssertLetterSize(pages[0]);
    }

    // ------------------------------------------------------------------
    // Null / invalid argument handling
    // ------------------------------------------------------------------

    [Fact]
    public void Render_NullActivity_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _renderer.Render(null!, _outputDir));
    }

    [Fact]
    public void Render_NullOutputDir_ThrowsArgumentNullException()
    {
        var activity = ParseActivity(new FitFileBuilder());
        Assert.Throws<ArgumentNullException>(() => _renderer.Render(activity, null!));
    }

    [Fact]
    public void Render_EmptyOutputDir_ThrowsArgumentException()
    {
        var activity = ParseActivity(new FitFileBuilder());
        Assert.Throws<ArgumentException>(() => _renderer.Render(activity, string.Empty));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private FitFileParser.Models.ActivitySummary ParseActivity(FitFileBuilder builder)
    {
        using var stream = builder.Build();
        return _parser.Parse(stream);
    }

    private static void AssertLetterSize(string pngPath)
    {
        AssertImageDimensions(pngPath, 1275, 1650);
    }

    private static void AssertImageDimensions(string pngPath, int expectedWidth, int expectedHeight)
    {
        using var fs = SysFile.OpenRead(pngPath);
        // Read PNG IHDR chunk: bytes 16-23 contain width and height as big-endian uint32
        fs.Seek(16, SeekOrigin.Begin);
        var buf = new byte[8];
        fs.ReadExactly(buf);
        int width  = (buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3];
        int height = (buf[4] << 24) | (buf[5] << 16) | (buf[6] << 8) | buf[7];

        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }
}
