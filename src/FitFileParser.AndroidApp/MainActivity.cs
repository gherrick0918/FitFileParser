using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Views.Accessibility;
using Android.Widget;
using AndroidX.Core.Content;
using FitFileParser.Mobile;

namespace FitFileParser.AndroidApp;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    private const int OpenDocumentRequestCode = 1001;
    private readonly AndroidPortraitReportGenerator _reportGenerator = new();
    private TextView? _statusText;
    private Button? _openOutputButton;
    private IReadOnlyList<string> _generatedFiles = [];

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        var selectFileButton = FindViewById<Button>(Resource.Id.selectFileButton);
        _statusText = FindViewById<TextView>(Resource.Id.statusText);
        _openOutputButton = FindViewById<Button>(Resource.Id.openOutputButton);

        if (selectFileButton is not null)
        {
            selectFileButton.Click += (_, _) => OpenFitFilePicker();
        }

        if (_openOutputButton is not null)
        {
            _openOutputButton.Click += (_, _) => OpenGeneratedImage();
        }

        if (savedInstanceState is null)
        {
            if (ShouldAutoLaunchPicker())
            {
                OpenFitFilePicker();
            }
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode != OpenDocumentRequestCode)
        {
            return;
        }

        if (resultCode == Result.Ok && data?.Data is not null)
        {
            _ = GenerateReportAsync(data.Data);
            return;
        }

        SetStatus(GetString(Resource.String.status_picker_cancelled));
    }

    private void OpenFitFilePicker()
    {
        SetOpenOutputButtonVisible(false);

        if (TryLaunchPicker(Intent.ActionOpenDocument, addOpenableCategory: true))
        {
            SetStatus(GetString(Resource.String.status_launching_picker));
            return;
        }

        if (TryLaunchPicker(Intent.ActionGetContent, addOpenableCategory: false))
        {
            SetStatus(GetString(Resource.String.status_picker_fallback));
            return;
        }

        SetStatus(GetString(Resource.String.status_no_file_manager));
    }

    private bool TryLaunchPicker(string action, bool addOpenableCategory)
    {
        try
        {
            var intent = new Intent(action);
            if (addOpenableCategory)
            {
                intent.AddCategory(Intent.CategoryOpenable);
            }

            intent.SetType("*/*");
            StartActivityForResult(intent, OpenDocumentRequestCode);
            return true;
        }
        catch (ActivityNotFoundException ex)
        {
            Android.Util.Log.Error(nameof(MainActivity), ex, "No file picker activity found.");
            return false;
        }
    }

    private bool ShouldAutoLaunchPicker()
    {
        var accessibilityManager = GetSystemService(AccessibilityService) as AccessibilityManager;
        if (accessibilityManager is null)
        {
            return false;
        }

        return !accessibilityManager.IsTouchExplorationEnabled;
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

            _generatedFiles = await Task.Run(() => _reportGenerator.RenderFromFit(inMemoryFit, outputDirectory));
            SetStatus(string.Format(GetString(Resource.String.status_success), _generatedFiles.Count));
            SetOpenOutputButtonVisible(_generatedFiles.Count > 0);
        }
        catch (Exception ex)
        {
            SetStatus(string.Format(GetString(Resource.String.status_error), ex.Message));
        }
    }

    private void OpenGeneratedImage()
    {
        try
        {
            // Open only the first page; if multiple pages were generated the user can tap again
            // after viewing to check the others, or a future update can add a gallery intent.
            var file = new Java.IO.File(_generatedFiles[0]);
            var authority = $"{PackageName}.fileprovider";
            var uri = FileProvider.GetUriForFile(this, authority, file);

            var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(uri, "image/png");
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            StartActivity(intent);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(nameof(MainActivity), ex, "Failed to open generated image.");
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

    private void SetOpenOutputButtonVisible(bool visible)
    {
        RunOnUiThread(() =>
        {
            if (_openOutputButton is not null)
            {
                _openOutputButton.Visibility = visible ? ViewStates.Visible : ViewStates.Gone;
            }
        });
    }
}