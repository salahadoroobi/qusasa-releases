using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace Qusasa;

internal static class ImageCodec
{
    internal const int MaxPngBytes = 8 * 1024 * 1024;
    internal const int MaxEncodedLength = (MaxPngBytes + 2) / 3 * 4;
    internal const string ClipboardFormat = "PNG";
    private const int MaxEdge = 8192;
    private const int MaxPixels = 16 * 1024 * 1024;
    private static readonly byte[] Header = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82 };

    internal static bool AllowsDimensions(int width, int height) =>
        width > 0 && height > 0 && width <= MaxEdge && height <= MaxEdge && (long)width * height <= MaxPixels;

    internal static Snippet? Encode(BitmapSource bitmap)
    {
        if (!AllowsDimensions(bitmap.PixelWidth, bitmap.PixelHeight)) return null;
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
        if (stream.Length > MaxPngBytes) return null;
        return new Snippet
        {
            ImagePng = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length),
            ImageWidth = bitmap.PixelWidth,
            ImageHeight = bitmap.PixelHeight
        };
    }

    internal static bool IsValid(Snippet clip, bool decodeImage)
    {
        var bytes = Convert.FromBase64String(clip.ImagePng!);
        return TryDimensions(bytes, out int width, out int height) &&
            width == clip.ImageWidth && height == clip.ImageHeight && (!decodeImage || Decode(bytes) != null);
    }

    internal static BitmapSource? Decode(byte[] bytes)
    {
        if (!TryDimensions(bytes, out int width, out int height)) return null;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            BitmapSource frame = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            if (frame.PixelWidth != width || frame.PixelHeight != height) return null;
            frame.Freeze();
            return frame;
        }
        catch (Exception exception) when (IsImagingError(exception))
        {
            return null;
        }
    }

    internal static BitmapSource? DecodeThumbnail(string png)
    {
        var bytes = Convert.FromBase64String(png);
        if (!TryDimensions(bytes, out int width, out int height)) return null;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            const int limit = 220;
            if (width >= height && width > limit) bitmap.DecodePixelWidth = limit;
            else if (height > limit) bitmap.DecodePixelHeight = limit;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception exception) when (IsImagingError(exception))
        {
            return null;
        }
    }

    internal static bool IsImagingError(Exception exception) =>
        exception is FileFormatException || exception is IOException || exception is NotSupportedException ||
        exception is ArgumentException || exception is InvalidOperationException || exception is ExternalException;

    internal static bool TryDimensions(byte[] data, out int width, out int height)
    {
        width = height = 0;
        if (data.Length < 33 || data.Length > MaxPngBytes) return false;
        for (int index = 0; index < Header.Length; index++)
            if (data[index] != Header[index]) return false;
        width = ReadInt32(data, 16);
        height = ReadInt32(data, 20);
        return AllowsDimensions(width, height);
    }

    private static int ReadInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
}
