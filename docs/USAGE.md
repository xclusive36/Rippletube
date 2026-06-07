# Use Rippletube

## Configure

Open Jellyfin Admin Dashboard, then open the Rippletube plugin page.

Set:

- `yt-dlp path or command`: usually `yt-dlp`, `/usr/local/bin/yt-dlp`, or a container path
- `ffmpeg path or command`: usually `ffmpeg` or `/usr/bin/ffmpeg`
- `Destination library folder`: a folder already included in a Jellyfin library
- `Playlist item cap`: maximum playlist items per job
- `Minimum free space`: disk guardrail before downloads begin
- `Format preset`: safe yt-dlp format behavior
- `Naming template`: how completed files are arranged
- `Cookies file path`: optional server-side Netscape cookies file
- `Scan Jellyfin library`: enabled by default

Save the configuration, then run validation.

## Preview A URL

Paste a video or playlist URL and click Preview. Rippletube asks `yt-dlp` for metadata and shows the title, uploader/channel, duration, thumbnail, and playlist count when available.

Use preview before playlists so you can catch unexpectedly large inputs.

## Download A Single Video

1. Paste the URL.
2. Leave playlist mode off.
3. Click Submit.
4. Watch progress in the queue.

Rippletube passes `--no-playlist` for single-video jobs.

## Download A Playlist

1. Paste the playlist URL.
2. Enable playlist mode.
3. Confirm the configured playlist cap is appropriate.
4. Click Submit.

Rippletube passes a playlist item limit to `yt-dlp`.

## Format Presets

- Compatible MP4 preferred: prefers MP4/H.264/AAC when available, without forced full recoding
- Best available: lets `yt-dlp` choose the best available video/audio
- Audio only: downloads the best available audio
- Capped 1080p: limits video height to 1080p

## Naming Templates

- `Uploader / Title [id] / Title.ext`
- `Playlist / Index - Title [id].ext`
- `Title [id].ext`

The video ID is included to avoid collisions and make troubleshooting easier.

## Queue Controls

- Cancel stops a pending job or requests termination of the active `yt-dlp` process.
- Retry creates a new pending job with the same URL and presets.
- Logs show compact progress, stderr tail, and exit summary.

## Library Scan

When auto scan is enabled, Rippletube asks Jellyfin to refresh the library after a successful download. The destination folder should already belong to a Jellyfin library.
