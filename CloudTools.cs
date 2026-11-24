using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Qusasa;

internal static class CloudTools
{
    private const string RegistryPath = @"Software\Qusasa\Cloud";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(18) };
    private static readonly Uri? Relay = GetRelay();
    internal static string Mode => Registry.GetValue(@"HKEY_CURRENT_USER\" + RegistryPath, "ScanMode", "Auto") as string ?? "Auto";

    internal static string Secret(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        if (key?.GetValue(name) is not byte[] data) return "";
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser)); }
        catch (CryptographicException) { return ""; }
    }

    internal static void SaveScanMode(string mode)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        key.SetValue("ScanMode", mode);
    }

    internal static async Task<(string Text, bool Cloud)> Scan(string png)
    {
        string mode = Mode;
        string key = Secret("OcrKey");
        bool canUseCloud = mode == "Cloud" || (mode == "Auto" && System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable());
        if (canUseCloud && (Relay != null || key.Length != 0))
        {
            try
            {
                byte[] image = Convert.FromBase64String(png);
                string text = Relay == null ? await ScanDirect(image, key).ConfigureAwait(false)
                    : await ScanRelay(image, Relay).ConfigureAwait(false);
                if (mode != "Auto" || !string.IsNullOrWhiteSpace(text)) return (text, true);
            }
            catch (Exception ex) when (mode == "Auto" && IsServiceError(ex)) { }
        }
        else if (mode == "Cloud") throw new InvalidOperationException();
        return (await Task.Run(() => LocalScanner.Read(png)).ConfigureAwait(false), false);
    }

    private static Uri? GetRelay()
    {
        string? value = typeof(CloudTools).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "OcrRelayUrl")?.Value;
        return Uri.TryCreate(value, UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(address.UserInfo) ? address : null;
    }

    private static async Task<string> ScanRelay(byte[] png, Uri endpoint)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = Png(png) };
        return (await Send<RelayResponse>(request).ConfigureAwait(false)).Text ?? "";
    }

    private static async Task<string> ScanDirect(byte[] png, string key)
    {
        using var form = new MultipartFormDataContent();
        form.Add(Png(png), "file", "clip.png");
        form.Add(new StringContent("2"), "OCREngine");
        form.Add(new StringContent("auto"), "language");
        form.Add(new StringContent("true"), "scale");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ocr.space/parse/image") { Content = form };
        request.Headers.Add("apikey", key);
        var response = await Send<ScanResponse>(request).ConfigureAwait(false);
        if (response.Error) throw new InvalidOperationException();
        return string.Join("\n", (response.Pages ?? Array.Empty<ScanPage>()).Select(page => page.Text));
    }

    private static ByteArrayContent Png(byte[] data)
    {
        var image = new ByteArrayContent(data);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        return image;
    }

    internal static bool IsServiceError(Exception ex) => ex is HttpRequestException || ex is TaskCanceledException ||
        ex is SerializationException || ex is InvalidOperationException || ex is FormatException || ex is ArgumentException;

    private static async Task<T> Send<T>(HttpRequestMessage request)
    {
        using var response = await Client.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
    }

    [DataContract] private sealed class ScanResponse
    {
        [DataMember(Name = "IsErroredOnProcessing")] public bool Error { get; set; }
        [DataMember(Name = "ParsedResults")] public ScanPage[]? Pages { get; set; }
    }

    [DataContract] private sealed class RelayResponse
    {
        [DataMember(Name = "text")] public string? Text { get; set; }
    }

    [DataContract] private sealed class ScanPage
    {
        [DataMember(Name = "ParsedText")] public string? Text { get; set; }
    }

}
