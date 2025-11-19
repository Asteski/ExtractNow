using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO.Compression;

namespace ExtractNow.Services
{
    [SupportedOSPlatform("windows")]
    public static class FileAssociations
    {
        private static readonly string[] KnownArchiveExtensions = new[]
        {
            ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".lz", ".lzma",
            ".cab", ".iso", ".wim", ".arj", ".lzh", ".z", ".tgz", ".tbz2", ".txz"
        };

        public enum RegistrationState
        {
            Success,
            AlreadyAssociated,
            Error
        }

        public readonly struct RegistrationResult
        {
            public RegistrationState State { get; }
            public string? Error { get; }
            public RegistrationResult(RegistrationState state, string? error)
            {
                State = state; Error = error;
            }
        }

        // Registers ExtractNow as an "Open with" option for the given extension under HKCU
        public static RegistrationResult RegisterOpenWith(string extension)
        {
            if (!extension.StartsWith('.')) extension = "." + extension;

            string appPath = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(AppContext.BaseDirectory, "ExtractNow.exe");
            string progId = GetProgIdForExtension(extension); // e.g., ExtractNow_zip
            string exeName = Path.GetFileName(appPath);

            try
            {
                // Ensure the application is registered so it appears in the Open With / Default Apps list
                using (var appKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\Applications\\{exeName}"))
                {
                    if (appKey != null)
                    {
                        appKey.SetValue("FriendlyAppName", "ExtractNow");
                        using (var cmd = appKey.CreateSubKey("shell\\open\\command"))
                        {
                            cmd?.SetValue(null, $"\"{appPath}\" \"%1\"");
                        }
                        // Advertise supported extension so it shows in Windows chooser for that file type
                        using (var supported = appKey.CreateSubKey("SupportedTypes"))
                        {
                            supported?.SetValue(extension, string.Empty);
                        }
                    }
                }

                // Check existing association
                bool inOpenWith = false;
                using (var ext = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{extension}\\OpenWithProgids"))
                {
                    if (ext != null)
                    {
                        foreach (var name in ext.GetValueNames())
                        {
                            if (string.Equals(name, progId, StringComparison.Ordinal))
                            {
                                inOpenWith = true;
                                break;
                            }
                        }
                    }
                }

                string expectedCmd = $"\"{appPath}\" \"%1\"";
                string? existingCmd = null;
                using (var cmd = Registry.CurrentUser.OpenSubKey($"Software\\Classes\\{progId}\\shell\\open\\command"))
                {
                    existingCmd = cmd?.GetValue(null) as string;
                }

                // Check if we are already the default handler
                string? userChoiceProgId = null;
                using (var uc = Registry.CurrentUser.OpenSubKey($"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\{extension}\\UserChoice"))
                {
                    userChoiceProgId = uc?.GetValue("ProgId") as string;
                }
                string? defaultProgId = null;
                using (var dotExt = Registry.CurrentUser.OpenSubKey($"Software\\Classes\\{extension}"))
                {
                    defaultProgId = dotExt?.GetValue(null) as string;
                }

                bool weAreDefault = string.Equals(userChoiceProgId, progId, StringComparison.OrdinalIgnoreCase)
                                     || (string.IsNullOrEmpty(userChoiceProgId) && string.Equals(defaultProgId, progId, StringComparison.OrdinalIgnoreCase));

                bool isAlreadyAssociated = weAreDefault || (inOpenWith && string.Equals(existingCmd, expectedCmd, StringComparison.OrdinalIgnoreCase));
                if (isAlreadyAssociated)
                {
                    return new RegistrationResult(RegistrationState.AlreadyAssociated, null);
                }

                // Create/update a per-extension ProgID so Windows can use it (this enables proper Type column text)
                CreateOrUpdatePerExtensionProgId(extension, appPath, expectedCmd);

                // Ensure we're listed as an OpenWith ProgId for the extension
                using (var ext = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{extension}\\OpenWithProgids"))
                {
                    ext?.SetValue(progId, Array.Empty<byte>(), RegistryValueKind.None);
                }

                // If there is no explicit UserChoice set by the user, set the default handler under HKCU for this extension.
                // Windows may still honor UserChoice over this; this only helps when no choice exists.
                if (string.IsNullOrEmpty(userChoiceProgId))
                {
                    using (var dotExt = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{extension}"))
                    {
                        var currentDefault = dotExt?.GetValue(null) as string;
                        if (string.IsNullOrEmpty(currentDefault))
                        {
                            dotExt?.SetValue(null, progId);
                        }
                    }
                }
                return new RegistrationResult(RegistrationState.Success, null);
            }
            catch (Exception ex)
            {
                return new RegistrationResult(RegistrationState.Error, ex.Message);
            }
        }

        // Ensure per-extension ProgIDs (with proper friendly names) exist so the Explorer Type column looks right
        public static void EnsureAssociationMetadataRegistered()
        {
            string appPath = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(AppContext.BaseDirectory, "ExtractNow.exe");
            string exeName = Path.GetFileName(appPath);
            string expectedCmd = $"\"{appPath}\" \"%1\"";

            try
            {
                // Ensure Applications\<exe> registration and SupportedTypes are present
                using (var appKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\Applications\\{exeName}"))
                {
                    if (appKey != null)
                    {
                        appKey.SetValue("FriendlyAppName", "ExtractNow");
                        using (var icon = appKey.CreateSubKey("DefaultIcon"))
                        {
                            icon?.SetValue(null, $"{appPath},0");
                        }
                        using (var cmd = appKey.CreateSubKey("shell\\open\\command"))
                        {
                            cmd?.SetValue(null, expectedCmd);
                        }
                        using (var supported = appKey.CreateSubKey("SupportedTypes"))
                        {
                            foreach (var ext in KnownArchiveExtensions)
                                supported?.SetValue(ext, string.Empty);
                        }
                    }
                }

                foreach (var ext in KnownArchiveExtensions)
                {
                    CreateOrUpdatePerExtensionProgId(ext, appPath, expectedCmd);
                    using (var openWith = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{ext}\\OpenWithProgids"))
                    {
                        openWith?.SetValue(GetProgIdForExtension(ext), Array.Empty<byte>(), RegistryValueKind.None);
                    }
                }
            }
            catch { /* best-effort */ }
            finally
            {
                try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); } catch { }
            }
        }

        // Remove HKCU registry entries that this app created for file associations
        // Returns the number of items removed
        public static int CleanupAssociationRegistryEntries()
        {
            int removed = 0;
            string appPath = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(AppContext.BaseDirectory, "ExtractNow.exe");
            string exeName = Path.GetFileName(appPath);

            try
            {
                // Remove from Applications\<exe>\SupportedTypes
                // Remove the Applications\<exe> key entirely so the app no longer appears in Open With > More options
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree($"Software\\Classes\\Applications\\{exeName}", throwOnMissingSubKey: false);
                    removed++;
                }
                catch { }

                // Remove per-extension OpenWithProgids and our ProgID keys
                foreach (var ext in KnownArchiveExtensions)
                {
                    string progId = GetProgIdForExtension(ext);
                    using (var openWith = Registry.CurrentUser.OpenSubKey($"Software\\Classes\\{ext}\\OpenWithProgids", writable: true))
                    {
                        if (openWith != null && Array.Exists(openWith.GetValueNames(), n => string.Equals(n, progId, StringComparison.Ordinal)))
                        {
                            try { openWith.DeleteValue(progId, false); removed++; } catch { }
                        }
                    }
                    try
                    {
                        Registry.CurrentUser.DeleteSubKeyTree($"Software\\Classes\\{progId}", throwOnMissingSubKey: false);
                        // Count approximate deletions
                    }
                    catch { }
                }

                // Also remove any stray ProgIDs starting with ExtractNow_ to be thorough
                using (var classes = Registry.CurrentUser.OpenSubKey("Software\\Classes"))
                {
                    if (classes != null)
                    {
                        foreach (var name in classes.GetSubKeyNames())
                        {
                            if (name.StartsWith("ExtractNow_", StringComparison.Ordinal))
                            {
                                try { Registry.CurrentUser.DeleteSubKeyTree($"Software\\Classes\\{name}", false); removed++; } catch { }
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); } catch { }
            }

            return removed;
        }

        // Set ExtractNow as the default handler for an extension
        // Due to Windows 10/11 UserChoice protection, this registers the app and opens a dialog for user confirmation
        public static RegistrationResult SetAsDefaultHandler(string extension)
        {
            if (!extension.StartsWith('.')) extension = "." + extension;

            string appPath = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(AppContext.BaseDirectory, "ExtractNow.exe");
            string progId = GetProgIdForExtension(extension);
            string expectedCmd = $"\"{appPath}\" \"%1\"";
            string exeName = Path.GetFileName(appPath);

            try
            {
                // Ensure the application is registered
                using (var appKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\Applications\\{exeName}"))
                {
                    if (appKey != null)
                    {
                        appKey.SetValue("FriendlyAppName", "ExtractNow");
                        using (var icon = appKey.CreateSubKey("DefaultIcon"))
                        {
                            icon?.SetValue(null, $"{appPath},0");
                        }
                        using (var cmd = appKey.CreateSubKey("shell\\open\\command"))
                        {
                            cmd?.SetValue(null, expectedCmd);
                        }
                        using (var supported = appKey.CreateSubKey("SupportedTypes"))
                        {
                            supported?.SetValue(extension, string.Empty);
                        }
                    }
                }

                // Create or update the ProgID
                CreateOrUpdatePerExtensionProgId(extension, appPath, expectedCmd);

                // Set as OpenWithProgids
                using (var openWith = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{extension}\\OpenWithProgids"))
                {
                    openWith?.SetValue(progId, Array.Empty<byte>(), RegistryValueKind.None);
                }

                // Set the default ProgID for this extension (this may be overridden by UserChoice)
                using (var dotExt = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{extension}"))
                {
                    dotExt?.SetValue(null, progId);
                }

                // Notify the shell about the change
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

                // Due to Windows UserChoice protection, we need user interaction
                // Open the file association dialog
                try
                {
                    string tempFile = CreateTempAssocDummyFile(extension);
                    
                    // Use the openas verb to trigger the association dialog
                    var psi = new ProcessStartInfo
                    {
                        FileName = tempFile,
                        Verb = "openas",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    
                    // Clean up temp file after delay
                    new Thread(() => 
                    { 
                        try 
                        { 
                            Thread.Sleep(20000); 
                            if (File.Exists(tempFile)) 
                                File.Delete(tempFile); 
                        } 
                        catch { } 
                    }) { IsBackground = true }.Start();
                }
                catch
                {
                    // If openas fails, user needs to do it manually
                    return new RegistrationResult(
                        RegistrationState.AlreadyAssociated,
                        "manual_required"
                    );
                }

                return new RegistrationResult(RegistrationState.Success, null);
            }
            catch (Exception ex)
            {
                return new RegistrationResult(RegistrationState.Error, ex.Message);
            }
        }

        // Set file association using UserChoice hash (Windows 10/11 compatible)
        public static RegistrationResult SetAsDefaultWithHash(string extension)
        {
            if (!extension.StartsWith('.')) extension = "." + extension;

            string appPath = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(AppContext.BaseDirectory, "ExtractNow.exe");
            string progId = GetProgIdForExtension(extension);
            string expectedCmd = $"\"{appPath}\" \"%1\"";

            try
            {
                // First, register the app properly
                var regResult = RegisterOpenWith(extension);
                if (regResult.State == RegistrationState.Error)
                {
                    return regResult;
                }

                // Get user SID and generate hash
                string userSid = UserChoiceHash.GetCurrentUserSid();
                if (string.IsNullOrEmpty(userSid))
                {
                    return new RegistrationResult(RegistrationState.Error, "Failed to get user SID");
                }

                // Round timestamp to nearest minute for hash generation
                DateTime now = DateTime.Now;
                DateTime timestamp = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Local);
                
                // Generate the UserChoice hash
                string hash = UserChoiceHash.Generate(extension, progId, userSid, timestamp);
                
                // Delete existing UserChoice key if it exists
                string userChoicePath = $"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\{extension}\\UserChoice";
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(userChoicePath, false);
                }
                catch { }

                // Write new UserChoice with hash
                using (var key = Registry.CurrentUser.CreateSubKey(userChoicePath))
                {
                    if (key == null)
                    {
                        return new RegistrationResult(RegistrationState.Error, "Failed to create UserChoice key");
                    }
                    
                    key.SetValue("Hash", hash);
                    key.SetValue("ProgId", progId);
                }

                // Notify shell of changes
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

                return new RegistrationResult(RegistrationState.Success, null);
            }
            catch (Exception ex)
            {
                return new RegistrationResult(RegistrationState.Error, ex.Message);
            }
        }

        private static string GetProgIdForExtension(string extension)
        {
            if (!extension.StartsWith('.')) extension = "." + extension;
            return $"ExtractNow{extension.Replace('.', '_')}";
        }

        private static void CreateOrUpdatePerExtensionProgId(string extension, string appPath, string expectedCmd)
        {
            string progId = GetProgIdForExtension(extension);
            string friendly = extension.TrimStart('.').ToUpperInvariant() + " file";
            using (var k1 = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{progId}"))
            {
                if (k1 == null) return;
                // Friendly type name used for Explorer Type column when our ProgID is chosen
                k1.SetValue(null, friendly);
                using (var icon = k1.CreateSubKey("DefaultIcon"))
                {
                    icon?.SetValue(null, $"{appPath},0");
                }
                using (var cmd = k1.CreateSubKey("shell\\open\\command"))
                {
                    cmd?.SetValue(null, expectedCmd);
                }
            }
        }

        // Show the Windows compact chooser using classic OpenAs for the given extension
        public static void OpenDefaultAppsUIForExtension(string extension)
        {
            if (!extension.StartsWith('.')) extension = "." + extension;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32.dll,OpenAs_RunDLL {extension}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
            catch { /* ignore */ }
        }

        // Legacy compact chooser path for environments where the ms-settings sheet is suppressed
        public static void OpenClassicChooserForExtension(string extension)
        {
            if (!extension.StartsWith('.')) extension = "." + extension;
            // Try SHOpenWithDialog first
            try
            {
                string tempFile = CreateTempAssocDummyFile(extension);
                var info = new OPENASINFO
                {
                    pcszFile = tempFile,
                    pcszClass = null,
                    oaifInFlags = OpenAsInfoFlags.OAIF_ALLOW_REGISTRATION | OpenAsInfoFlags.OAIF_REGISTER_EXT
                };
                int hr = SHOpenWithDialog(IntPtr.Zero, ref info);
                if (hr == 0)
                {
                    new Thread(() => { try { Thread.Sleep(15000); if (File.Exists(tempFile)) File.Delete(tempFile); } catch { } }) { IsBackground = true }.Start();
                    return;
                }
            }
            catch { }

            // Fallback to openas verb
            try
            {
                string tempFile = CreateTempAssocDummyFile(extension);
                var openAs = new ProcessStartInfo { FileName = tempFile, Verb = "openas", UseShellExecute = true };
                Process.Start(openAs);
                new Thread(() => { try { Thread.Sleep(15000); if (File.Exists(tempFile)) File.Delete(tempFile); } catch { } }) { IsBackground = true }.Start();
                return;
            }
            catch { }

            // Rundll32 fallback
            try
            {
                string tempFile = CreateTempAssocDummyFile(extension);
                var openWith = new ProcessStartInfo { FileName = "rundll32.exe", Arguments = $"shell32.dll,OpenAs_RunDLL \"{tempFile}\"", UseShellExecute = false, CreateNoWindow = true };
                Process.Start(openWith);
                new Thread(() => { try { Thread.Sleep(15000); if (File.Exists(tempFile)) File.Delete(tempFile); } catch { } }) { IsBackground = true }.Start();
            }
            catch { }
        }

        private static string CreateTempAssocDummyFile(string extension)
        {
            string baseDir = AppContext.BaseDirectory;
            string tempFile = Path.Combine(baseDir, $"ExtractNow_AssocDummy{extension}");
            if (File.Exists(tempFile)) return tempFile;
            try
            {
                if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true))
                    {
                        // Create a tiny empty zip (valid archive)
                    }
                }
                else
                {
                    // For other extensions, zero-byte is usually sufficient to trigger the chooser
                    File.WriteAllBytes(tempFile, Array.Empty<byte>());
                }
            }
            catch
            {
                // As a last resort, attempt a zero-byte file
                try { File.WriteAllBytes(tempFile, Array.Empty<byte>()); } catch { /* ignore */ }
            }
            return tempFile;
        }

        // P/Invoke for SHOpenWithDialog to reliably show the compact chooser
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OPENASINFO
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pcszFile;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string? pcszClass;

            public OpenAsInfoFlags oaifInFlags;
        }

        [Flags]
        private enum OpenAsInfoFlags : uint
        {
            OAIF_ALLOW_REGISTRATION = 0x00000001,
            OAIF_REGISTER_EXT = 0x00000002,
            OAIF_EXEC = 0x00000004,
            OAIF_FORCE_REGISTRATION = 0x00000008,
            OAIF_HIDE_REGISTRATION = 0x00000020,
            OAIF_URL_PROTOCOL = 0x00000040,
            OAIF_FILE_IS_URI = 0x00000080
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHOpenWithDialog(IntPtr hwndParent, ref OPENASINFO info);

        // Notify Explorer about association/icon changes so it refreshes icons
        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
