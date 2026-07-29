using FitFileParser.Models;
using FitFileParser.Parsing;
using FitFileParser.Rendering;

// -------------------------------------------------------------------------
// FitFileParser – convert .fit activity files to letter-size PNG reports
//
// Usage:
//   fitfileparser [<activity.fit>] [--output <dir>]
// -------------------------------------------------------------------------

if (args.Any(a => a is "-h" or "--help"))
{
    PrintUsage();
    return 0;
}

// Determine solution root (walk up from executable location)
string solutionRoot = GetSolutionRoot();

// Default to samples folder if no input file specified
string inputPath = args.Length > 0 && !args[0].StartsWith("--")
    ? args[0]
    : Path.Combine(solutionRoot, "samples");

// Parse output directory option
string? outputDirectory = null;
for (int i = 0; i < args.Length; i++)
{
    if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
    {
        outputDirectory = args[++i];
    }
}

// If input is a directory, look for .fit files
string[] fitFiles;
if (Directory.Exists(inputPath))
{
    fitFiles = Directory.GetFiles(inputPath, "*.fit", SearchOption.TopDirectoryOnly);
    if (fitFiles.Length == 0)
    {
        Console.Error.WriteLine($"Error: No .fit files found in directory: {inputPath}");
        return 1;
    }
    Console.WriteLine($"Found {fitFiles.Length} .fit file(s) in {inputPath}");
}
else if (File.Exists(inputPath))
{
    fitFiles = [inputPath];
}
else
{
    Console.Error.WriteLine($"Error: File or directory not found: {inputPath}");
    return 1;
}

int successCount = 0;
int errorCount = 0;

foreach (var fitFile in fitFiles)
{
    try
    {
        Console.WriteLine($"\n{'='} Processing {Path.GetFileName(fitFile)} {'='}");
        using var stream = File.OpenRead(fitFile);

        var parser = new FitParser();
        var activity = parser.Parse(stream);

        Console.WriteLine($"  Sport:    {activity.Sport}");
        if (!string.IsNullOrEmpty(activity.SubSport))
            Console.WriteLine($"  SubSport: {activity.SubSport}");
        Console.WriteLine($"  Date:     {activity.StartTime:yyyy-MM-dd HH:mm}");
        Console.WriteLine($"  Duration: {activity.TotalTimerTime}");
        if (activity.TotalDistanceMiles.HasValue)
            Console.WriteLine($"  Distance: {activity.TotalDistanceMiles.Value:F2} mi");
        if (activity.AvgPacePerMile.HasValue)
            Console.WriteLine($"  Avg pace: {FormatPace(activity.AvgPacePerMile.Value)}");
        if (activity.AvgSpeedMph.HasValue)
            Console.WriteLine($"  Avg speed:{activity.AvgSpeedMph.Value,6:F2} mph");
        if (activity.MaxSpeedMph.HasValue)
            Console.WriteLine($"  Max speed:{activity.MaxSpeedMph.Value,6:F2} mph");
        if (activity.AvgCadence.HasValue || activity.AvgRunningCadence.HasValue)
            Console.WriteLine($"  Cadence:  {activity.AvgRunningCadence ?? activity.AvgCadence} avg spm");
        if (activity.MaxCadence.HasValue || activity.MaxRunningCadence.HasValue)
            Console.WriteLine($"  Max cad.: {activity.MaxRunningCadence ?? activity.MaxCadence} spm");
        if (activity.TotalMovingTime.HasValue)
            Console.WriteLine($"  Moving:   {activity.TotalMovingTime.Value}");
        if (activity.TotalWorkJoules.HasValue)
            Console.WriteLine($"  Work:     {activity.TotalWorkJoules.Value / 1000f:F1} kJ");
        if (activity.MinAltitudeFt.HasValue || activity.MaxAltitudeFt.HasValue)
            Console.WriteLine($"  Altitude: {activity.MinAltitudeFt?.ToString("F0") ?? "—"} to {activity.MaxAltitudeFt?.ToString("F0") ?? "—"} ft");
        if (activity.AvgTemperatureF.HasValue || activity.MaxTemperatureF.HasValue)
            Console.WriteLine($"  Temp:     {activity.AvgTemperatureF?.ToString("F0") ?? "—"}°F avg, {activity.MaxTemperatureF?.ToString("F0") ?? "—"}°F max");
        Console.WriteLine($"  Laps:     {activity.Laps.Count}");

        // Generate descriptive output folder name with generation timestamp
        string activityName = BuildActivityName(activity);
        string generationTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folderName = $"{activityName}_gen{generationTime}";
        string defaultOutputDir = Path.Combine(solutionRoot, "output", folderName);
        string finalOutputDir = outputDirectory ?? defaultOutputDir;

        Console.WriteLine($"\nRendering PNG report to {finalOutputDir}/ ...");
        var renderer = new PngRenderer();
        var pages = renderer.Render(activity, finalOutputDir);

        foreach (var page in pages)
            Console.WriteLine($"  {Path.GetFileName(page)}");

        successCount++;
    }
    catch (FitParseException ex)
    {
        Console.Error.WriteLine($"Error parsing {Path.GetFileName(fitFile)}: {ex.Message}");
        errorCount++;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unexpected error processing {Path.GetFileName(fitFile)}: {ex.Message}");
        errorCount++;
    }
}

Console.WriteLine($"\n{'='} Summary {'='}");
Console.WriteLine($"  Successfully processed: {successCount}");
if (errorCount > 0)
    Console.WriteLine($"  Errors: {errorCount}");
Console.WriteLine("\nDone.");

return errorCount > 0 ? 2 : 0;

static void PrintUsage()
{
    Console.WriteLine("""
        FitFileParser — convert .fit activity files to letter-size PNG reports

        Usage:
          fitfileparser [<activity.fit>] [--output <dir>>]

        Arguments:
          <activity.fit>       Path to a .fit file or directory containing .fit files
                               (default: samples/ folder in solution root)

        Options:
          --output, -o <dir>   Output directory for PNG files 
                               (default: output/<activity-name>_gen<timestamp>/ in solution root)
          --help,  -h          Show this help message

        Examples:
          fitfileparser                                    # Process all files in samples/
          fitfileparser samples/outdoor-run.fit            # Process specific file
          fitfileparser samples/ --output my-reports/      # Custom output location
        """);
}

static string FormatPace(TimeSpan pace) => $"{(int)pace.TotalMinutes}:{pace.Seconds:D2} /mi";

static string GetSolutionRoot()
{
    // Start from the executable directory and walk up to find the solution root
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        // Look for .slnx or .sln file, or common solution folders
        if (dir.GetFiles("*.slnx").Length > 0 ||
            dir.GetFiles("*.sln").Length > 0 ||
            (Directory.Exists(Path.Combine(dir.FullName, "src")) &&
             Directory.Exists(Path.Combine(dir.FullName, "samples"))))
        {
            return dir.FullName;
        }
        dir = dir.Parent;
    }

    // Fallback to current directory
    return Directory.GetCurrentDirectory();
}

static string BuildActivityName(ActivitySummary activity)
{
    // Build a descriptive name: Sport_Date_Time
    string sport = SanitizeFileName(activity.Sport ?? "Activity");
    if (!string.IsNullOrEmpty(activity.SubSport))
        sport += $"-{SanitizeFileName(activity.SubSport)}";
    
    string timestamp = activity.StartTime.ToString("yyyy-MM-dd_HHmm");
    
    return $"{sport}_{timestamp}";
}

static string SanitizeFileName(string name)
{
    // Remove invalid filename characters
    var invalidChars = Path.GetInvalidFileNameChars();
    return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries))
        .Replace(" ", "-");
}
