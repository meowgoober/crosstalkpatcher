using Microsoft.Win32;

namespace CrossTalkPatcher;

public static class RegistryPatcher
{
    private const string CrossTalkHost = "ms.msgrsvcs.ctsrv.gay";

    // MSN Messenger < 4.7.2009
    public static void PatchOldMsn()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MessengerService");
        key.SetValue("Server", CrossTalkHost, RegistryValueKind.String);
    }

    // MSN/Windows Messenger 4.7.2009 - 3001: only touch the key if it already exists
    public static bool UpdateMsnServerIfPresent()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\MessengerService", writable: true);
        if (key == null)
            return false;

        key.SetValue("Server", CrossTalkHost, RegistryValueKind.String);
        return true;
    }

    // Yahoo! Messenger 5/6 legacy method (XP SP1 or older)
    public static void PatchLegacyYahoo()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Yahoo\Pager");
        key.SetValue("Conn Server", CrossTalkHost, RegistryValueKind.String);
        key.SetValue("socket server", CrossTalkHost, RegistryValueKind.String);
        key.SetValue("Host Name", CrossTalkHost, RegistryValueKind.String);
    }
}