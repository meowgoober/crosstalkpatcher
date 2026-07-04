namespace CrossTalkPatcher;

public static class RerouteInjector
{
    public static void AddRerouteImport(string exePath, string dllName = "reroute.dll", string functionName = "ImportMe")
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException("Target executable not found", exePath);

        string backup = exePath + ".bak";
        try
        {
            if (!File.Exists(backup))
            {
                File.Copy(exePath, backup);
                Console.WriteLine($"Backup saved to {backup}");
            }
            else
            {
                Console.WriteLine($"Backup already exists at {backup} (not overwriting)");
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException(
                $"Access denied writing to \"{Path.GetDirectoryName(exePath)}\". " +
                "Re-run this program as Administrator (right-click your terminal -> Run as administrator).");
        }

        LiefImportInjector.AddImport(exePath, dllName, functionName);
    }
}