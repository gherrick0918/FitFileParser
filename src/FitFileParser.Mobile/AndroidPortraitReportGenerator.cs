using FitFileParser.Parsing;
using FitFileParser.Rendering;

namespace FitFileParser.Mobile;

/// <summary>
/// Shared report generation entry point for Android portrait-oriented output.
/// </summary>
public sealed class AndroidPortraitReportGenerator
{
    private readonly FitParser _parser = new();
    private readonly PngRenderer _renderer = new(PngRenderer.ReportLayout.AndroidPortrait);

    public IReadOnlyList<string> RenderFromFit(Stream fitStream, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(fitStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var activity = _parser.Parse(fitStream);
        return _renderer.Render(activity, outputDirectory);
    }
}
