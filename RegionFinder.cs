using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Qusasa;

internal sealed class RegionFinder
{
    private readonly int width, height, step, sourceWidth, sourceHeight;
    private readonly int[] pixels, visited, queue;
    private int generation;
    private Rectangle lastResult;

    internal RegionFinder(Bitmap screen)
    {
        sourceWidth = screen.Width;
        sourceHeight = screen.Height;
        step = Math.Max(2, (Math.Max(sourceWidth, sourceHeight) + 479) / 480);
        width = (sourceWidth + step - 1) / step;
        height = (sourceHeight + step - 1) / step;
        pixels = new int[width * height];
        visited = new int[pixels.Length];
        queue = new int[pixels.Length];
        using var preview = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(preview))
        {
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(screen, new Rectangle(0, 0, width, height));
        }
        var bits = preview.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, preview.PixelFormat);
        try { Marshal.Copy(bits.Scan0, pixels, 0, pixels.Length); }
        finally { preview.UnlockBits(bits); }
    }

    internal Rectangle Find(Point pointer)
    {
        if (pointer.X < 0 || pointer.Y < 0 || pointer.X >= sourceWidth || pointer.Y >= sourceHeight) return Rectangle.Empty;
        int x = pointer.X * width / sourceWidth;
        int y = pointer.Y * height / sourceHeight;
        int start = y * width + x;
        if (generation != 0 && visited[start] == generation) return lastResult;
        generation++;
        int color = pixels[start], count = 0;
        int left = x, right = x, top = y, bottom = y;
        Enqueue(start);
        for (int head = 0; head < count; head++)
        {
            int index = queue[head], column = index % width, row = index / width;
            left = Math.Min(left, column); right = Math.Max(right, column);
            top = Math.Min(top, row); bottom = Math.Max(bottom, row);
            if (column > 0) Enqueue(index - 1);
            if (column + 1 < width) Enqueue(index + 1);
            if (row > 0) Enqueue(index - width);
            if (row + 1 < height) Enqueue(index + width);
        }
        lastResult = Rectangle.FromLTRB(left * sourceWidth / width, top * sourceHeight / height,
            (right + 1) * sourceWidth / width, (bottom + 1) * sourceHeight / height);
        if (lastResult.Width < 20 || lastResult.Height < 14 ||
            (long)lastResult.Width * lastResult.Height > (long)sourceWidth * sourceHeight / 2)
            lastResult = Rectangle.Empty;
        return lastResult;

        void Enqueue(int index)
        {
            if (visited[index] == generation || !Similar(color, pixels[index])) return;
            visited[index] = generation;
            queue[count++] = index;
        }
    }

    private static bool Similar(int a, int b) =>
        Math.Abs(((a >> 16) & 255) - ((b >> 16) & 255)) <= 23 &&
        Math.Abs(((a >> 8) & 255) - ((b >> 8) & 255)) <= 23 &&
        Math.Abs((a & 255) - (b & 255)) <= 23;
}
