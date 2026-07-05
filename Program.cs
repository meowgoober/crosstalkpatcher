// main program ui and stuff
// you know it

using System.Diagnostics;
using System.Security.Principal;

namespace CrossTalkPatcher;

public static class Program
{
    public static void Main()
    {
        Updater.CheckForUpdates();

        if (!IsAdministrator())
        {
            Console.WriteLine("Note: not running as Administrator. Options 2 and 4 write into");
            Console.WriteLine("Program Files and will fail with 'Access denied' unless you");
            Console.WriteLine("re-launch this as elevated.");
            Console.WriteLine();
            Console.WriteLine("Press Enter to continue anyway...");
            Console.ReadLine();
        }

        while (true)
        {
            Console.Clear();
            Console.WriteLine("============================================");
            Console.WriteLine("  CrossTalk Client Patcher (C#)");
            Console.WriteLine("============================================");
            Console.WriteLine("  1. MSN Messenger < 4.7.2009  (registry)");
            Console.WriteLine("  2. MSN/Windows Messenger 4.7.2009 - 3001  (binary + registry)");
            Console.WriteLine("  3. Yahoo! Messenger 5/6 legacy method  (registry)");
            Console.WriteLine("  4. Reroute setup for MSN 5+ / Yahoo (auto-download, copy, configure, PE patch)");
            Console.WriteLine("  5. AIM / OSCAR");
            Console.WriteLine("  6. Join the developers' Discord server");
            Console.WriteLine("  7. Join CrossTalk's official Discord server");
            Console.WriteLine("  8. Visit crosstalk.im");
            Console.WriteLine("  9. Download a client");
            Console.WriteLine("  0. Exit");
            Console.WriteLine("============================================");
            Console.Write("Select an option: ");
            string? choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1": OldMsn(); break;
                    case "2": MidMsn(); break;
                    case "3": OldYahoo(); break;
                    case "4": Reroute(); break;
                    case "5": Aim(); break;
                    case "6": OpenUrl("https://discord.gg/dnfGVjJ8r3"); break;
                    case "7": OpenUrl("https://discord.gg/2bbHHP7TaS"); break;
                    case "8": OpenUrl("https://crosstalk.im/"); break;
                    case "9": ClientDownloads(); break;
                    case "0": return;
                    default: break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            if (choice != "0" && choice != "5" && choice != "6" && choice != "7" && choice != "8" && choice != "9")
            {
                Console.WriteLine();
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
            }
        }
    }

    private static void OldMsn()
    {
        RegistryPatcher.PatchOldMsn();
        Console.WriteLine("Done. Server key set to ms.msgrsvcs.ctsrv.gay");
    }

    private static void MidMsn()
    {
        Console.Write("NOTE: this may or may not work for you, it might patch when you first install messenger but when you uninstall and install it it might not patch it.");
        Console.Write("Full path to msnmsgr.exe: ");
        string exePath = Console.ReadLine() ?? "";

        Console.Write("Also patch the HTTP gateway address? [y/N]: ");
        bool gateway = (Console.ReadLine() ?? "").Trim().Equals("y", StringComparison.OrdinalIgnoreCase);

        BinaryPatcher.Patch(exePath, gateway);

        Console.WriteLine();
        Console.WriteLine("Updating the registry Server key, if present...");
        if (RegistryPatcher.UpdateMsnServerIfPresent())
            Console.WriteLine("Registry Server key updated.");
        else
            Console.WriteLine("No existing Server key found under MessengerService - nothing to update there.");
    }

    private static void OldYahoo()
    {
        RegistryPatcher.PatchLegacyYahoo();
        Console.WriteLine("Done.");
    }

    private const string RerouteDllUrl = "https://storage.ugnet.gay/crosstalk-dist/client/all/patching/reroute/reroute.dll";
    private const string RerouteIniUrl = "https://storage.ugnet.gay/crosstalk-dist/client/all/patching/reroute/sample-reroute.ini";

    private static void Reroute()
    {
        Console.WriteLine();
        Console.WriteLine("Which client are you patching?");
        Console.WriteLine("  1. MSN Messenger 5.0 - 8.1");
        Console.WriteLine("  2. Windows Live Messenger 8.5");
        Console.WriteLine("  3. Windows Live Messenger 2009+  (patches Messenger + Contacts)");
        Console.WriteLine("  4. Yahoo! Messenger 5/6");
        Console.WriteLine("  5. Yahoo! Messenger 7.5/8");
        Console.WriteLine("  6. Custom (enter folder/exe manually)");
        Console.Write("Choice: ");
        string clientChoice = (Console.ReadLine() ?? "").Trim();

        string programFiles = Environment.Is64BitOperatingSystem
            ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        var targets = new List<(string InstallDir, string ExeName)>();
        string clientType;

        switch (clientChoice)
        {
            case "1":
                targets.Add((Path.Combine(programFiles, "MSN Messenger"), "msnmsgr.exe"));
                clientType = "msn";
                break;
            case "2":
                targets.Add((Path.Combine(programFiles, "Windows Live", "Messenger"), "msnmsgr.exe"));
                clientType = "msn";
                break;
            case "3":
                targets.Add((Path.Combine(programFiles, "Windows Live", "Messenger"), "msnmsgr.exe"));
                targets.Add((Path.Combine(programFiles, "Windows Live", "Contacts"), "wlcomm.exe"));
                clientType = "msn";
                break;
            case "4":
                targets.Add((Path.Combine(programFiles, "Yahoo!", "Messenger"), "YPager.exe"));
                clientType = "yahoo";
                break;
            case "5":
                targets.Add((Path.Combine(programFiles, "Yahoo!", "Messenger"), "YahooMessenger.exe"));
                clientType = "yahoo";
                break;
            default:
                Console.Write("Install folder: ");
                string dir = Console.ReadLine() ?? "";
                Console.Write("Executable name: ");
                string exe = Console.ReadLine() ?? "";
                targets.Add((dir, exe));
                Console.Write("Client type [msn/yahoo/myspace]: ");
                clientType = (Console.ReadLine() ?? "msn").Trim().ToLowerInvariant();
                break;
        }

        Console.WriteLine();
        Console.WriteLine("Targets:");
        foreach (var t in targets)
            Console.WriteLine($"  {Path.Combine(t.InstallDir, t.ExeName)}");
        Console.Write("Look correct? [Y/n]: ");
        if ((Console.ReadLine() ?? "").Trim().Equals("n", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Aborted - rerun and pick option 6 to enter a custom path.");
            return;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "CrossTalkPatcher");
        Directory.CreateDirectory(tempDir);
        string dllTemp = Path.Combine(tempDir, "reroute.dll");
        string iniTemp = Path.Combine(tempDir, "sample-reroute.ini");

        if (!LiefImportInjector.EnsurePythonAndLief())
            return;

        Console.WriteLine();
        Console.WriteLine("Downloading reroute.dll and sample-reroute.ini...");
        Downloader.DownloadFileAsync(RerouteDllUrl, dllTemp).GetAwaiter().GetResult();
        Downloader.DownloadFileAsync(RerouteIniUrl, iniTemp).GetAwaiter().GetResult();
        Console.WriteLine("Download complete.");

        foreach (var (installDir, exeName) in targets)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {exeName} in {installDir} ---");

            if (!Directory.Exists(installDir))
            {
                Console.WriteLine("Folder not found - skipping. (Is the client actually installed here?)");
                continue;
            }

            string exePath = Path.Combine(installDir, exeName);
            if (!File.Exists(exePath))
            {
                Console.WriteLine($"{exeName} not found in this folder - skipping.");
                continue;
            }

            string destDll = Path.Combine(installDir, "reroute.dll");
            string destIni = Path.Combine(installDir, $"{exeName}-reroute.ini");
            File.Copy(dllTemp, destDll, overwrite: true);
            File.Copy(iniTemp, destIni, overwrite: true);

            // The sample ini already points every service URL at the correct
            // CrossTalk servers - the only line that needs changing is `type`.
            var iniLines = File.ReadAllLines(destIni);
            for (int i = 0; i < iniLines.Length; i++)
            {
                string trimmed = iniLines[i].TrimStart();
                if (trimmed.StartsWith("type") && !trimmed.StartsWith("#"))
                {
                    iniLines[i] = $"type = {clientType}";
                    break;
                }
            }
            File.WriteAllLines(destIni, iniLines);
            Console.WriteLine($"Configured {Path.GetFileName(destIni)} (type = {clientType})");

            try
            {
                LiefImportInjector.AddImport(exePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PE import patch failed for {exeName}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Done. You should now be able to sign in.");
    }

    private static void Aim()
    {
        Console.WriteLine();
        Console.WriteLine("Crosstalk has disabled this and is only for testing. Returning to main menu.");
        Thread.Sleep(1500);
    }
    // What? You think you were actually gonna get AIM? Didnt crosstalk say its disabled? Yeah they did, i dont know why this is still in the menu.

    private static void ClientDownloads()
    {
        Console.WriteLine();
        Console.WriteLine("Client download options:");
        Console.WriteLine("  1. WLM/MSN (Pre-Patched/Unpatched)");
        Console.WriteLine("  2. Yahoo (Unpatched)");
        Console.Write("Choice: ");

        string choice = (Console.ReadLine() ?? "").Trim();
        switch (choice)
        {
            case "1":
                OpenUrl("https://crosstalk.im/downloads/msn");
                break;
            case "2":
                OpenUrl("https://crosstalk.im/downloads/ym");
                break;
            default:
                Console.WriteLine("Invalid choice.");
                break;
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            Console.WriteLine();
            Console.WriteLine($"Opened {url} in your default browser.");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"Couldn't open a browser automatically ({ex.Message}).");
            Console.WriteLine($"Here's the link: {url}");
        }
        Thread.Sleep(1000);
    }
}