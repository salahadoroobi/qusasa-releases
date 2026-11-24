using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Qusasa;

internal static class UpdateChannel
{
    private const long MaxInstaller = 250L * 1024 * 1024;
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };
    internal static Uri? Feed { get; } = Https(typeof(UpdateChannel).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "UpdateFeedUrl")?.Value);

    [DataContract]
    internal sealed class Release
    {
        [DataMember(Name = "version")] public string Version { get; set; } = "";
        [DataMember(Name = "url")] public string Url { get; set; } = "";
        [DataMember(Name = "sha256")] public string Hash { get; set; } = "";
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "notesAr")] public string ArabicNotes { get; set; } = "";
        [DataMember(Name = "notes")] public string Notes { get; set; } = "";
        [DataMember(Name = "localizedNotes")] public LocalizedNotes? Localized { get; set; }

        internal string NotesFor(string language) => language switch
        {
            "ar" => ArabicNotes, "ja" => Localized?.Japanese ?? Notes, "zh" => Localized?.Chinese ?? Notes,
            "fr" => Localized?.French ?? Notes, "es" => Localized?.Spanish ?? Notes, _ => Notes
        };
    }

    [DataContract]
    internal sealed class LocalizedNotes
    {
        [DataMember(Name = "ja")] public string? Japanese { get; set; }
        [DataMember(Name = "zh")] public string? Chinese { get; set; }
        [DataMember(Name = "fr")] public string? French { get; set; }
        [DataMember(Name = "es")] public string? Spanish { get; set; }
    }

    [DataContract]
    private sealed class SignedFeed
    {
        [DataMember(Name = "payload")] public string Payload { get; set; } = "";
        [DataMember(Name = "signature")] public string Signature { get; set; } = "";
    }

    internal static async Task<Release?> Check(Uri feed)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var response = await Client.GetAsync(feed, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        ValidateResponse(response);
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var data = new MemoryStream();
        var buffer = new byte[4096];
        for (int read; (read = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) != 0;)
        {
            if (data.Length + read > 96 * 1024) throw new InvalidDataException();
            data.Write(buffer, 0, read);
        }
        var trust = Application.GetResourceStream(new Uri("/Qusasa;component/UpdateTrust.xml", UriKind.Relative));
        using var reader = new StreamReader(trust.Stream);
        var release = Verify(data.ToArray(), reader.ReadToEnd());
        return Version.Parse(release.Version) > typeof(UpdateChannel).Assembly.GetName().Version ? release : null;
    }

    internal static Release Verify(byte[] envelope, string publicKey)
    {
        var signed = Decode<SignedFeed>(envelope);
        byte[] payload = Convert.FromBase64String(signed.Payload);
        using var rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        if (!rsa.VerifyData(payload, Convert.FromBase64String(signed.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new CryptographicException();
        var release = Decode<Release>(payload);
        if (!Version.TryParse(release.Version, out _) || Https(release.Url) == null ||
            release.Size < 1 || release.Size > MaxInstaller || release.Hash?.Length != 64 ||
            !release.Hash.All(Uri.IsHexDigit) || release.Notes == null || release.ArabicNotes == null ||
            release.Notes.Length > 12000 || release.ArabicNotes.Length > 12000)
            throw new InvalidDataException();
        return release;
    }

    internal static async Task<string> Download(Release release, IProgress<int> progress)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Qusasa", "Updates");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Qusasa-" + Guid.NewGuid().ToString("N") + "-Setup.exe");
        try
        {
            using var response = await Client.GetAsync(Https(release.Url)!, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            ValidateResponse(response);
            var advertised = response.Content.Headers.ContentLength;
            if (advertised.HasValue && advertised.Value != release.Size) throw new InvalidDataException();
            using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            {
                var buffer = new byte[65536];
                long total = 0;
                int previous = -1;
                for (int read; (read = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) != 0;)
                {
                    total += read;
                    if (total > release.Size) throw new InvalidDataException();
                    await output.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false);
                    int percent = (int)(total * 100 / release.Size);
                    if (percent != previous) { progress.Report(percent); previous = percent; }
                }
            }
            VerifyInstaller(path, release);
            return path;
        }
        catch
        {
            try { File.Delete(path); }
            catch (Exception error) when (HistoryStore.IsStorageError(error)) { }
            throw;
        }
    }

    internal static void VerifyInstaller(string path, Release release)
    {
        using var file = File.OpenRead(path);
        using var sha = SHA256.Create();
        if (file.Length != release.Size || !string.Equals(BitConverter.ToString(sha.ComputeHash(file)).Replace("-", ""),
            release.Hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
    }

    private static void ValidateResponse(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException();
    }

    private static Uri? Https(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var address) &&
        address.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(address.UserInfo) ? address : null;

    private static T Decode<T>(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return new DataContractJsonSerializer(typeof(T)).ReadObject(stream) is T item ? item : throw new InvalidDataException();
    }

    internal static bool IsUpdateError(Exception error) => HistoryStore.IsStorageError(error) ||
        error is HttpRequestException || error is OperationCanceledException || error is System.ComponentModel.Win32Exception;
}
