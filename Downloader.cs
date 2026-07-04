using System.Net;

namespace CrossTalkPatcher;

public static class Downloader
{
    public static void DownloadFile(string url, string destPath)
    {
#if NETFRAMEWORK
        using (var client = new WebClient())
        {
            client.DownloadFile(url, destPath);
        }
#else
        using (var client = new System.Net.Http.HttpClient())
        {
            using (var response = client.GetAsync(url).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                using (var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                using (var file = File.Create(destPath))
                {
                    stream.CopyTo(file);
                }
            }
        }
#endif
    }

    public static Task DownloadFileAsync(string url, string destPath)
    {
        return Task.Run(() => DownloadFile(url, destPath));
    }
}