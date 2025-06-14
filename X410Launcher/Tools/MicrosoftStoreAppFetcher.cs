using System.Net;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using HtmlAgilityPack;

namespace X410Launcher.Tools;

public class MicrosoftStorePackage
{
    private string _responseString = string.Empty;

    private readonly string _apiBase;

    private readonly string _token;
    public string Token => _token;

    private List<PackageInfo> _locations = new();
    public List<PackageInfo> Locations { get => _locations; }

    public MicrosoftStorePackage(string token, string apiBase = "https://store.rg-adguard.net/api/")
    {
        _token = token;
        _apiBase = apiBase;
    }

    public async Task LoadAsync()
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(_apiBase);
        var response = await client.PostAsync("GetFiles", new FormUrlEncodedContent(new Dictionary<string, string>()
        {
            { "type", "ProductId" },
            { "url", _token },
        }));

        response.EnsureSuccessStatusCode();

        _responseString = await response.Content.ReadAsStringAsync();

        ParseLocations();
    }

    private void ParseLocations()
    {
        _locations.Clear();
        var doc = new HtmlDocument();
        doc.LoadHtml(_responseString);

        var table = doc.DocumentNode.SelectSingleNode("//table[@class='tftable']")
                    ?.Descendants("tr")
                    ?.Where(tr => tr.Elements("td").Count() >= 1)
                    ?.ToList() 
                    ?? (IList<HtmlNode>)Array.Empty<HtmlNode>();

        foreach (var row in table)
        {
            var data = row.Elements("td").ToList();
            string url = data[0].Descendants("a").First().GetAttributeValue("href", string.Empty);
            string name = data[0].InnerText;
            string expire = data[1].InnerText;
            string sha1 = data[2].InnerText;

            var info = name.Split('_');

            if (new string[] { ".appx", ".appxbundle", ".msix", ".msixbundle" }.Contains(Path.GetExtension(name)))
            {
                _locations.Add(new PackageInfo()
                {
                    URL = url,
                    Name = name,
                    ExpireTime = DateTime.Parse(expire),
                    SHA1 = sha1,
                    PackageName = info[0],
                    Version = Version.Parse(info[1]),
                    Architecture = (PackageArchitecture)Enum.Parse(typeof(PackageArchitecture), info[2]),
                    Format = (PackageFormat)Enum.Parse(typeof(PackageFormat), Path.GetExtension(name).Substring(1))
                });
            }
        }
    }

    public PackageInfo? Find(string name, PackageArchitecture arch)
    {
        return _locations.Find(location => location.PackageName == name && location.Architecture == arch);
    }
}

public delegate void PackageDownloadCallback(byte[] buffer, int length, long downloadedLength, long totalLength);

public class PackageInfo
{
    public string Name { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public Version Version { get; set; } = new();
    public PackageArchitecture Architecture { get; set; }
    public PackageFormat Format { get; set; }
    public string SHA1 { get; set; } = string.Empty;
    public string URL { get; set; } = string.Empty;
    public DateTime ExpireTime { get; set; }
    public long? Size { get; private set; } = null;

    public async Task DownloadAsync(PackageDownloadCallback? callback = null, int bufferLength = 32768, int maxRetries = 128)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(URL, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        Size = response.Content.Headers.ContentLength;
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(true);

        var buffer = new byte[bufferLength];
        long bytesRead = 0;
        int retriesLeft = maxRetries;
        while (Size == null || Size < 0 || bytesRead < Size)
        {
            var currentRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (currentRead == 0)
            {
                if (retriesLeft == 0)
                {
                    throw new Exception("Failed to download");
                }
                --retriesLeft;
            }
            bytesRead += currentRead;
            callback?.Invoke(buffer, currentRead, bytesRead, Size ?? -1);
        }
    }

    [Obsolete("This API is deprecated.")]
    public async Task<string> DownloadAsync(Action<DownloadProgressChangedEventArgs>? callback)
    {
        if (ExpireTime < DateTime.Now) throw new InvalidOperationException("The download link has expired");
        var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Create a new WebClient instance.
        using (WebClient myWebClient = new WebClient())
        {
            myWebClient.DownloadProgressChanged += (sender, args) => callback?.Invoke(args);
            // Download the Web resource and save it into the current filesystem folder.
            await myWebClient.DownloadFileTaskAsync(new Uri(URL), Path.Combine(path, Name));
        }

        return Path.Combine(path, Name);
    }
}

public enum PackageArchitecture
{
    neutral,
    x86,
    x64,
    arm,
    arm64
}

public enum PackageFormat
{
    appxbundle,
    appx,
    msixbundle,
    msix
}