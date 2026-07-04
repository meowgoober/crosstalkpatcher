using System.Diagnostics;
using System.Linq;

namespace CrossTalkPatcher;

/// <summary>
/// Wraps add_import.py instead of hand-rolling low-level PE
/// patching in C#. LIEF is a mature, purpose-built library for exactly this, why would i do this? Maybe because im lazy, who knows?
/// Requires python3 with `pip install lief` available on PATH.
/// </summary>
public static class LiefImportInjector
{
    private const string ModernPythonVersion = "3.12";
    private const string LegacyPythonVersion = "3.8";
    private const string Win81PythonVersion = "3.11";

    public static bool EnsurePythonAndLief()
    {
        string? pythonExe = FindPythonExecutable();
        if (pythonExe is null)
        {
            if (!TryBootstrapPython())
            {
                Console.WriteLine("Python is required for Option 4.");
                Console.WriteLine($"Install Python 3.8+ (use {LegacyPythonVersion} for XP/Vista/7/8, {Win81PythonVersion} for 8.1, and {ModernPythonVersion}+ for 10/11).");
                Console.WriteLine("Modern Windows: https://www.python.org/downloads/windows/");
                Console.WriteLine("Legacy Windows: https://www.python.org/downloads/windows/");
                return false;
            }

            pythonExe = FindPythonExecutable();
        }

        if (pythonExe is null)
            return false;

        if (IsLiefInstalled(pythonExe))
            return true;

        Console.WriteLine("The LIEF Python module is not installed. Trying to install it automatically...");
        if (!InstallLief(pythonExe))
        {
            Console.WriteLine("Automatic LIEF installation failed.");
            Console.WriteLine("Please install it manually by running: pip install lief");
            return false;
        }

        if (!IsLiefInstalled(pythonExe))
        {
            Console.WriteLine("LIEF is still not available after installation.");
            Console.WriteLine("Please install it manually by running: pip install lief");
            return false;
        }

        Console.WriteLine("LIEF installed successfully.");
        return true;
    }

    public static void AddImport(string exePath, string dllName = "reroute.dll", string functionName = "ImportMe")
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException("Target executable not found", exePath);

        string scriptPath = Path.Combine(AppContext.BaseDirectory, "add_import.py");
        if (!File.Exists(scriptPath))
        {
            // fall back to looking next to the source files during `dotnet run`
            scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "add_import.py");
        }
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException(
                "add_import.py not found. Make sure it's copied next to CrossTalkPatcher.csproj " +
                "(and add <None Include=\"add_import.py\"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None> to the .csproj so it lands in bin\\ too).",
                scriptPath);
        }

        string? pythonExe = FindPythonExecutable();
        if (pythonExe is null)
            throw new InvalidOperationException("Python is required for Option 4. Please install Python and ensure it is available on PATH.");

        string backup = exePath + ".bak";
        if (!File.Exists(backup))
        {
            File.Copy(exePath, backup);
            Console.WriteLine($"Backup saved to {backup}");
        }
        else
        {
            Console.WriteLine($"Backup already exists at {backup} (not overwriting)");
        }

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = BuildArguments(scriptPath, exePath, dllName, functionName)
        };

        using (var process = Process.Start(psi))
        {
            if (process == null)
                throw new InvalidOperationException("Failed to start the Python process.");

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stdout))
                Console.WriteLine(stdout.TrimEnd());

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"add_import.py failed (exit code {process.ExitCode}).\n{stderr}\n" +
                    "If this mentions 'No module named lief', run: pip install lief");
            }

            if (!string.IsNullOrWhiteSpace(stderr))
                Console.WriteLine($"[python stderr] {stderr.TrimEnd()}");
        }
    }

    private static string? FindPythonExecutable()
    {
        foreach (string candidate in new[] { "python", "python3", "py", "python.exe", "python3.exe" })
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = candidate == "py" ? "-3 -c \"import sys; print(sys.executable)\"" : "-c \"import sys; print(sys.executable)\""
                };

                using (var process = Process.Start(psi))
                {
                    if (process is null)
                        continue;

                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
                        return candidate;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static bool TryBootstrapPython()
    {
        Console.WriteLine("Python was not found. Trying a guided bootstrap...");

        foreach (string candidate in new[] { "winget", "powershell" })
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = candidate == "winget"
                        ? $"install --id {GetPreferredPythonPackageId()} -e --source winget"
                        : "-NoProfile -Command \"Get-Command winget -ErrorAction SilentlyContinue | Out-Null\""
                };

                using (var process = Process.Start(psi))
                {
                    if (process is null)
                        continue;

                    process.WaitForExit();
                    if (process.ExitCode == 0)
                        return true;
                }
            }
            catch
            {
            }
        }

        Console.WriteLine("Automatic bootstrap was not available. Please install Python manually and ensure it is on PATH.");
        return false;
    }

    private static bool IsLiefInstalled(string pythonExe)
    {
        return RunPythonCommand(pythonExe, new[] { "-c", "import importlib.util; import sys; sys.exit(0 if importlib.util.find_spec('lief') else 1)" }, showOutput: false);
    }

    private static bool InstallLief(string pythonExe)
    {
        return RunPythonCommand(pythonExe, new[] { "-m", "pip", "install", "lief", "--disable-pip-version-check", "--no-input" }, showOutput: true);
    }

    private static bool RunPythonCommand(string pythonExe, string[] args, bool showOutput)
    {
        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = BuildArguments(pythonExe == "py" ? new[] { "-3" }.Concat(args).ToArray() : args)
        };

        using (var process = Process.Start(psi))
        {
            if (process == null)
                return false;

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (showOutput && !string.IsNullOrWhiteSpace(stdout))
                Console.WriteLine(stdout.TrimEnd());

            if (showOutput && !string.IsNullOrWhiteSpace(stderr))
                Console.WriteLine($"[python stderr] {stderr.TrimEnd()}");

            return process.ExitCode == 0;
        }
    }

    private static string BuildArguments(params string[] args)
    {
        return string.Join(" ", args.Select(static arg => QuoteForCommandLine(arg)));
    }

    private static string QuoteForCommandLine(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string GetPreferredPythonPackageId()
    {
        var osVersion = Environment.OSVersion.Version;

        if (osVersion.Major >= 10)
            return $"Python.Python.{ModernPythonVersion}";

        if (osVersion.Major == 6 && osVersion.Minor == 3)
            return $"Python.Python.{Win81PythonVersion}";

        return $"Python.Python.{LegacyPythonVersion}";
    }
}