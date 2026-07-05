import urllib.request

def download_file(url, dest_path):
    """Downloads a file from a URL to the specified destination path."""
    req = urllib.request.Request(
        url,
        headers={'User-Agent': 'CrossTalkPatcher-Downloader'}
    )
    # i would LOVE to see the crosstalk devs react to "CrossTalkPatcher-Downloader" download from their servers lmao
    with urllib.request.urlopen(req) as response, open(dest_path, 'wb') as out_file:
        out_file.write(response.read())
