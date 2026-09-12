"""
Google Play Developer API automation for Momentary Momentos (com.momentarymementos.app).

Setup (one-time, manual, in your browser):
  1. Play Console -> Setup -> API access -> "Choose a project to link" (or create one).
  2. In the linked Google Cloud project, IAM & Admin -> Service Accounts -> Create service account.
  3. Create a JSON key for it and download it.
  4. Back in Play Console -> API access, find the service account and "Grant access".
     Give it at least: "View app information", "Manage production/testing releases".
  5. Save the downloaded key as scripts/play-service-account.json (already gitignored)
     or point PLAY_SERVICE_ACCOUNT_JSON at wherever you stored it.

Install dependencies:
  pip install -r scripts/requirements.txt

Usage:
  python scripts/play_publish.py list-tracks
  python scripts/play_publish.py upload --track internal --aab path\\to\\app.aab --notes "Bug fixes"
  python scripts/play_publish.py promote --from internal --to closed
"""

import argparse
import os
import sys

from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

PACKAGE_NAME = "com.momentarymementos.app"
SCOPES = ["https://www.googleapis.com/auth/androidpublisher"]
DEFAULT_KEY_PATH = os.path.join(os.path.dirname(__file__), "play-service-account.json")


def get_service():
    key_path = os.environ.get("PLAY_SERVICE_ACCOUNT_JSON", DEFAULT_KEY_PATH)
    if not os.path.exists(key_path):
        sys.exit(
            f"Service account key not found at {key_path}.\n"
            "Set PLAY_SERVICE_ACCOUNT_JSON or save the key to scripts/play-service-account.json."
        )
    creds = service_account.Credentials.from_service_account_file(key_path, scopes=SCOPES)
    return build("androidpublisher", "v3", credentials=creds)


def list_tracks(_args):
    service = get_service()
    edit = service.edits().insert(packageName=PACKAGE_NAME, body={}).execute()
    edit_id = edit["id"]
    try:
        tracks = service.edits().tracks().list(packageName=PACKAGE_NAME, editId=edit_id).execute()
        for track in tracks.get("tracks", []):
            print(f"Track: {track['track']}")
            for release in track.get("releases", []):
                codes = release.get("versionCodes", [])
                status = release.get("status")
                notes = release.get("releaseNotes", [])
                note_text = notes[0]["text"] if notes else ""
                print(f"  status={status} versionCodes={codes} notes={note_text!r}")
    finally:
        # Read-only inspection; discard the draft edit without committing.
        service.edits().delete(packageName=PACKAGE_NAME, editId=edit_id).execute()


def upload(args):
    service = get_service()
    edit = service.edits().insert(packageName=PACKAGE_NAME, body={}).execute()
    edit_id = edit["id"]

    bundle = (
        service.edits()
        .bundles()
        .upload(
            packageName=PACKAGE_NAME,
            editId=edit_id,
            media_body=MediaFileUpload(args.aab, mimetype="application/octet-stream"),
        )
        .execute()
    )
    version_code = bundle["versionCode"]
    print(f"Uploaded bundle as versionCode {version_code}")

    track_body = {
        "releases": [
            {
                "versionCodes": [version_code],
                "status": "completed",
                "releaseNotes": [{"language": "en-US", "text": args.notes}] if args.notes else [],
            }
        ]
    }
    service.edits().tracks().update(
        packageName=PACKAGE_NAME, editId=edit_id, track=args.track, body=track_body
    ).execute()

    result = service.edits().commit(packageName=PACKAGE_NAME, editId=edit_id).execute()
    print(f"Committed edit {result['id']} -> track '{args.track}'")


def promote(args):
    service = get_service()
    edit = service.edits().insert(packageName=PACKAGE_NAME, body={}).execute()
    edit_id = edit["id"]

    source = (
        service.edits()
        .tracks()
        .get(packageName=PACKAGE_NAME, editId=edit_id, track=args.from_track)
        .execute()
    )
    releases = source.get("releases", [])
    if not releases:
        service.edits().delete(packageName=PACKAGE_NAME, editId=edit_id).execute()
        sys.exit(f"No releases found on track '{args.from_track}'")

    service.edits().tracks().update(
        packageName=PACKAGE_NAME,
        editId=edit_id,
        track=args.to_track,
        body={"releases": releases},
    ).execute()

    result = service.edits().commit(packageName=PACKAGE_NAME, editId=edit_id).execute()
    print(f"Committed edit {result['id']}: promoted '{args.from_track}' -> '{args.to_track}'")


def main():
    parser = argparse.ArgumentParser(description="Play Store release automation")
    sub = parser.add_subparsers(required=True)

    p_list = sub.add_parser("list-tracks", help="Show current releases on each track")
    p_list.set_defaults(func=list_tracks)

    p_upload = sub.add_parser("upload", help="Upload an AAB to a track")
    p_upload.add_argument("--track", required=True, help="e.g. internal, closed, production")
    p_upload.add_argument("--aab", required=True, help="Path to the signed .aab file")
    p_upload.add_argument("--notes", default="", help="Release notes (en-US)")
    p_upload.set_defaults(func=upload)

    p_promote = sub.add_parser("promote", help="Promote the current release from one track to another")
    p_promote.add_argument("--from", dest="from_track", required=True)
    p_promote.add_argument("--to", dest="to_track", required=True)
    p_promote.set_defaults(func=promote)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
