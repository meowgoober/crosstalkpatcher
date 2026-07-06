using System.Diagnostics;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace CrossTalkPatcher;

/// <summary>
/// Downloads and silently installs the latest patch release of the Python
/// minor version that officially supports the current Windows release.
///
/// OS → Python minor version mapping:
///   Vista SP2  (6.0)      →  3.8.x   (last minor to support Vista)
///   Win 7 / 8  (6.1–6.2)  →  3.12.x  (last minor to support Win 7)
///   Win 8.1+/10/11        →  3.13.x  (current stable minor)
///
/// The patch number (x) is resolved live via the endoflife.date API so it
/// stays current automatically.  Hardcoded versions are used as fallbacks
/// when the machine is offline or the API is unreachable.
/// </summary>
public static class PythonInstaller
{
    private readonly record struct PythonRelease(string Version, string X86Url, string X64Url);

    // Fallback versions used when the live API cannot be reached.
    private const string FallbackVista  = "3.6.8";
    private const string FallbackWin7   = "3.12.9";
    private const string FallbackModern = "3.13.3";

    /// <summary>
    /// Prompts the user, fetches the latest patch version from endoflife.date,
    /// downloads the right installer for this OS, runs it silently, then
    /// returns the path to python.exe on success or null on failure/decline.
    /// </summary>
    public static string? TryInstall()
    {
        PythonRelease release = PickRelease();
        bool is64 = Environment.Is64BitOperatingSystem;
        string url = is64 ? release.X64Url : release.X86Url;

        Console.WriteLine();
        Console.WriteLine($"Python {release.Version} ({(is64 ? "64-bit" : "32-bit")}) will be");
        Console.WriteLine("downloaded from python.org and installed for your user account.");
        Console.Write("Download and install now? [Y/n]: ");
        if ((Console.ReadLine() ?? "").Trim().Equals("n", StringComparison.OrdinalIgnoreCase))
            return null;

        string installer = Path.Combine(
            Path.GetTempPath(),
            $"python-{release.Version}-installer.exe");

        Console.WriteLine($"Downloading Python {release.Version}...");
        try
        {
            Downloader.DownloadFileAsync(url, installer).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Download failed: {ex.Message}");
            return null;
        }

        Console.WriteLine("Running Python installer silently (this may take a minute)...");

        // /quiet            – no UI
        // InstallAllUsers=0 – per-user install, no admin required
        // PrependPath=1     – adds the Python folder to the current user's PATH
        var psi = new ProcessStartInfo
        {
            FileName = installer,
            Arguments = "/quiet InstallAllUsers=0 PrependPath=1",
            UseShellExecute = true,
        };

        try
        {
            using var proc = Process.Start(psi);
            proc?.WaitForExit();

            if (proc == null || proc.ExitCode != 0)
            {
                Console.WriteLine($"Python installer exited with code {proc?.ExitCode}.");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to launch installer: {ex.Message}");
            return null;
        }

        // The installer writes PATH to the registry but the current process
        // won't see the change.  Locate python.exe in the known default folder
        // and patch PATH ourselves so child processes can find it immediately.
        string? pythonExe = FindInstalledExe(release.Version);
        if (pythonExe != null)
        {
            string dir = Path.GetDirectoryName(pythonExe) ?? "";
            if (!string.IsNullOrEmpty(dir))
            {
                string oldPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                Environment.SetEnvironmentVariable("PATH", dir + ";" + oldPath);
            }

            Console.WriteLine($"Python {release.Version} installed successfully.");
            Console.WriteLine($"Location: {pythonExe}");
            return pythonExe;
        }

        // Installer succeeded but we can't find the exe — tell the user to restart.
        Console.WriteLine("Python was installed but could not be located automatically.");
        Console.WriteLine("Please restart CrossTalkPatcher and try again.");
        return null;
    }

    // -------------------------------------------------------------------------
    // Version resolution
    // -------------------------------------------------------------------------

    private static PythonRelease PickRelease()
    {
        var v = Environment.OSVersion.Version;
        // 6.0 = Vista, 6.1 = Win7, 6.2 = Win8, 6.3 = Win8.1, 10.0 = Win10/11
        if (v.Major == 6 && v.Minor == 0) return BuildRelease("3.8",  FallbackVista);
        if (v.Major == 6)                  return BuildRelease("3.12", FallbackWin7);
        return                                    BuildRelease("3.13", FallbackModern);
    }

    /// <summary>
    /// Resolves the latest patch for <paramref name="minorVer"/> via the
    /// endoflife.date API, falling back to <paramref name="fallback"/> when
    /// offline or the API is unreachable.  Constructs download URLs from the
    /// resolved version string.
    /// </summary>
    private static PythonRelease BuildRelease(string minorVer, string fallback)
    {
        string version = FetchLatestPatch(minorVer) ?? fallback;
        return new PythonRelease(
            version,
            $"https://www.python.org/ftp/python/{version}/python-{version}.exe",
            $"https://www.python.org/ftp/python/{version}/python-{version}-amd64.exe");
    }

    /// <summary>
    /// Queries https://endoflife.date/api/python.json and returns the latest
    /// patch version string for <paramref name="minorVer"/> (e.g. "3.8"),
    /// or null if the request fails for any reason.
    /// </summary>
    private static string? FetchLatestPatch(string minorVer)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CrossTalkPatcher");
            string json = client
                .GetStringAsync("https://endoflife.date/api/python.json")
                .GetAwaiter().GetResult();

            var arr = JArray.Parse(json);
            foreach (var entry in arr)
            {
                if (entry["cycle"]?.Value<string>() == minorVer)
                    return entry["latest"]?.Value<string>();
            }
        }
        catch
        {
            // Offline or API unavailable — caller will use the hardcoded fallback.
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Post-install discovery
    // -------------------------------------------------------------------------

    private static string? FindInstalledExe(string version)
    {
        // "3.8.10" → "Python38",  "3.12.9" → "Python312",  "3.13.3" → "Python313"
        string shortVer = version.Substring(0, version.LastIndexOf('.'));  // "3.8"
        string tag      = "Python" + shortVer.Replace(".", "");            // "Python38"

        string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Per-user install path (InstallAllUsers=0)
        string perUser = Path.Combine(localApp, "Programs", "Python", tag, "python.exe");
        if (File.Exists(perUser)) return perUser;

        // System-wide paths as fallback
        foreach (string pf in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        })
        {
            if (string.IsNullOrEmpty(pf)) continue;
            string sys = Path.Combine(pf, "Python", tag, "python.exe");
            if (File.Exists(sys)) return sys;
        }

        return null;
    }
}
