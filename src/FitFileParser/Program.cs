using FitFileParser.Parsing;
using FitFileParser.Rendering;

// -------------------------------------------------------------------------
// FitFileParser – convert .fit activity files to letter-size PNG reports
//
// Usage:
//   fitfileparser <activity.fit> [--output <dir>]
// -------------------------------------------------------------------------

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintUsage();
    return 0;
}

string inputPath = args[0];
string outputDirectory = "report";

for (int i = 1; i < args.Length; i++)
{
    if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
    {
        outputDirectory = args[++i];
    }
    else
    {
        Console.Error.WriteLine($"Unknown option: {args[i]}");
        PrintUsage();
        return 1;
    }
}

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Error: File not found: {inputPath}");
    return 1;
}

try
{
    Console.WriteLine($"Parsing {inputPath} ...");
    using var stream = File.OpenRead(inputPath);

    var parser = new FitParser();
    var activity = parser.Parse(stream);

    Console.WriteLine($"  Sport:    {activity.Sport}");
    Console.WriteLine($"  Date:     {activity.StartTime:yyyy-MM-dd HH:mm}");
    Console.WriteLine($"  Duration: {activity.TotalTimerTime}");
    if (activity.TotalDistanceKm.HasValue)
        Console.WriteLine($"  Distance: {activity.TotalDistanceKm.Value:F2} km");
    Console.WriteLine($"  Laps:     {activity.Laps.Count}");

    Console.WriteLine($"\nRendering PNG report to {outputDirectory}/ ...");
    var renderer = new PngRenderer();
    var pages = renderer.Render(activity, outputDirectory);

    foreach (var page in pages)
        Console.WriteLine($"  {page}");

    Console.WriteLine("\nDone.");
    return 0;
}
catch (FitParseException ex)
{
    Console.Error.WriteLine($"Error parsing FIT file: {ex.Message}");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    return 3;
}

static void PrintUsage()
{
    Console.WriteLine("""
        FitFileParser — convert .fit activity files to letter-size PNG reports

        Usage:
          fitfileparser <activity.fit> [--output <dir>]

        Options:
          --output, -o <dir>   Output directory for PNG files (default: report/)
          --help,  -h          Show this help message

        Example:
          fitfileparser outdoor-run.fit --output run-report/
        """);
}
