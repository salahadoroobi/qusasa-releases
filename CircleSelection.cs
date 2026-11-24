using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Qusasa;

internal sealed class CircleSelection : Form
{
    private enum Mode { Window, Rectangle, Free }
    private readonly Bitmap desktop;
    private readonly bool scanOnly;
    private bool controlReleased;
    private RegionFinder? shapes;
    private sealed class CaptureWindow
    {
        internal readonly Rectangle Bounds;
        internal readonly List<CaptureWindow> Children = new();
        internal IntPtr Handle;
        internal Rectangle[]? AccessibleRegions;

        internal CaptureWindow(Rectangle bounds) => Bounds = bounds;
    }

    private readonly List<CaptureWindow> windows = new();
    private readonly List<Rectangle> windowChoices = new();
    private int windowLevel;
    private Point? windowPointer;
    private bool readingRegions;
    private readonly Rectangle screenBounds;
    private readonly List<Point> points = new();
    private readonly CaptureToolbar options = new();
    private readonly CaptureToolbar editTools = new();
    private readonly CaptureAnnotations annotations = new();
    private readonly Dictionary<CaptureAnnotations.Tool, Button> editButtons = new();
    private readonly Rectangle workArea;
    private Rectangle selection;
    private Point? toolbarPosition;
    private TextBox? textInput;
    private Point textAnchor;
    private Mode mode;
    private Rectangle candidate;
    private Point? dragStart;
    private Point resizeEdges;
    private Rectangle resizeStart;
    private Button? selectedToolButton;
    private Button? customSwatch;
    private readonly Dictionary<CaptureAnnotations.Tool, (Color Ink, float Width)> toolStyles = new();
    private static readonly Color[] PresetColors =
        { Color.White, Color.FromArgb(33, 41, 52), Color.FromArgb(248, 86, 86),
          Color.FromArgb(252, 194, 65), Color.FromArgb(28, 171, 126), Color.FromArgb(69, 140, 250) };
    public Bitmap? RegionImage { get; private set; }

    public CircleSelection(bool scanOnly = false)
    {
        this.scanOnly = scanOnly;
        var screen = Screen.FromPoint(Cursor.Position);
        var bounds = screen.Bounds;
        screenBounds = bounds;
        var foreground = GetForegroundWindow();
        var monitor = new Rectangle(Point.Empty, bounds.Size);
        var backdropCandidates = new List<CaptureWindow>();
        workArea = screen.WorkingArea;
        workArea.Offset(-bounds.X, -bounds.Y);
        EnumWindows((handle, _) =>
        {
            int extendedStyle = GetWindowLong(handle, -20);
            if (!IsWindowVisible(handle) || IsIconic(handle) || IsShellSurface(handle) ||
                (extendedStyle & 0x20) != 0 ||
                (DwmGetWindowAttribute(handle, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) ||
                !GetWindowRect(handle, out var rect)) return true;

            if (GetFrameBounds(handle, 9, out var frame, Marshal.SizeOf(typeof(NativeRect))) == 0 &&
                frame.Right > frame.Left && frame.Bottom > frame.Top)
                rect = frame;
            var area = VisibleBounds(rect, bounds);
            if (area.IsEmpty) return true;
            var window = new CaptureWindow(area) { Handle = handle };
            CollectChildren(handle, bounds, window.Children, area, 0);
            // Desktop overlays and backdrops often report the entire monitor as their frame.
            int style = GetWindowLong(handle, -16);
            bool coversMonitor = area.Left <= 8 && area.Top <= 8 &&
                monitor.Right - area.Right <= 8 && monitor.Bottom - area.Bottom <= 8;
            bool floatingOverlay = (extendedStyle & (0x08000000 | 0x00080000)) != 0 ||
                ((extendedStyle & 0x00000008) != 0 && GetWindowTextLength(handle) == 0);
            if (coversMonitor && (style & 0x00C00000) == 0 && (handle != foreground || floatingOverlay))
                backdropCandidates.Add(window);
            else windows.Add(window);
            return true;
        }, IntPtr.Zero);
        windows.AddRange(backdropCandidates);
        desktop = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(desktop))
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;
        AccessibleName = Ui.T(scanOnly ? "الماسح الضوئي" : "الالتقاط");
        Controls.Add(editTools);
        Controls.Add(options);
        editTools.Visible = options.Visible = false;
        if (!scanOnly)
        {
            ConfigureEditToolbar();
            ConfigureOptions();
        }
        Shown += (_, __) =>
        {
            Activate();
            SetForegroundWindow(Handle);
            Focus();
            DetectWindow(PointToClient(Cursor.Position));
        };
    }

    private static void CollectChildren(IntPtr parent, Rectangle screen, List<CaptureWindow> children, Rectangle clip, int depth)
    {
        if (depth >= 24) return;
        var visited = new HashSet<IntPtr>();
        for (IntPtr child = GetWindow(parent, 5); child != IntPtr.Zero && visited.Add(child); child = GetWindow(child, 2))
        {
            if (!IsWindowVisible(child) || !GetWindowRect(child, out var rect)) continue;
            var bounds = Rectangle.Intersect(clip, VisibleBounds(rect, screen));
            if (bounds.Width < 32 || bounds.Height < 24) continue;

            uint style = unchecked((uint)GetWindowLong(child, -16));
            string name = ClassName(child);
            bool framed = (style & 0x00C00000) == 0x00C00000 || name == "#32770";
            bool documentedPane = name == "MDIClient" || name.StartsWith("WindowsForms10.Window.", StringComparison.Ordinal) ||
                name is "DirectUIHWND" or "SHELLDLL_DefView" or "Internet Explorer_Server" or
                    "Chrome_RenderWidgetHostHWND" or "Windows.UI.Core.CoreWindow";
            bool pane = documentedPane && bounds.Width >= 160 && bounds.Height >= 95;
            if (bounds != clip && (framed || pane))
            {
                var window = new CaptureWindow(bounds);
                children.Add(window);
                CollectChildren(child, screen, window.Children, bounds, depth + 1);
            }
            else CollectChildren(child, screen, children, bounds, depth + 1);
        }
    }

    private void FindWindowChoices(List<CaptureWindow> candidates, Point point)
    {
        foreach (var window in candidates)
        {
            if (!window.Bounds.Contains(point)) continue;
            FindWindowChoices(window.Children, point);
            if (window.Handle != IntPtr.Zero)
            {
                if (window.AccessibleRegions == null && IsHandleCreated && !readingRegions)
                    ReadAccessibleRegions(window);
                if (window.AccessibleRegions != null)
                {
                    foreach (var region in window.AccessibleRegions)
                        if (region.Contains(point) && !windowChoices.Contains(region)) windowChoices.Add(region);
                    windowChoices.Sort((a, b) => ((long)a.Width * a.Height).CompareTo((long)b.Width * b.Height));
                }
            }
            if (!windowChoices.Contains(window.Bounds)) windowChoices.Add(window.Bounds);
            return;
        }
    }

    private async void ReadAccessibleRegions(CaptureWindow window)
    {
        readingRegions = true;
        try
        {
            window.AccessibleRegions = await System.Threading.Tasks.Task.Run(() => ReadAccessibleBounds(window));
            if (!IsDisposed && Visible && selection.IsEmpty && dragStart == null)
                DetectWindow(PointToClient(Cursor.Position));
        }
        finally { readingRegions = false; }
    }

    private Rectangle[] ReadAccessibleBounds(CaptureWindow window)
    {
        try
        {
            var root = System.Windows.Automation.AutomationElement.FromHandle(window.Handle);
            var types = new[] { System.Windows.Automation.ControlType.Window, System.Windows.Automation.ControlType.Pane,
                System.Windows.Automation.ControlType.Document, System.Windows.Automation.ControlType.Group,
                System.Windows.Automation.ControlType.Menu };
            var query = new System.Windows.Automation.OrCondition(types.Select(type =>
                (System.Windows.Automation.Condition)new System.Windows.Automation.PropertyCondition(
                    System.Windows.Automation.AutomationElement.ControlTypeProperty, type)).ToArray());
            var cache = new System.Windows.Automation.CacheRequest();
            cache.Add(System.Windows.Automation.AutomationElement.BoundingRectangleProperty);
            cache.Add(System.Windows.Automation.AutomationElement.IsOffscreenProperty);
            using (cache.Activate())
            {
                var matches = root.FindAll(System.Windows.Automation.TreeScope.Descendants, query);
                var regions = new HashSet<Rectangle>();
                for (int i = 0; i < Math.Min(matches.Count, 600); i++)
                {
                    var item = matches[i].Cached;
                    var rect = item.BoundingRectangle;
                    if (item.IsOffscreen || rect.IsEmpty || rect.Width < 80 || rect.Height < 42) continue;
                    var area = Rectangle.Intersect(window.Bounds, Rectangle.FromLTRB(
                        (int)rect.Left - screenBounds.X, (int)rect.Top - screenBounds.Y,
                        (int)rect.Right - screenBounds.X, (int)rect.Bottom - screenBounds.Y));
                    if (area.Width >= 80 && area.Height >= 42 && area != window.Bounds) regions.Add(area);
                }
                return regions.ToArray();
            }
        }
        catch (Exception ex) when (ex is System.Windows.Automation.ElementNotAvailableException ||
            ex is COMException || ex is InvalidOperationException || ex is ArgumentException ||
            ex is UnauthorizedAccessException || ex is TimeoutException)
        { return Array.Empty<Rectangle>(); }
    }

    private static Rectangle VisibleBounds(NativeRect rect, Rectangle screen)
    {
        var area = Rectangle.Intersect(screen, Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom));
        if (area.Width < 32 || area.Height < 24) return Rectangle.Empty;
        area.Offset(-screen.X, -screen.Y);
        return area;
    }

    private static string ClassName(IntPtr window)
    {
        var name = new StringBuilder(128);
        GetClassName(window, name, name.Capacity);
        return name.ToString();
    }

    private static bool IsShellSurface(IntPtr window) =>
        ClassName(window) is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";

    private void DetectWindow(Point point)
    {
        bool controlDown = (ModifierKeys & Keys.Control) != 0;
        if (!controlDown) controlReleased = true;
        candidate = controlReleased && controlDown ? (shapes ??= new RegionFinder(desktop)).Find(point) : Rectangle.Empty;
        if (windowPointer is not Point previous || Math.Abs(previous.X - point.X) + Math.Abs(previous.Y - point.Y) > 5)
        {
            windowPointer = point;
            windowLevel = 0;
        }
        windowChoices.Clear();
        if (candidate.IsEmpty)
        {
            FindWindowChoices(windows, point);
            if (windowChoices.Count != 0)
                candidate = windowChoices[Math.Min(windowLevel, windowChoices.Count - 1)];
        }
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!selection.IsEmpty || dragStart != null || windowChoices.Count < 2) return;
        windowLevel = (Math.Min(windowLevel, windowChoices.Count - 1) +
            (e.Delta > 0 ? 1 : windowChoices.Count - 1)) % windowChoices.Count;
        candidate = windowChoices[windowLevel];
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(desktop, 0, 0);
        using var shade = new SolidBrush(Color.FromArgb(128, 13, 19, 27));
        using var pen = new Pen(Color.FromArgb(99, 229, 219), 2);
        using var path = SelectionPath();
        using var outside = new Region(ClientRectangle);
        if (path.PointCount > 0) outside.Exclude(path);
        e.Graphics.FillRegion(shade, outside);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (!selection.IsEmpty)
        {
            var state = e.Graphics.Save();
            e.Graphics.SetClip(path, CombineMode.Intersect);
            e.Graphics.TranslateTransform(selection.X, selection.Y);
            annotations.Draw(e.Graphics, true);
            e.Graphics.Restore(state);
        }
        if (path.PointCount > 0) e.Graphics.DrawPath(pen, path);
        if (!selection.IsEmpty && mode != Mode.Free)
            foreach (var anchor in HandlePositions(selection))
            {
                var handle = new Rectangle(anchor.X - 4, anchor.Y - 4, 8, 8);
                e.Graphics.FillRectangle(Brushes.White, handle);
                e.Graphics.DrawRectangle(pen, handle);
            }
    }

    private GraphicsPath SelectionPath()
    {
        var path = new GraphicsPath();
        if (mode == Mode.Free && points.Count >= 3) path.AddPolygon(points.ToArray());
        else
        {
            Rectangle area = !selection.IsEmpty ? selection : mode == Mode.Window ? candidate : BoundsOf(points);
            if (area.Width > 0 && area.Height > 0) path.AddRectangle(area);
        }
        return path;
    }

    private static Rectangle BoundsOf(List<Point> trace) => trace.Count < 2 ? Rectangle.Empty :
        Rectangle.FromLTRB(trace.Min(p => p.X), trace.Min(p => p.Y), trace.Max(p => p.X) + 1, trace.Max(p => p.Y) + 1);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { ReselectOrClose(); return; }
        if (e.Button != MouseButtons.Left) return;
        if (!selection.IsEmpty)
        {
            resizeEdges = GetResizeEdges(e.Location);
            if (!resizeEdges.IsEmpty)
            {
                FinishText(true);
                resizeStart = selection;
                dragStart = e.Location;
                Capture = true;
                return;
            }
            using var path = SelectionPath();
            if (!path.IsVisible(e.Location)) return;
            if (annotations.Selected == CaptureAnnotations.Tool.Text) BeginText(e.Location);
            else
            {
                annotations.Start(ToImagePoint(e.Location));
                Capture = true;
            }
            return;
        }
        dragStart = ClampToScreen(e.Location);
        mode = (ModifierKeys & Keys.Alt) != 0 ? Mode.Free : Mode.Window;
        points.Clear();
        points.Add(dragStart.Value);
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!selection.IsEmpty)
        {
            if (Capture && !resizeEdges.IsEmpty && dragStart is Point origin)
            {
                var adjusted = ResizeSelection(resizeStart, ClientRectangle, resizeEdges,
                    new Point(e.X - origin.X, e.Y - origin.Y));
                annotations.Offset(selection.X - adjusted.X, selection.Y - adjusted.Y);
                selection = adjusted;
                Invalidate();
            }
            else if (Capture && annotations.Drawing)
            {
                annotations.Move(ToImagePoint(e.Location));
                Invalidate();
            }
            else Cursor = CursorFor(GetResizeEdges(e.Location));
            return;
        }
        if (Capture && dragStart is Point start)
        {
            if (mode == Mode.Window && Math.Abs(e.X - start.X) + Math.Abs(e.Y - start.Y) < 5) return;
            if (mode == Mode.Window) mode = Mode.Rectangle;
            if (mode == Mode.Rectangle && points.Count > 1) points.RemoveAt(1);
            points.Add(ClampToScreen(e.Location));
            Invalidate();
        }
        else DetectWindow(e.Location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !Capture) return;
        OnMouseMove(e);
        Capture = false;
        dragStart = null;
        if (!selection.IsEmpty)
        {
            if (!resizeEdges.IsEmpty)
            {
                resizeEdges = Point.Empty;
                PositionToolbars();
            }
            else annotations.Finish(ToImagePoint(e.Location));
            Invalidate();
            return;
        }
        if (mode == Mode.Window) DetectWindow(e.Location);
        PreviewSelection();
    }

    private Point ClampToScreen(Point point) => new(Math.Max(0, Math.Min(ClientSize.Width - 1, point.X)),
        Math.Max(0, Math.Min(ClientSize.Height - 1, point.Y)));

    private void PreviewSelection()
    {
        using var path = SelectionPath();
        if (path.PointCount == 0) return;
        var region = Rectangle.Intersect(Rectangle.Ceiling(path.GetBounds()), ClientRectangle);
        if (region.Width < 5 || region.Height < 5 || (mode == Mode.Free && points.Count < 3))
        {
            mode = Mode.Window;
            points.Clear();
            DetectWindow(PointToClient(Cursor.Position));
            return;
        }
        if (scanOnly)
        {
            RegionImage = Crop(desktop, region, mode == Mode.Free ? path : null);
            DialogResult = DialogResult.OK;
            return;
        }
        selection = region;
        editTools.Visible = true;
        PositionToolbars();
        Invalidate();
    }

    private void PositionToolbars()
    {
        editTools.PerformLayout();
        var position = toolbarPosition ?? CaptureToolbar.PlaceNear(selection, workArea, editTools.Size, Ui.IsArabic);
        editTools.Location = new Point(
            Math.Max(workArea.Left, Math.Min(position.X, workArea.Right - editTools.Width)),
            Math.Max(workArea.Top, Math.Min(position.Y, workArea.Bottom - editTools.Height)));
        editTools.BringToFront();
        if (!options.Visible) return;
        options.PerformLayout();
        int x = !Ui.IsArabic ? editTools.Left : editTools.Right - options.Width;
        if (selectedToolButton != null)
            x = editTools.Left + selectedToolButton.Left + selectedToolButton.Width / 2 - options.Width / 2;
        int y = editTools.Bottom + options.Height + 5 <= workArea.Bottom ? editTools.Bottom + 5 : editTools.Top - options.Height - 5;
        options.Location = new Point(Math.Max(workArea.Left, Math.Min(x, workArea.Right - options.Width)), Math.Max(workArea.Top, y));
        options.BringToFront();
    }

    private static Point[] HandlePositions(Rectangle box) => new[]
    {
        new Point(box.Left, box.Top), new Point(box.Left + box.Width / 2, box.Top), new Point(box.Right, box.Top),
        new Point(box.Left, box.Top + box.Height / 2), new Point(box.Right, box.Top + box.Height / 2),
        new Point(box.Left, box.Bottom), new Point(box.Left + box.Width / 2, box.Bottom), new Point(box.Right, box.Bottom)
    };

    private Point GetResizeEdges(Point point)
    {
        if (mode == Mode.Free) return Point.Empty;
        var handles = HandlePositions(selection);
        for (int i = 0; i < handles.Length; i++)
            if (Math.Abs(handles[i].X - point.X) <= 8 && Math.Abs(handles[i].Y - point.Y) <= 8)
                return new Point(i is 0 or 3 or 5 ? -1 : i is 2 or 4 or 7 ? 1 : 0, i < 3 ? -1 : i > 4 ? 1 : 0);
        return Point.Empty;
    }

    private static Cursor CursorFor(Point edges)
    {
        if (edges.X == 0) return edges.Y == 0 ? Cursors.Cross : Cursors.SizeNS;
        if (edges.Y == 0) return Cursors.SizeWE;
        return edges.X == edges.Y ? Cursors.SizeNWSE : Cursors.SizeNESW;
    }

    internal static Rectangle ResizeSelection(Rectangle start, Rectangle bounds, Point edges, Point delta)
    {
        const int min = 5;
        int left = edges.X < 0 ? Math.Max(bounds.Left, Math.Min(start.Left + delta.X, start.Right - min)) : start.Left;
        int right = edges.X > 0 ? Math.Min(bounds.Right, Math.Max(start.Right + delta.X, start.Left + min)) : start.Right;
        int top = edges.Y < 0 ? Math.Max(bounds.Top, Math.Min(start.Top + delta.Y, start.Bottom - min)) : start.Top;
        int bottom = edges.Y > 0 ? Math.Min(bounds.Bottom, Math.Max(start.Bottom + delta.Y, start.Top + min)) : start.Bottom;
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private void ConfigureEditToolbar()
    {
        editTools.AddAppMark();
        editTools.Cursor = Cursors.SizeAll;
        editTools.MoveRequested += position =>
        {
            toolbarPosition = position;
            PositionToolbars();
        };
        AddEditTool(CaptureAnnotations.Tool.Rectangle, "\uE003", "مستطيل", "Rectangle");
        AddEditTool(CaptureAnnotations.Tool.Ellipse, "\uEA3A", "دائرة", "Ellipse");
        AddEditTool(CaptureAnnotations.Tool.Arrow, "\uE8A7", "سهم", "Arrow");
        AddEditTool(CaptureAnnotations.Tool.Pen, "\uE70F", "قلم", "Pen");
        AddEditTool(CaptureAnnotations.Tool.Hide, "\uE9A6", "إخفاء", "Redact");
        AddEditTool(CaptureAnnotations.Tool.Text, "\uE8D2", "إضافة نص", "Add text");
        editTools.IconButton("\uE7A7", "تراجع (Ctrl+Z)", "Undo (Ctrl+Z)", Undo);
        editTools.IconButton("\uE7A8", "إعادة التحديد", "Reselect", Reselect);
        editTools.IconButton("\uE896", "تصدير PNG", "Export PNG", Export);
        editTools.IconButton("\uE711", "إلغاء (Esc)", "Cancel (Esc)", Close);
        editTools.IconButton("\uE73E", "حفظ ونسخ (Enter)", "Save and copy (Enter)", AcceptSelection, primary: true);
        SelectTool(CaptureAnnotations.Tool.Pen, showOptions: false);
    }

    private void AddEditTool(CaptureAnnotations.Tool tool, string glyph, string ar, string en) =>
        editButtons[tool] = editTools.IconButton(glyph, ar, en, () => SelectTool(tool));

    private void SelectTool(CaptureAnnotations.Tool tool, bool showOptions = true)
    {
        FinishText(true);
        annotations.Selected = tool;
        selectedToolButton = editButtons[tool];
        foreach (var pair in editButtons) CaptureToolbar.SetActive(pair.Value, pair.Key == tool);
        var style = toolStyles.TryGetValue(tool, out var saved) ? saved : (CaptureToolbar.Accent, 3f);
        annotations.Ink = style.Item1;
        annotations.Width = style.Item2;
        ShowToolOptions(showOptions);
    }

    private readonly Dictionary<Color, Button> swatches = new();
    private ComboBox? strokeWidth;

    private void ConfigureOptions()
    {
        foreach (Color color in PresetColors)
        {
            string label = "#" + color.ToArgb().ToString("X8").Substring(2);
            var swatch = options.IconButton("●", label, label, () => SetInk(color));
            swatch.ForeColor = color;
            swatches.Add(color, swatch);
        }
        customSwatch = options.IconButton("◉", "عجلة الألوان", "Color wheel", PickColor);
        strokeWidth = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 65,
            Margin = new Padding(4, 6, 3, 0),
            AccessibleName = CaptureToolbar.Display("السماكة", "Stroke width")
        };
        foreach (int width in new[] { 1, 2, 3, 5, 8, 12 }) strokeWidth.Items.Add(width);
        strokeWidth.SelectedIndexChanged += (_, __) =>
        {
            if (strokeWidth.SelectedItem is int width)
            {
                annotations.Width = width;
                toolStyles[annotations.Selected] = (annotations.Ink, width);
            }
        };
        var widthLabel = new Label
        {
            AutoSize = true, ForeColor = Color.White, Margin = new Padding(5, 9, 0, 0),
            Text = CaptureToolbar.Display("السماكة", "Width")
        };
        options.Controls.Add(widthLabel);
        options.Controls.Add(strokeWidth);
        strokeWidth.VisibleChanged += (_, __) => widthLabel.Visible = strokeWidth.Visible;
    }

    private void ShowToolOptions(bool visible)
    {
        var tool = annotations.Selected;
        options.Visible = visible && tool != CaptureAnnotations.Tool.Hide;
        if (!options.Visible) return;
        strokeWidth!.Visible = tool != CaptureAnnotations.Tool.Text;
        strokeWidth.SelectedItem = (int)annotations.Width;
        UpdateSwatches();
        PositionToolbars();
    }

    private void UpdateSwatches()
    {
        foreach (var pair in swatches)
        {
            bool selected = pair.Key.ToArgb() == annotations.Ink.ToArgb();
            pair.Value.FlatAppearance.BorderSize = selected ? 1 : 0;
            pair.Value.FlatAppearance.BorderColor = Color.White;
        }
        if (customSwatch != null) customSwatch.ForeColor = annotations.Ink;
    }

    private void SetInk(Color color)
    {
        annotations.Ink = color;
        toolStyles[annotations.Selected] = (color, annotations.Width);
        UpdateSwatches();
    }

    private void PickColor()
    {
        var wheel = new CaptureColorWheel(annotations.Ink, SetInk);
        wheel.Show(customSwatch!, new Point(0, customSwatch!.Height));
    }

    private void Undo()
    {
        FinishText(true);
        annotations.Undo();
        Invalidate();
    }

    private void Reselect()
    {
        FinishText(false);
        annotations.Clear();
        selection = Rectangle.Empty;
        toolbarPosition = null;
        points.Clear();
        editTools.Visible = options.Visible = false;
        mode = Mode.Window;
        Cursor = Cursors.Cross;
        DetectWindow(PointToClient(Cursor.Position));
    }

    private void ReselectOrClose()
    {
        if (selection.IsEmpty) Close();
        else Reselect();
    }

    private Point ToImagePoint(Point point) => new(
        Math.Max(0, Math.Min(selection.Width, point.X - selection.X)),
        Math.Max(0, Math.Min(selection.Height, point.Y - selection.Y)));

    private void BeginText(Point position)
    {
        FinishText(true);
        textAnchor = ToImagePoint(position);
        textInput = new TextBox
        {
            Location = new Point(Math.Min(position.X, ClientSize.Width - 180), Math.Min(position.Y, ClientSize.Height - 28)),
            Width = 180,
            MaxLength = 160,
            AccessibleName = CaptureToolbar.Display("نص التعليق", "Annotation text")
        };
        textInput.LostFocus += (_, __) => FinishText(true);
        Controls.Add(textInput);
        textInput.BringToFront();
        textInput.Focus();
    }

    private void FinishText(bool save)
    {
        if (textInput == null) return;
        var input = textInput;
        textInput = null;
        if (save) annotations.AddText(textAnchor, input.Text);
        Controls.Remove(input);
        input.Dispose();
        Invalidate();
    }

    private void AcceptSelection()
    {
        if (selection.IsEmpty) return;
        FinishText(true);
        RegionImage = RenderSelection();
        DialogResult = DialogResult.OK;
    }

    private void Export()
    {
        FinishText(true);
        using var dialog = new SaveFileDialog { FileName = "Qusasa.png", Filter = "PNG (*.png)|*.png", DefaultExt = "png", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var image = RenderSelection();
            image.Save(dialog.FileName, ImageFormat.Png);
        }
        catch (Exception ex) when (ImageCodec.IsImagingError(ex) || ex is UnauthorizedAccessException)
        {
            MessageBox.Show(this, CaptureToolbar.Display("تعذّر حفظ الصورة.", "Could not save the image."));
        }
    }

    private Bitmap RenderSelection()
    {
        using var path = SelectionPath();
        return Crop(desktop, selection, mode == Mode.Free ? path : null, graphics => annotations.Draw(graphics, false));
    }

    internal static Bitmap Crop(Bitmap source, Rectangle region, GraphicsPath? mask, Action<Graphics>? draw = null)
    {
        var crop = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        try
        {
            using (var graphics = Graphics.FromImage(crop))
            {
                graphics.DrawImageUnscaled(source, -region.X, -region.Y);
                draw?.Invoke(graphics);
            }
            if (mask != null) ApplyMask(crop, region, mask);
            return crop;
        }
        catch { crop.Dispose(); throw; }
    }

    private static void ApplyMask(Bitmap crop, Rectangle region, GraphicsPath path)
    {
        var bounds = new Rectangle(Point.Empty, crop.Size);
        using var coverage = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(coverage))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(-region.X, -region.Y);
            graphics.FillPath(Brushes.White, path);
        }
        var mask = coverage.LockBits(bounds, ImageLockMode.ReadOnly, coverage.PixelFormat);
        try
        {
            var target = crop.LockBits(bounds, ImageLockMode.ReadWrite, crop.PixelFormat);
            try
            {
                var maskRow = new int[bounds.Width];
                var pixels = new int[bounds.Width];
                for (int y = 0; y < bounds.Height; y++)
                {
                    var row = IntPtr.Add(target.Scan0, y * target.Stride);
                    Marshal.Copy(IntPtr.Add(mask.Scan0, y * mask.Stride), maskRow, 0, bounds.Width);
                    Marshal.Copy(row, pixels, 0, bounds.Width);
                    for (int x = 0; x < bounds.Width; x++)
                    {
                        int alpha = (int)((uint)maskRow[x] >> 24);
                        pixels[x] = alpha == 0 ? 0 : (pixels[x] & 0x00ffffff) | (alpha << 24);
                    }
                    Marshal.Copy(pixels, 0, row, bounds.Width);
                }
            }
            finally { crop.UnlockBits(target); }
        }
        finally { coverage.UnlockBits(mask); }
    }

    protected override bool ProcessCmdKey(ref Message message, Keys key)
    {
        if ((key & Keys.KeyCode) == Keys.Escape)
        {
            if (textInput != null) FinishText(false);
            else Close();
            return true;
        }
        if (textInput != null)
        {
            if (key == Keys.Enter) { FinishText(true); return true; }
            return base.ProcessCmdKey(ref message, key);
        }
        if (!selection.IsEmpty && key == (Keys.Control | Keys.Z)) { Undo(); return true; }
        if (!selection.IsEmpty && key == Keys.Enter && !editTools.ContainsFocus) { AcceptSelection(); return true; }
        return base.ProcessCmdKey(ref message, key);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { desktop?.Dispose(); RegionImage?.Dispose(); }
        base.Dispose(disposing);
    }

    private delegate bool WindowEnumerator(IntPtr handle, IntPtr state);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { internal int Left, Top, Right, Bottom; }
    [DllImport("user32")] private static extern bool EnumWindows(WindowEnumerator callback, IntPtr state);
    [DllImport("user32")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr handle);
    [DllImport("user32")] private static extern IntPtr GetWindow(IntPtr handle, uint command);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr handle, StringBuilder name, int length);
    [DllImport("user32", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr handle, int index);
    [DllImport("user32")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32")] private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32")] private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
    [DllImport("dwmapi", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int GetFrameBounds(IntPtr handle, int attribute, out NativeRect value, int length);
    [DllImport("dwmapi")] private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out int value, int length);
}
