using System.Text;

namespace CrossTalkPatcher;

/// <summary>
///  makes the ascii strings point to the actual servers, if you want to know why, go to https://crosstalk.im/guides/patching and look at MSN/Windows Messenger 4.7.2009 - 3001 patching guide with Hex
/// </summary>
public static class BinaryPatcher
{
    public record StringPair(string Find, string Replace);

    public static readonly StringPair[] CorePairs =
    {
        new("messenger.hotmail.com", "ms.msgrsvcs.ctsrv.gay"),
        new("nexus.passport.com", "pp.login.ugnet.gay"),
    };

    public static readonly StringPair GatewayPair =
        new("gateway.messenger.hotmail.com", "httpgws.ms.msgrsvcs.ctsrv.gay");

    public static void Patch(string targetFile, bool includeGateway)
    {
        if (!File.Exists(targetFile))
            throw new FileNotFoundException("Target file not found", targetFile);

        string backup = targetFile + ".bak";
        if (!File.Exists(backup))
        {
            File.Copy(targetFile, backup);
            Console.WriteLine($"Backup saved to {backup}");
        }
        else
        {
            Console.WriteLine($"Backup already exists at {backup} (not overwriting)");
        }

        var pairs = new List<StringPair>(CorePairs);
        if (includeGateway) pairs.Add(GatewayPair);

        byte[] bytes = File.ReadAllBytes(targetFile);
        int totalReplacements = 0;

        foreach (var pair in pairs)
        {
            byte[] findBytes = Encoding.ASCII.GetBytes(pair.Find);
            byte[] replaceBytes = Encoding.ASCII.GetBytes(pair.Replace);

            if (replaceBytes.Length > findBytes.Length)
            {
                Console.WriteLine($"Skipping '{pair.Find}' -> '{pair.Replace}': replacement is longer than original.");
                continue;
            }

            if (replaceBytes.Length < findBytes.Length)
            {
                var padded = new byte[findBytes.Length]; // defaults to 0x00
                Array.Copy(replaceBytes, padded, replaceBytes.Length);
                replaceBytes = padded;
            }

            int occurrences = 0;
            for (int i = 0; i <= bytes.Length - findBytes.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < findBytes.Length; j++)
                {
                    if (bytes[i + j] != findBytes[j]) { match = false; break; }
                }
                if (match)
                {
                    Array.Copy(replaceBytes, 0, bytes, i, replaceBytes.Length);
                    occurrences++;
                    i += findBytes.Length - 1;
                }
            }

            Console.WriteLine($"'{pair.Find}' -> '{pair.Replace}': {occurrences} occurrence(s) patched");
            totalReplacements += occurrences;
        }

        if (totalReplacements > 0)
        {
            File.WriteAllBytes(targetFile, bytes);
            Console.WriteLine($"Done. {totalReplacements} total replacement(s) written to {targetFile}");
        }
        else
        {
            Console.WriteLine("No matching strings were found. Wrong file/version?");
        }
    }
}