using System.Diagnostics;

namespace CrossTalkPatcher;

/// <summary>
/// Wraps add_import.py instead of hand-rolling low-level PE
/// patching in C#. LIEF is a mature, purpose-built library for exactly this, why would i do this? Maybe because im lazy, who knows?
/// Requires python3 with `pip install lief` available on PATH.
/// </summary>
public static class LiefImportInjector
{
    public static bool EnsurePythonAndLief()
    {
        string? pythonExe = FindPythonExecutable();
        if (pythonExe is null)
        {
            Console.WriteLine("Python is required for Option 4.");
            Console.WriteLine("Please install Python from https://www.python.org/downloads/ and make sure it is available on PATH.");
            return false;
        }

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
        };
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(exePath);
        psi.ArgumentList.Add(dllName);
        psi.ArgumentList.Add(functionName);

        using var process = Process.Start(psi);
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

    private static string? FindPythonExecutable()
    {
        foreach (string candidate in new[] { "python", "python3", "py" })
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
                };

                if (candidate == "py")
                    psi.ArgumentList.Add("-3");

                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("import sys; print(sys.executable)");

                using var process = Process.Start(psi);
                if (process is null)
                    continue;

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
                    return candidate;
            }
            catch
            {
            }
        }

        return null;
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
        };

        if (pythonExe == "py")
            psi.ArgumentList.Add("-3");

        foreach (string arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi);
        if (process is null)
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