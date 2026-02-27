using StoreLib.DataContracts;
using StoreLib.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace X410Launcher.Tools;

public class MicrosoftStorePackage
{
    public delegate void PackageLoadCallback();

    private static readonly string _fe3FileUrl = ((Func<string>)(() => {
        using var fe3FileUrlStream = typeof(FE3Handler).Assembly
            .GetManifestResourceStream("StoreLib.Xml.FE3FileUrl.xml");
        using var fe3FileUrlReader = new StreamReader(fe3FileUrlStream!);
        return fe3FileUrlReader.ReadToEnd();
    }))();

    private readonly string _token;
    public string Token => _token;

    private List<PackageInfo> _locations = new();
    public List<PackageInfo> Locations { get => _locations; }

    private int? _totalLocations = null;
    public int? TotalLocations => _totalLocations;

    public MicrosoftStorePackage(string token)
    {
        _token = token;
    }

    public async Task LoadAsync(PackageLoadCallback? callback = null)
    {
        _locations.Clear();
        _totalLocations = null;

        var dcatHandler = DisplayCatalogHandler.ProductionConfig();
        await dcatHandler.QueryDCATAsync(_token);

        if (!dcatHandler.IsFound)
        {
            return;
        }

        var sku = dcatHandler.ProductListing.Product.DisplaySkuAvailabilities[0].Sku;
        var wuCategoryId = sku.Properties.FulfillmentData.WuCategoryId;
        var syncUpdatesResponse = await FE3Handler.SyncUpdatesAsync(wuCategoryId);

        var xml = new SoapXml(syncUpdatesResponse);

        var updateInfoNodes = xml.Body.SelectNodes(".//this:UpdateInfo").ToList();

        _totalLocations = updateInfoNodes.Count;
        callback?.Invoke();

        var tasks = updateInfoNodes.Select(async (updateInfo) =>
        {
            var id = updateInfo.SelectSingleNode(".//this:ID").InnerText;

            var extendedInfo = xml.Body
                .SelectSingleNode($".//this:ExtendedUpdateInfo//this:Update[this:ID[. = '{id}']]");

            var file = extendedInfo.SelectSingleNode(".//this:File");

            var updateIdentity = updateInfo.SelectSingleNode(".//this:UpdateIdentity");
            var updateId = updateIdentity.GetAttribute("UpdateID");
            var revisionNumber = updateIdentity.GetAttribute("RevisionNumber");

            var url = await GetFileUrlAsync(updateId, revisionNumber);

            var fileName = file.GetAttribute("FileName");
            var identifier = file.GetAttribute("InstallerSpecificIdentifier");
            var sha1 = Base64ToHex(file.GetAttribute("Digest"));
            var size = file.GetAttribute("Size");

            PackageInfo? packageInfo = null;

            if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(identifier))
            {
                var info = identifier.Split('_');

                packageInfo = new PackageInfo()
                {
                    URL = url,
                    Name = Path.ChangeExtension(identifier, Path.GetExtension(fileName)),
                    ExpireTime = DateTime.TryParse(xml.Expires, out var expireTime) ? expireTime
                        : DateTime.MaxValue,
                    SHA1 = sha1,
                    PackageName = info[0],
                    Version = Version.Parse(info[1]),
                    Architecture = (PackageArchitecture)Enum.Parse(
                        typeof(PackageArchitecture), info[2]
                    ),
                    Format = (PackageFormat)Enum.Parse(
                        typeof(PackageFormat),
                        Path.GetExtension(fileName).Substring(1)
                    ),
                    Size = long.TryParse(size, out var longSize) ? longSize : null
                };
            }

            lock (_locations)
            {
                if (packageInfo != null)
                {
                    _locations.Add(packageInfo);
                }

                callback?.Invoke();
            }
        });

        await Task.WhenAll(tasks);

        _totalLocations = _locations.Count;
        callback?.Invoke();
    }

    private async Task<string> GetFileUrlAsync(string updateId, string revisionNumber)
    {
        using var client = new MSHttpClient();

        var content = new StringContent(
            string.Format(_fe3FileUrl, updateId, revisionNumber),
            Encoding.UTF8,
            "application/soap+xml"
        );

        var response = await client.PostAsync(Endpoints.FE3DeliverySecured, content);
        var getExtendedUpdateInfo2Response = await response.Content.ReadAsStringAsync();

        var xml = new SoapXml(getExtendedUpdateInfo2Response);

        foreach (var node in xml.Body.SelectNodes(".//this:FileLocation/this:Url"))
        {
            var url = node.InnerText;

            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResponse = await client.SendAsync(headRequest);

            if (!headResponse.IsSuccessStatusCode)
            {
                continue;
            }

            var dispositions = headResponse.Content.Headers.GetValues("Content-Disposition")
                .Select(ContentDispositionHeaderValue.Parse);

            var isBlockMap = dispositions.Any(
                disposition => Path.GetExtension(disposition.FileName) == ".BlockMap"
            );

            if (isBlockMap)
            {
                continue;
            }

            return url;
        }

        return "";
    }

    private string Base64ToHex(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        return string.Concat(bytes.Select(b => b.ToString("x2")));
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
    public long? Size { get; set; } = null;

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
