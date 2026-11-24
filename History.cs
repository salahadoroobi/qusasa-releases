using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;

namespace Qusasa;

[DataContract]
internal sealed class Snippet
{
    [DataMember(Name = "id")] public Guid Id { get; set; } = Guid.NewGuid();
    [DataMember(Name = "text")] public string Text { get; set; } = "";
    [DataMember(Name = "savedUtc")] public DateTime SavedUtc { get; set; } = DateTime.UtcNow;
    [DataMember(Name = "pinned")] public bool Pinned { get; set; }
    [DataMember(Name = "png", EmitDefaultValue = false)] public string? ImagePng { get; set; }
    [DataMember(Name = "width", EmitDefaultValue = false)] public int ImageWidth { get; set; }
    [DataMember(Name = "height", EmitDefaultValue = false)] public int ImageHeight { get; set; }

    [DataMember(EmitDefaultValue = false)] public DateTime? FirstSavedUtc { get; set; }
    internal DateTime HistoryDay => (FirstSavedUtc ?? SavedUtc).ToLocalTime().Date;

    public bool IsImage => ImagePng != null;
    public bool IsLink => !IsImage && WebAddress(Text) != null;

    internal static string? WebAddress(string text)
    {
        string value = TrimClipboardText(text);
        if (value.StartsWith("/http", StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);
        return value.Length <= 8192 && !value.Any(char.IsWhiteSpace) &&
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && uri.Host.Length > 0
            ? value : null;
    }

    internal static string TrimClipboardText(string text) => Regex.Replace(text,
        @"^[\s\u061C\u200E\u200F\u202A-\u202E\u2066-\u2069\uFEFF]+|[\s\u061C\u200E\u200F\u202A-\u202E\u2066-\u2069\uFEFF]+$", "");

    public bool IsFilePath => !IsImage && TrimClipboardText(Text).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        is { Length: > 0 } lines && lines.All(IsAbsoluteWindowsPath);

    internal static bool IsAbsoluteWindowsPath(string line)
    {
        string path = TrimClipboardText(TrimClipboardText(line).Trim('"'));
        return Regex.IsMatch(path, @"^(?:[A-Za-z]:[\\/]|\\\\(?:\?\\(?:[A-Za-z]:\\|UNC\\[^\\]+\\[^\\]+)|[^\\]+\\[^\\]+))", RegexOptions.CultureInvariant)
            && path.IndexOfAny(new[] { '"', '<', '>', '|', '\0', '\t' }) < 0;
    }

    public string Kind => IsImage ? Ui.T("صورة") : IsLink ? Ui.T("رابط") : IsFilePath ? Ui.T("مسار") : Ui.T("نص");
    public string Glyph => IsImage ? "\uEB9F" : IsLink ? "\uE71B" : IsFilePath ? "\uE8B7" : "\uE8A5";
    public string PinGlyph => Pinned ? "\uE718" : "";
    public string Dimensions => string.Format(CultureInfo.InvariantCulture, "{0} × {1}", ImageWidth, ImageHeight);
    internal (bool IsImage, string Value) ContentKey => (IsImage, ImagePng ?? Text);
    internal string SearchText => IsImage ? Ui.T("صورة ") + Dimensions : Text;
    public string TimeLabel
    {
        get
        {
            DateTime local = SavedUtc.ToLocalTime();
            return local.ToString(local.Date == DateTime.Today ? "t" : "d", Ui.Culture);
        }
    }

    public string DateLabel => SavedUtc.ToLocalTime().ToString("g", Ui.Culture);
    public string Caption
    {
        get
        {
            if (IsImage) return Dimensions;
            string sample = Text.Length > 280 ? Text.Substring(0, 280) : Text;
            string line = Regex.Replace(sample.Trim(), @"\s+", " ");
            return line.Length > 110 ? line.Substring(0, 110) + "…" : line;
        }
    }

    public FlowDirection Direction
    {
        get
        {
            if (IsImage) return FlowDirection.LeftToRight;
            foreach (char character in Text)
            {
                if (!char.IsLetter(character)) continue;
                return (character >= '\u0590' && character <= '\u08ff') ||
                       (character >= '\ufb1d' && character <= '\ufefc')
                    ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            }
            return FlowDirection.RightToLeft;
        }
    }

    internal Snippet With(string? text = null, bool? pinned = null, bool moveToNow = false) => new()
    {
        Id = Id, Text = text ?? Text, Pinned = pinned ?? Pinned,
        ImagePng = ImagePng, ImageWidth = ImageWidth, ImageHeight = ImageHeight,
        FirstSavedUtc = FirstSavedUtc ?? SavedUtc,
        SavedUtc = moveToNow ? DateTime.UtcNow : SavedUtc
    };
}

[DataContract]
internal sealed class History
{
    internal const int MaxEntries = 2000;
    internal const int MaxTextLength = 500_000;
    internal const int MaxTotalLength = 4_000_000;
    internal const int MaxTotalImageEncodedLength = 64 * 1024 * 1024;
    internal const string DefaultFont = "Segoe UI";
    internal const int DefaultFontSize = 15;

    [DataMember(Name = "clips")] public List<Snippet> Clips { get; set; } = new();
    [DataMember(Name = "listening")] public bool Listening { get; set; } = true;
    [DataMember(Name = "keepInTray")] public bool KeepInTray { get; set; } = true;
    [DataMember] public bool ShortHistoryUnits { get; set; }
    [DataMember] public bool ManualUpdates { get; set; }
    [DataMember(Name = "lightTheme")] public bool LightTheme { get; set; }
    [DataMember(Name = "font")] public string ClipFont { get; set; } = DefaultFont;
    [DataMember(Name = "fontSize")] public int ClipFontSize { get; set; } = DefaultFontSize;
    [DataMember(Name = "desktopNotifications")] public bool DesktopNotifications { get; set; }
    [DataMember(Name = "notificationContent")] public bool NotificationContent { get; set; }
    [DataMember(Name = "copySound")] public bool CopySound { get; set; }

    [DataMember] public bool GlassEnabled { get; set; }
    [DataMember] public string? ThemeMode { get; set; } = "Auto";
    [DataMember] public string? UiLanguage { get; set; }
    [DataMember] public string? TitleFont { get; set; }
    [DataMember] public string? ImportedTitleFont { get; set; }
    [DataMember] public string? ImportedTitleFamily { get; set; }
    [DataMember] public int TitleStep { get; set; }
    [DataMember] public bool ScannerEnabled { get; set; }
    [DataMember] public bool CircleEnabled { get; set; }
    [DataMember] public string? ImportedFont { get; set; }
    [DataMember] public string? ImportedFamily { get; set; }
    internal string Theme => ThemeMode ?? (LightTheme ? "Light" : "Dark");

    [OnDeserializing]
    private void ApplyDefaults(StreamingContext context)
    {
        ClipFont = DefaultFont;
        ClipFontSize = DefaultFontSize;
    }

    public History Copy()
    {
        var copy = (History)MemberwiseClone();
        copy.Clips = new List<Snippet>(Clips);
        return copy;
    }

    public bool Fits() => Clips.Count <= MaxEntries &&
        Clips.Sum(clip => (long)clip.Text.Length) <= MaxTotalLength &&
        Clips.Sum(clip => (long)(clip.ImagePng?.Length ?? 0)) <= MaxTotalImageEncodedLength;


}

[DataContract]
internal sealed class Archive
{
    [DataMember(Name = "format")] public string Format { get; set; } = "Qusasa/1";
    [DataMember(Name = "clips")] public List<Snippet> Clips { get; set; } = new();
}

internal sealed class HistoryStore
{
    private const int MaxArchiveBytes = 160 * 1024 * 1024;
    private const int MaxTextFileBytes = 4 * 1024 * 1024;
    private static readonly byte[] ArchivePurpose = System.Text.Encoding.ASCII.GetBytes("Qusasa.Backup.1");
    private readonly string file;
    private readonly string legacyFile;
    private string? failedFile;
    private bool preserveOnSave;

    public HistoryStore(string directory)
    {
        file = Path.Combine(directory, "history.qusasa");
        legacyFile = Path.Combine(directory, "history.dat");
    }

    public History Read(out bool failed)
    {
        failed = false;
        string source = File.Exists(file) ? file : legacyFile;
        try
        {
            var history = Decode<History>(ProtectedData.Unprotect(ReadLimited(source), null, DataProtectionScope.CurrentUser));
            Validate(history.Clips);
            return history;
        }
        catch (FileNotFoundException) { return new History(); }
        catch (DirectoryNotFoundException) { return new History(); }
        catch (Exception exception) when (IsStorageError(exception))
        {
            failed = preserveOnSave = true;
            failedFile = source;
            return new History { Listening = false };
        }
    }

    internal long? StoredBytes()
    {
        try
        {
            if (File.Exists(file)) return new FileInfo(file).Length;
            if (File.Exists(legacyFile)) return new FileInfo(legacyFile).Length;
            return 0;
        }
        catch (Exception error) when (IsStorageError(error)) { return null; }
    }

    public void Write(History history)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var data = ProtectedData.Protect(Encode(history), null, DataProtectionScope.CurrentUser);
        if (preserveOnSave && failedFile != null && File.Exists(failedFile))
        {
            File.Copy(failedFile, failedFile + ".recovery-" + Guid.NewGuid().ToString("N"));
            preserveOnSave = false;
        }
        WriteAtomic(file, data);
    }

    public static List<Snippet> Import(string path)
    {
        byte[] data = ReadLimited(path);
        if (string.Equals(Path.GetExtension(path), ".qusasa", StringComparison.OrdinalIgnoreCase))
            data = ProtectedData.Unprotect(data, ArchivePurpose, DataProtectionScope.CurrentUser);
        Archive archive = Decode<Archive>(data);
        if (archive.Format != "Qusasa/1" && archive.Format != "Qusasa/2") throw new SerializationException();
        Validate(archive.Clips, decodeImages: true);
        return archive.Clips;
    }

    public static void Export(string path, List<Snippet> clips) =>
        WriteAtomic(path, ProtectedData.Protect(
            Encode(new Archive { Clips = clips, Format = clips.Any(clip => clip.IsImage) ? "Qusasa/2" : "Qusasa/1" }),
            ArchivePurpose, DataProtectionScope.CurrentUser));

    public static string ReadText(string path)
    {
        using var input = new MemoryStream(ReadLimited(path, MaxTextFileBytes));
        using var reader = new StreamReader(input, new System.Text.UTF8Encoding(false, true));
        return reader.ReadToEnd();
    }

    public static bool IsStorageError(Exception exception) =>
        exception is IOException || exception is UnauthorizedAccessException || exception is SerializationException ||
        exception is CryptographicException || exception is System.Xml.XmlException || exception is FormatException ||
        exception is System.Text.DecoderFallbackException || exception is ArgumentException;

    private static void Validate(List<Snippet>? clips, bool decodeImages = false)
    {
        if (clips == null || clips.Count > History.MaxEntries ||
            clips.Any(clip => clip == null || clip.Id == Guid.Empty || clip.SavedUtc == default || clip.Text == null ||
                (clip.IsImage ? clip.Text.Length != 0 || clip.ImagePng!.Length > ImageCodec.MaxEncodedLength :
                    string.IsNullOrWhiteSpace(clip.Text) || clip.Text.Length > History.MaxTextLength)) ||
            clips.Sum(clip => (long)clip.Text.Length) > History.MaxTotalLength ||
            clips.Sum(clip => (long)(clip.ImagePng?.Length ?? 0)) > History.MaxTotalImageEncodedLength ||
            clips.Select(clip => clip.Id).Distinct().Count() != clips.Count ||
            clips.Select(clip => clip.ContentKey).Distinct().Count() != clips.Count ||
            clips.Any(clip => clip.IsImage && !ImageCodec.IsValid(clip, decodeImages)))
            throw new SerializationException();
    }

    private static byte[] ReadLimited(string path, int limit = MaxArchiveBytes)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > limit) throw new IOException();
        var data = new byte[(int)stream.Length];
        int offset = 0;
        while (offset < data.Length)
        {
            int bytesRead = stream.Read(data, offset, data.Length - offset);
            if (bytesRead == 0) throw new EndOfStreamException();
            offset += bytesRead;
        }
        return data;
    }

    private static byte[] Encode<T>(T data)
    {
        using var stream = new MemoryStream();
        new DataContractJsonSerializer(typeof(T)).WriteObject(stream, data);
        return stream.ToArray();
    }

    private static T Decode<T>(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return new DataContractJsonSerializer(typeof(T)).ReadObject(stream) is T value
            ? value : throw new SerializationException();
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null, true);
            else File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
