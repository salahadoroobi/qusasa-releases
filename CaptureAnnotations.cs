using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Qusasa;

internal sealed class CaptureAnnotations
{
    internal enum Tool { Pen, Rectangle, Ellipse, Arrow, Hide, Text }
    private readonly List<Mark> marks = new();
    private Mark? draft;
    internal Tool Selected { get; set; }
    internal Color Ink { get; set; } = CaptureToolbar.Accent;
    internal float Width { get; set; } = 3;
    internal bool Drawing => draft != null;

    internal void Start(Point point)
    {
        draft = new Mark(Selected, Ink, Width);
        draft.Points.Add(point);
    }

    internal void Move(Point point)
    {
        if (draft == null) return;
        var points = draft.Points;
        if (Selected != Tool.Pen && points.Count > 1) points.RemoveAt(1);
        points.Add(point);
    }

    internal void Finish(Point point)
    {
        if (draft == null) return;
        Move(point);
        marks.Add(draft);
        draft = null;
    }

    internal void AddText(Point point, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var mark = new Mark(Tool.Text, Ink, 3) { Text = text };
        mark.Points.Add(point);
        marks.Add(mark);
    }

    internal void Offset(int x, int y)
    {
        if (x == 0 && y == 0) return;
        foreach (var mark in marks)
            for (int i = 0; i < mark.Points.Count; i++)
            {
                PointF point = mark.Points[i];
                mark.Points[i] = new PointF(point.X + x, point.Y + y);
            }
    }

    internal void Undo()
    {
        if (marks.Count != 0) marks.RemoveAt(marks.Count - 1);
    }

    internal void Clear()
    {
        marks.Clear();
        draft = null;
    }

    internal void Draw(Graphics graphics, bool preview)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var mark in marks) mark.Draw(graphics);
        if (preview) draft?.Draw(graphics);
    }

    internal sealed class Mark
    {
        internal List<PointF> Points { get; } = new();
        private readonly Tool tool;
        private readonly Color color;
        private readonly float width;
        internal string? Text { get; set; }

        internal Mark(Tool tool, Color color, float width)
        {
            this.tool = tool;
            this.color = color;
            this.width = width;
        }

        internal void Draw(Graphics graphics)
        {
            if (tool == Tool.Text && Text is string caption && Points.Count > 0)
            {
                using var font = new Font("Segoe UI", 17, GraphicsUnit.Pixel);
                using var brush = new SolidBrush(color);
                graphics.DrawString(caption, font, brush, Points[0]);
                return;
            }
            if (Points.Count < 2) return;
            PointF start = Points[0], end = Points[Points.Count - 1];
            var box = RectangleF.FromLTRB(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
                Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));
            using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            switch (tool)
            {
                case Tool.Pen: graphics.DrawLines(pen, Points.ToArray()); break;
                case Tool.Rectangle: graphics.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height); break;
                case Tool.Ellipse:
                    if (box.Width > 0 && box.Height > 0) graphics.DrawEllipse(pen, box);
                    break;
                case Tool.Arrow:
                    using (var arrow = new AdjustableArrowCap(3, 5))
                    {
                        pen.CustomEndCap = arrow;
                        graphics.DrawLine(pen, start, end);
                    }
                    break;
                case Tool.Hide:
                    graphics.FillRectangle(Brushes.Black, Rectangle.FromLTRB((int)Math.Floor(box.Left), (int)Math.Floor(box.Top),
                        (int)Math.Ceiling(box.Right), (int)Math.Ceiling(box.Bottom)));
                    break;
            }
        }
    }

}
