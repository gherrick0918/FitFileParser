using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using FitFileParser.Mobile;

namespace FitFileParser.AndroidApp;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    private const int OpenDocumentRequestCode = 1001;
    private readonly AndroidPortraitReportGenerator _reportGenerator = new();
    private TextView? _statusText;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        var selectFileButton = FindViewById<Button>(Resource.Id.selectFileButton);
        _statusText = FindViewById<TextView>(Resource.Id.statusText);

        if (selectFileButton is not null)
        {
            selectFileButton.Click += (_, _) => OpenFitFilePicker();
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == OpenDocumentRequestCode && resultCode == Result.Ok && data?.Data is not null)
        {
            _ = GenerateReportAsync(data.Data);
        }
    }

    private void OpenFitFilePicker()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraMimeTypes, new[] { "application/octet-stream" });

        StartActivityForResult(intent, OpenDocumentRequestCode);
    }

    private async Task GenerateReportAsync(Android.Net.Uri fitFileUri)
    {
        SetStatus(GetString(Resource.String.status_generating));

        try
        {
            await using var inputStream = ContentResolver?.OpenInputStream(fitFileUri)
                ?? throw new InvalidOperationException("Unable to open the selected file.");
            await using var inMemoryFit = new MemoryStream();
            await inputStream.CopyToAsync(inMemoryFit);
            inMemoryFit.Position = 0;

            var outputDirectory = Path.Combine(
                FilesDir?.AbsolutePath ?? throw new InvalidOperationException("App storage unavailable."),
                "reports",
                DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"));

            Directory.CreateDirectory(outputDirectory);

            var generatedPages = await Task.Run(() => _reportGenerator.RenderFromFit(inMemoryFit, outputDirectory));
            SetStatus(string.Format(GetString(Resource.String.status_success), generatedPages.Count, outputDirectory));
        }
        catch (Exception ex)
        {
            SetStatus(string.Format(GetString(Resource.String.status_error), ex.Message));
        }
    }

    private void SetStatus(string status)
    {
        RunOnUiThread(() =>
        {
            if (_statusText is not null)
            {
                _statusText.Text = status;
            }
        });
    }
}