# ExtractNow

Tiny, portable WPF app that extracts archives using bundled 7-Zip. Ships self-contained builds for Windows x64 and ARM64.

## Features
- Extracts to a sibling folder named after the archive (e.g., `file.zip` → `file/`).
- Progress with live log and Cancel.
- Drag & drop, or open via file association (first CLI arg is the archive path).
- Settings: choose bundled/system 7-Zip, tray behavior, and display preferences.
- Optional: automatically open the extracted folder on completion.
- Optional: close the app after a successful extraction.
- “Open extracted folder…” button enabled after success.
- Keyboard shortcuts and a simple About dialog (shows version and owner).

## Requirements
Runtime (end users):
- Windows 10/11 (x64 or ARM64)
- No separate .NET install required (self-contained portable build)

For building from source:
- .NET 8 SDK

## Usage
- Run the app and drag & drop an archive or click Extract.
- To integrate with Explorer, use Settings to register per-user "Open with" for `.zip`, `.7z`, `.rar` (no default handler changes).

## Bundled 7-Zip
The repo includes a `7zip/` directory that’s copied next to the executable at publish time. The app auto-detects `7z.exe` there. You can instead set a system 7-Zip path in Settings. When redistributing, keep the 7-Zip license/readme files intact.

## Keyboard shortcuts
- Ctrl + ,  → Settings
- Ctrl + O  → Select archive
- Ctrl + E  → Open extracted folder
- Ctrl + C  → Cancel extraction
- Ctrl + W  → Exit
- Esc       → Close About window

## Notes
- Portable builds include the .NET runtime; no install required.
- If an archive is corrupt or unsupported, 7-Zip returns non-zero; the app surfaces this in the log.