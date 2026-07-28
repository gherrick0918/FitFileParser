# Android Package Generation

This repository already contains an Android app project at:

- `src/FitFileParser.AndroidApp/FitFileParser.AndroidApp.csproj`

Use the steps below to generate installable Android packages.

## Prerequisites

1. Install [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9).
2. Install Java 17 (required by Android tooling).
3. Install Android workload:

   ```bash
   dotnet workload install android
   ```

## Generate packages locally

From repository root:

1. Restore:

   ```bash
   dotnet restore src/FitFileParser.AndroidApp/FitFileParser.AndroidApp.csproj
   ```

2. Build release app:

   ```bash
   dotnet build src/FitFileParser.AndroidApp/FitFileParser.AndroidApp.csproj -c Release -f net9.0-android
   ```

3. Generate APK:

   ```bash
   dotnet publish src/FitFileParser.AndroidApp/FitFileParser.AndroidApp.csproj -c Release -f net9.0-android -p:AndroidPackageFormat=apk
   ```

4. Generate AAB:

   ```bash
   dotnet publish src/FitFileParser.AndroidApp/FitFileParser.AndroidApp.csproj -c Release -f net9.0-android -p:AndroidPackageFormat=aab
   ```

## Output locations

Generated package files are created under:

- `src/FitFileParser.AndroidApp/bin/Release/net9.0-android/publish/`

Look for:

- `*.apk`
- `*.aab`

## Generate packages in GitHub Actions

A workflow is included at:

- `.github/workflows/android-package.yml`

Run it manually from the **Actions** tab (`Android Package` workflow).  
It uploads APK and AAB files as workflow artifacts.
