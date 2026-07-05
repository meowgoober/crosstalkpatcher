using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace CrossTalkPatcher;

public static class Updater
{
    private static readonly HttpClient Client = new();

    static Updater()
    {
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("CrossTalkPatcher-Updater");
    }

    public static void CheckForUpdates()
    {
        try
        {
            // Clean up any old update leftovers from previous runs
            string currentExe = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(currentExe))
            {
                string oldExe = currentExe + ".old";
                if (File.Exists(oldExe))
                {
                    try { File.Delete(oldExe); } catch {}
                }
            }

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (currentVersion == null) return;

            // Skip checking for updates on default local debug builds (1.0.0)
            if (currentVersion.Major == 1 && currentVersion.Minor == 0 && currentVersion.Build == 0 && currentVersion.Revision == 0)
            {
                return;
            }

            // GitHub releases API
            string response = Client.GetStringAsync("https://api.github.com/repos/meowgoober/crosstalkpatcher/releases/latest")
                .GetAwaiter().GetResult();

            var root = JObject.Parse(response);

            var tagToken = root["tag_name"];
            if (tagToken == null) return;
            string tag = tagToken.Value<string>() ?? "";
            string latestVerStr = tag.TrimStart('v');

            if (!Version.TryParse(latestVerStr, out var latestVersion)) return;

            // Compare versions
            if (latestVersion > currentVersion)
            {
                Console.WriteLine();
                Console.WriteLine("====================================================");
                Console.WriteLine($"  Update Available: v{latestVersion} (Current: v{currentVersion.ToString(3)})");
                Console.WriteLine("====================================================");
                Console.Write("Would you like to auto-update now? [Y/n]: ");
                string? input = Console.ReadLine()?.Trim().ToLower();
                if (input == "n") return;

                // Find zip asset
                string downloadUrl = "";
                string expectedAssetSuffix = Environment.Is64BitProcess ? "-win-x64.zip" : "-win-x86.zip";
                if (root["assets"] is JArray assetsProp)
                {
                    foreach (var asset in assetsProp)
                    {
                        string name = asset["name"]?.Value<string>() ?? "";
                        if (name.EndsWith(expectedAssetSuffix, StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset["browser_download_url"]?.Value<string>() ?? "";
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Console.WriteLine($"Could not find the {expectedAssetSuffix} asset in the latest release.");
                    Thread.Sleep(2000);
                    return;
                }

                PerformUpdate(downloadUrl, currentExe);
            }
        }
        catch (Exception ex)
        {
            // Fail silently so it doesn't crash the patcher if offline
            #if DEBUG
            Console.WriteLine($"Updater check failed: {ex.Message}");
            #else
            _ = ex;
            #endif
        }
    }

    private static void PerformUpdate(string downloadUrl, string currentExePath)
    {
        try
        {
            Console.WriteLine("Downloading update...");
            string tempDir = Path.Combine(Path.GetTempPath(), "CrossTalkPatcherUpdate");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            string zipPath = Path.Combine(tempDir, "update.zip");

            // Download zip
            using (var response = Client.GetAsync(downloadUrl).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                using var fs = File.Create(zipPath);
                response.Content.CopyToAsync(fs).GetAwaiter().GetResult();
            }

            Console.WriteLine("Extracting files...");
            string extractPath = Path.Combine(tempDir, "extracted");
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            // Find new executable and python script in extracted folder
            string[] files = Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories);
            string? newExe = null;
            string? newPy = null;

            foreach (var f in files)
            {
                string name = Path.GetFileName(f);
                if (name.Equals("CrossTalkPatcher.exe", StringComparison.OrdinalIgnoreCase))
                    newExe = f;
                else if (name.Equals("add_import.py", StringComparison.OrdinalIgnoreCase))
                    newPy = f;
            }

            if (newExe == null)
            {
                Console.WriteLine("Update failed: CrossTalkPatcher.exe not found in release archive.");
                Thread.Sleep(2000);
                return;
            }

            Console.WriteLine("Replacing files...");
            string appDir = Path.GetDirectoryName(currentExePath) ?? AppContext.BaseDirectory;

            // Rename running executable
            string oldExePath = currentExePath + ".old";
            if (File.Exists(oldExePath)) File.Delete(oldExePath);
            File.Move(currentExePath, oldExePath);

            // Copy new executable
            File.Copy(newExe, currentExePath, overwrite: true);

            // Copy python script if present
            if (newPy != null)
            {
                string destPy = Path.Combine(appDir, "add_import.py");
                File.Copy(newPy, destPy, overwrite: true);
            }

            Console.WriteLine("Update complete! Restarting patcher...");
            Thread.Sleep(1000);

            // Start the new process
            Process.Start(new ProcessStartInfo(currentExePath) { UseShellExecute = true });
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update failed: {ex.Message}");
            Console.WriteLine("Reverting files...");
            try
            {
                string oldExePath = currentExePath + ".old";
                if (File.Exists(oldExePath))
                {
                    if (File.Exists(currentExePath)) File.Delete(currentExePath);
                    File.Move(oldExePath, currentExePath);
                }
            }
            catch {}
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }
    }
}
