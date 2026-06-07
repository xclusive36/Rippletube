#!/usr/bin/env python3
"""Generate Jellyfin plugin repository manifest.json for a release."""

from __future__ import annotations

import argparse
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path


PLUGIN = {
    "category": "General",
    "guid": "8f3d0a10-5ac8-4b6d-9983-b3495ebd2d81",
    "name": "Rippletube",
    "description": (
        "Adds an admin-only Jellyfin page for previewing videos and playlists with yt-dlp, "
        "queueing downloads, writing sidecar metadata, and scanning the configured library "
        "destination after successful downloads."
    ),
    "owner": "xclusive36",
    "overview": "Admin-only yt-dlp downloader and Jellyfin library importer.",
}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", required=True)
    parser.add_argument("--target-abi", default="10.11.3.0")
    parser.add_argument("--zip", required=True, type=Path)
    parser.add_argument("--source-url", required=True)
    parser.add_argument("--changelog", default="Release build.")
    parser.add_argument("--output", default="manifest.json", type=Path)
    args = parser.parse_args()

    checksum = hashlib.md5(args.zip.read_bytes()).hexdigest()
    entry = {
        **PLUGIN,
        "versions": [
            {
                "checksum": checksum,
                "changelog": args.changelog,
                "targetAbi": args.target_abi,
                "sourceUrl": args.source_url,
                "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
                "version": args.version,
            }
        ],
    }

    args.output.write_text(json.dumps([entry], indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
