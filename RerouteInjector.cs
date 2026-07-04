namespace CrossTalkPatcher;

/// AsmResolver was a pain to get working with, i tried to even to get to test with Yahoo Messenger but it wouldnt, when it did this program told me that it will only do .NET programs, i have NEVER seen a .net program that uses PE in its own other than retro messaging client, i hate this stupid plugin so much and this was a pain, this took me 2 whole days, and it was acting bad. This has been moved to Liefimportinjector.cs. - corncat 
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