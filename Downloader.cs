// i think i was using this for something, i forgot.

using System.Net.Http;

namespace CrossTalkPatcher;

public static class Downloader
{
    private static readonly HttpClient Client = new();

    public static async Task DownloadFileAsync(string url, string destPath)
    {
        using var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        await using var fs = File.Create(destPath);
        await response.Content.CopyToAsync(fs);
    }
}