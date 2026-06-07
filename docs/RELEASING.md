# Releasing Rippletube

## One-Time Setup

In GitHub repository settings:

1. Open Settings.
2. Open Actions, then General.
3. Under Workflow permissions, allow read and write permissions.
4. Save.

The release workflow needs write permission so it can create a GitHub Release and update `manifest.json` on `main`.

## Create A Release

Use a four-part Jellyfin-style plugin version:

```bash
git tag v0.1.0.1
git push origin v0.1.0.1
```

The release workflow will:

- restore, build, and test the solution
- publish the plugin
- zip the published plugin files
- create an MD5 checksum
- generate `manifest.json`
- create a GitHub Release with the zip, checksum, and manifest
- commit the updated root `manifest.json` back to `main`

## Plugin Repository URL

Users install from this Jellyfin repository URL:

```text
https://raw.githubusercontent.com/xclusive36/Rippletube/main/manifest.json
```

The manifest is populated by the latest tagged release.

## Version Compatibility

The current release workflow and `build.yaml` target Jellyfin ABI `10.10.7.0` and .NET `net8.0`. If a future Jellyfin server version changes plugin ABI requirements, update:

- `build.yaml`
- `.github/workflows/release.yml`
- `src/Jellyfin.Plugin.Rippletube/Jellyfin.Plugin.Rippletube.csproj`
