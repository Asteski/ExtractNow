using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ExtractNow.Services
{
    public sealed class Extractor
    {
    private readonly SettingsService _settings;
        private static readonly Regex PercentRegex = new Regex(@"\b(\d{1,3})%\b", RegexOptions.Compiled);

        public Extractor(SettingsService settings)
        {
            _settings = settings;
        }

        public record Result(bool Success, string? ErrorMessage);

        public async Task<Result> ExtractAsync(
            string archivePath,
            string outputDir,
            IProgress<int>? progress,
            IProgress<string>? log,
            CancellationToken ct)
        {
            // Resolve 7-Zip location: custom path from settings or app-local 7zip folder
            string baseDir = AppContext.BaseDirectory;
            string folder = !string.IsNullOrWhiteSpace(_settings.SevenZipPath) ? _settings.SevenZipPath! : Path.Combine(baseDir, "7zip");
            string sevenZip = Path.Combine(folder, "7z.exe");
            if (!File.Exists(sevenZip))
            {
                // Fallback to 7zG.exe if present
                string sevenZipGui = Path.Combine(folder, "7zG.exe");
                if (File.Exists(sevenZipGui))
                {
                    sevenZip = sevenZipGui;
                }
                else
                {
                    return new Result(false, "7-Zip binaries not found. Select a valid 7-Zip folder in Settings, or restore default.");
                }
            }

            // Validate dependent DLL (7z.dll) exists in the same folder
            string sevenZipDll = Path.Combine(Path.GetDirectoryName(sevenZip)!, "7z.dll");
            if (!File.Exists(sevenZipDll))
            {
                return new Result(false, "7-Zip DLL (7z.dll) not found next to 7z.exe. Select a valid 7-Zip folder in Settings.");
            }

            // 7z.exe gives better console output than 7zG.exe
            var arguments = $"x \"{archivePath}\" -o\"{outputDir}\" -y -bsp1 -bso1";

            var psi = new ProcessStartInfo
            {
                FileName = sevenZip,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(archivePath) ?? Environment.CurrentDirectory
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            proc.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                log?.Report(e.Data);
                var m = PercentRegex.Match(e.Data);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var p))
                {
                    progress?.Report(Math.Clamp(p, 0, 100));
                }
            };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) log?.Report("ERR: " + e.Data); };
            proc.Exited += (s, e) => tcs.TrySetResult(proc.ExitCode);

            try
            {
                ct.ThrowIfCancellationRequested();
                if (!proc.Start())
                {
                    return new Result(false, "Failed to start 7-Zip process.");
                }
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                using (ct.Register(() => TryKill(proc)))
                {
                    var exitCode = await tcs.Task.ConfigureAwait(false);
                    if (exitCode == 0)
                    {
                        progress?.Report(100);
                        return new Result(true, null);
                    }
                    return new Result(false, $"7-Zip exited with code {exitCode}.");
                }
            }
            catch (OperationCanceledException)
            {
                TryKill(proc);
                throw;
            }
            catch (Exception ex)
            {
                return new Result(false, ex.Message);
            }
        }

        private static void TryKill(Process p)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(true);
                }
            }
            catch { }
        }
    }
}
