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

## Create a release keystore

> **Important:** Google Play requires packages to be signed with a release keystore.  
> Without explicit signing parameters, the .NET Android toolchain uses the debug keystore even in Release configuration, causing Google Play to reject the upload as a debug build.

If you do not already have a release keystore, create one with `keytool` (bundled with Java):

```bash
keytool -genkey -v \
  -keystore release.keystore \
  -alias wg84fitfileparser \
  -keyalg RSA \
  -keysize 2048 \
  -validity 10000
```

Store the resulting `release.keystore` file securely — **do not commit it to source control**.

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

   > Important: do not add a trailing slash or backslash after `net9.0-android`.  
   > `-f net9.0-android\` is invalid and causes `NETSDK1013`.

3. Generate signed APK:

   ```bash
   dotnet publish src/FitFileParser.AndroidApp/FitFileParser.AndroidApp.csproj \
     -c Release -f net9.0-android \
     -p:AndroidPackageFormat=apk \
     -p:AndroidKeyStore=true \
     -p:AndroidSigningKeyStore="C:/path/to/release.keystore" \
     -p:AndroidSigningKeyAlias=wg84fitfileparser \
     -p:AndroidSigningKeyPass=<key-password> \
     -p:AndroidSigningStorePass=<store-password>
   ```

4. Generate signed AAB (required for Google Play):

   ```bash
   dotnet publish src/FitFileParser.AndroidApp/FitFileParser.AndroidApp.csproj \
     -c Release -f net9.0-android \
     -p:AndroidPackageFormat=aab \
     -p:AndroidKeyStore=true \
     -p:AndroidSigningKeyStore="C:/path/to/release.keystore" \
     -p:AndroidSigningKeyAlias=wg84fitfileparser \
     -p:AndroidSigningKeyPass=<key-password> \
     -p:AndroidSigningStorePass=<store-password>
   ```

Replace `C:/path/to/release.keystore`, `my-key-alias`, `<key-password>`, and `<store-password>` with the values you used when creating the keystore.

> **Windows / Git Bash (MINGW64) note:** Do **not** use a Unix-style path like `/path/to/release.keystore` for `AndroidSigningKeyStore` when running from Git Bash.  
> Git Bash automatically converts leading-slash paths into Windows paths relative to the Git installation directory (e.g. `C:/Program Files/Git/path/to/release.keystore`), which does not exist and causes error `XA4310`.  
> Always supply a full Windows path such as `C:/Users/you/release.keystore` (forward or back slashes both work), or disable path conversion for that argument by prefixing the command with `MSYS_NO_PATHCONV=1`.

## Notes about framework support warnings

When using .NET 10 SDK with `net9.0-android`, you may see warning `NETSDK1202` indicating the workload is out of support.  
This warning does not prevent restore/build/publish, but you should plan to move to a supported Android target framework.

## Output locations

Generated package files are created under:

- `src/FitFileParser.AndroidApp/bin/Release/net9.0-android/publish/`

Look for:

- `*.apk`
- `*.aab`

## Generate packages in GitHub Actions

A workflow is included at:

- `.github/workflows/android-package.yml`

Before running the workflow, add the following **repository secrets** (Settings → Secrets and variables → Actions):

| Secret name | Value |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | Base64-encoded release keystore: `base64 -w 0 release.keystore` |
| `ANDROID_KEY_ALIAS` | Key alias used when creating the keystore |
| `ANDROID_KEY_PASSWORD` | Password for the key entry |
| `ANDROID_STORE_PASSWORD` | Password for the keystore file |

Run the workflow manually from the **Actions** tab (`Android Package` workflow).  
It uploads signed APK and AAB files as workflow artifacts.
