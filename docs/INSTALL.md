# Install Rippletube

## Install From Jellyfin Plugin Repository

After the first release is published, add the Rippletube repository to Jellyfin:

```text
https://raw.githubusercontent.com/xclusive36/Rippletube/main/manifest.json
```

In Jellyfin:

1. Open Dashboard.
2. Go to Plugins.
3. Open Repositories.
4. Add the Rippletube manifest URL.
5. Open Catalog.
6. Install Rippletube.
7. Restart Jellyfin.

This is the recommended install path for normal users.

## Manual Install From Release Zip

1. Download the latest `Rippletube_VERSION.zip` from GitHub Releases.
2. Create a Jellyfin plugin folder named `Rippletube`.
3. Extract the zip into that folder.
4. Restart Jellyfin.

Common plugin roots:

- Linux package installs: `/var/lib/jellyfin/plugins/Rippletube/`
- Windows direct installs: `%UserProfile%\AppData\Local\jellyfin\plugins\Rippletube\`
- Windows tray installs: `%ProgramData%\Jellyfin\Server\plugins\Rippletube\`

## Build From Source

1. Install the .NET 9 SDK.
2. Clone or copy this repository.
3. Build the plugin:

```bash
dotnet build -c Release
```

4. Copy the built plugin DLL and related output files from:

```text
src/Jellyfin.Plugin.Rippletube/bin/Release/net9.0/
```

into a Jellyfin plugin folder named `Rippletube`.

Common plugin roots:

- Linux package installs: `/var/lib/jellyfin/plugins/Rippletube/`
- Windows direct installs: `%UserProfile%\AppData\Local\jellyfin\plugins\Rippletube\`
- Windows tray installs: `%ProgramData%\Jellyfin\Server\plugins\Rippletube\`

5. Restart Jellyfin.
6. Open the Jellyfin admin dashboard and confirm Rippletube appears in plugins.

## Install yt-dlp And ffmpeg

Rippletube does not bundle `yt-dlp` or `ffmpeg`. Install both where the Jellyfin server process can execute them.

Linux example:

```bash
python3 -m pip install -U yt-dlp
sudo apt-get install ffmpeg
```

Docker example using a custom image:

```dockerfile
FROM jellyfin/jellyfin:latest
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pip ffmpeg \
    && python3 -m pip install --break-system-packages -U yt-dlp \
    && rm -rf /var/lib/apt/lists/*
```

Inside Docker, paths must make sense inside the container, not on the host. If the host path is `/mnt/media/youtube`, the container might see it as `/media/youtube` depending on your volume mapping.

## File Permissions

The Jellyfin process user must be able to:

- execute `yt-dlp`
- execute `ffmpeg`
- read the optional cookies file
- write videos and sidecars into the destination folder
- write Rippletube queue and archive state under Jellyfin data storage

## Version Compatibility

Jellyfin plugins must reference Jellyfin package versions that match the target server version. This scaffold currently references Jellyfin `10.11.3` packages and targets `net9.0`. If your Jellyfin server uses a different plugin ABI, update the package versions before building.

## Release Maintainer Notes

The repository includes GitHub Actions for builds and releases.

- Every push and pull request to `main` runs restore, build, and tests.
- Pushing a tag like `v0.1.0.0` builds the plugin, creates `Rippletube_0.1.0.0.zip`, creates an MD5 checksum, publishes a GitHub Release, and updates root `manifest.json`.
- Jellyfin users should use the raw `manifest.json` URL from `main`.
