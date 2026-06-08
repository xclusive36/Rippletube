# Troubleshooting

## Plugin Does Not Appear

- Confirm the DLL is in its own `Rippletube` folder under Jellyfin's plugin directory.
- Confirm Jellyfin was restarted.
- Confirm the plugin package versions match your Jellyfin server version.
- Check Jellyfin logs for `NotSupported` or assembly load errors.

## yt-dlp Not Found

- Install `yt-dlp` on the same machine or container where Jellyfin runs.
- Use an absolute path if `yt-dlp` is not on Jellyfin's `PATH`.
- In Docker, exec into the container and run `yt-dlp --version`.

## ffmpeg Not Found

- Install `ffmpeg` on the same machine or container where Jellyfin runs.
- Use an absolute path if needed.
- In Docker, exec into the container and run `ffmpeg -version`.

## Permission Denied Writing To Library

- Confirm the destination path is visible from the Jellyfin process.
- Confirm Jellyfin has write permission to the destination folder.
- Confirm Jellyfin can create subfolders inside the destination folder. Rippletube's default naming template creates uploader/title subdirectories.
- In Docker, confirm the volume is mounted read-write.
- Confirm the path configured in Rippletube is the container path, not the host-only path.

Example Linux package install:

```bash
sudo chown -R jellyfin:jellyfin /path/to/destination
sudo chmod -R u+rwX /path/to/destination
```

If the media directory is shared with another user or service, prefer a shared media group instead of making everything world-writable.

## Playlist Too Large

- Preview the URL to inspect playlist count.
- Lower or raise the playlist item cap in configuration.
- Keep caps conservative to avoid filling disk unexpectedly.

## Cookies File Issues

- Use a server-side cookies file path.
- Do not paste cookie contents into the UI.
- Confirm Jellyfin can read the file.
- In Docker, mount the cookies file into the container and use the container path.

## Downloaded Media Does Not Appear

- Confirm the destination folder is already part of a Jellyfin library.
- Confirm auto scan is enabled, or manually scan the library.
- Confirm the file extension is one Jellyfin recognizes.
- Check queue logs for failed merge, remux, or permission errors.

## Docker Path Mismatch

Host paths and container paths differ. If your compose file maps:

```yaml
volumes:
  - /mnt/media/youtube:/media/youtube
```

then Rippletube should use `/media/youtube`, not `/mnt/media/youtube`.
