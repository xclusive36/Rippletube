# Rippletube

Rippletube is an admin-only Jellyfin plugin for downloading videos with `yt-dlp` and importing them into an existing Jellyfin library.

## Features

- Admin-only Jellyfin web UI
- `yt-dlp` and `ffmpeg` validation
- URL preview before download
- Single-video and capped playlist downloads
- Durable one-at-a-time queue with cancel, retry, progress, history, and compact logs
- Safe format presets instead of raw `yt-dlp` arguments
- Sidecar metadata: info JSON, thumbnails, descriptions, and subtitles when available
- Download archive support to avoid duplicate downloads
- Automatic Jellyfin library scan after successful downloads

## Minimum Requirements

- Jellyfin Server compatible with the plugin release target ABI
- `yt-dlp` installed on the Jellyfin server or inside the Jellyfin Docker container
- `ffmpeg` installed on the Jellyfin server or inside the Jellyfin Docker container
- A writable destination folder that is already included in a Jellyfin library

The .NET 9 SDK is only required for developers building from source.

`ffmpeg` is required even though Rippletube does not force full video recoding in v1. `yt-dlp` often needs `ffmpeg` to merge separate audio/video streams, remux containers, embed metadata, embed thumbnails, or extract audio.

## Storage Format

The default preset prefers Jellyfin-friendly MP4/H.264/AAC output when the source provides it. If a clean MP4 is not available, the plugin allows `yt-dlp` to save the best compatible available container, commonly MKV or WebM. Rippletube does not force expensive full-video transcoding in v1.

## Security Notes

Rippletube downloads arbitrary URLs from the Jellyfin server. For that reason, v1 is intentionally admin-only, blocks local/private-network URLs, validates destination paths, and does not expose raw `yt-dlp` arguments in the UI.

See:

- [Install guide](docs/INSTALL.md)
- [Usage guide](docs/USAGE.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Release guide](docs/RELEASING.md)

## Plugin Repository

After the first tagged release is published, users can add this repository URL in Jellyfin:

```text
https://raw.githubusercontent.com/xclusive36/Rippletube/main/manifest.json
```
