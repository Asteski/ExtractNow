# ExtractNow

ExtractNow is a portable WPF application that extracts archives instantly without displaying additional windows. It comes bundled with 7-Zip and provides self-contained builds for Windows x64 and ARM64.

![ExtractNow](https://raw.githubusercontent.com/Asteski/ExtractNow/refs/heads/main/img/img01.png)

## Features
- **Extracts to a sibling folder** named after the archive (e.g., `file.zip` → `file/`).
- **Resizable main window** with automatic size persistence (optional restore-to-default on restart).
- **Progress with live log** and confirmation prompt before cancel.
- **Drag & drop** or open via file association (first CLI arg is the archive path).
- **Settings window**:
  - Choose bundled or custom 7-Zip path (supports typing/pasting paths).
  - Configure tray icon with live progress indicator during extraction.
  - Control window visibility on association launch and file size threshold.
  - Optionally open extracted folder automatically on completion.
  - Optionally close the app after successful extraction.
  - Restore original window size on restart.
- **"Open extracted folder…"** button enabled after success.
- **Keyboard shortcuts** for quick access (Settings, Extract, Open Folder, Cancel, Exit).

## Requirements
- Windows 10/11 (x64 or ARM64)
- No separate .NET install required (self-contained portable build)

## Keyboard shortcuts
- ctrl + ,  → Settings
- ctrl + o  → Select archive
- ctrl + e  → Open extracted folder
- ctrl + c  → Cancel extraction
- ctrl + w  → Exit
- esc       → Close About or Settings window

## Bundled 7-Zip
The repo includes a `7zip/` directory that’s copied next to the executable at publish time. The app auto-detects `7z.exe` there. You can instead set a system 7-Zip path in Settings. When redistributing, keep the 7-Zip license/readme files intact.

## Notes
- Portable builds include the .NET runtime; no install required.
- If an archive is corrupt or unsupported, 7-Zip returns non-zero; the app surfaces this in the log.
