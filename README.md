# ExtractNow

A tiny WPF app to extract archives using bundled 7-Zip components for quick extraction from Explorer. Now ships portable self-contained builds for Windows x64 and ARM64.

## Features
- Extract any supported archive to a subfolder with the same name next to the archive
- Progress bar with live 7-Zip output and cancel
- Drag & drop or launch via file association (passes archive path as first argument)
- Settings to choose 7-Zip executable path

## Requirements
Runtime (end users):
- Windows 10/11 (x64 or ARM64)
- No separate .NET install required (self-contained portable build)

For building from source:
- .NET 8 SDK
- (Optional) External system 7-Zip if you don't rely on the bundled copy

## Usage
- Run the app normally to open the UI, drag & drop an archive, or click Open Archive…
- Or, in Settings, register "Open with" for `.zip`, `.7z`, `.rar`. Then right-click an archive in Explorer and choose ExtractNow.

### Bundled 7-Zip
The repository includes a `7zip/` directory that is copied beside the executable on publish. The app will auto-detect `7z.exe` there. You may still point Settings to a separate 7-Zip installation if preferred.

## Notes
- Portable builds include the .NET runtime; no install required.
- If an archive is corrupt or unsupported, 7-Zip returns non-zero; the app surfaces this in the log.
- Architecture-specific builds (win-x64 / win-arm64) are functionally equivalent.
