using System.Text;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wmux.Core.Buffer;
using Wmux.Core.Terminal;
using Windows.UI;

namespace Wmux.App.Controls;

public sealed class TerminalCanvas : UserControl
{
    private readonly TerminalSession _session;
    private readonly CanvasControl _canvas;
    private CanvasTextFormat? _textFormat;
    private CanvasTextFormat? _boldFormat;
    private float _cellWidth;
    private float _cellHeight;
    private bool _measured;
    private readonly DispatcherTimer _blinkTimer;
    private bool _cursorVisible = true;

    public int TerminalColumns { get; private set; } = 80;
    public int TerminalRows { get; private set; } = 24;

    public TerminalCanvas(TerminalSession session)
    {
        _session = session;

        _canvas = new CanvasControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _canvas.Draw += OnDraw;
        _canvas.CreateResources += OnCreateResources;

        this.Content = _canvas;
        this.HorizontalAlignment = HorizontalAlignment.Stretch;
        this.VerticalAlignment = VerticalAlignment.Stretch;
        this.SizeChanged += OnControlSizeChanged;

        _session.Redraw += OnTerminalRedraw;

        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _blinkTimer.Tick += (s, e) => { _cursorVisible = !_cursorVisible; _canvas.Invalidate(); };
        _blinkTimer.Start();
    }

    public void StartShell(string shellPath, string? workingDirectory = null)
    {
        try { _session.Start(shellPath, workingDirectory); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Shell start failed: {ex.Message}"); }
    }

    public void InvalidateCanvas() => _canvas.Invalidate();

    private void OnCreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
    {
        _textFormat = new CanvasTextFormat
        {
            FontFamily = "Cascadia Mono, Consolas, Courier New",
            FontSize = 14,
            WordWrapping = CanvasWordWrapping.NoWrap,
        };
        _boldFormat = new CanvasTextFormat
        {
            FontFamily = "Cascadia Mono, Consolas, Courier New",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            WordWrapping = CanvasWordWrapping.NoWrap,
        };
        MeasureCell(sender);
        RecalculateSize();
    }

    private void MeasureCell(CanvasControl sender)
    {
        if (_textFormat == null) return;

        using var layout = new CanvasTextLayout(sender, "MM", _textFormat, 10000, 10000);
        var pos = layout.GetCaretPosition(1, false);
        _cellWidth = pos.X;
        _cellHeight = (float)layout.LayoutBounds.Height;

        if (_cellWidth < 2) _cellWidth = 8.4f;
        if (_cellHeight < 4) _cellHeight = 18f;

        _measured = true;
    }

    private void OnControlSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_measured) RecalculateSize();
    }

    private void RecalculateSize()
    {
        if (!_measured || _cellWidth <= 0 || _cellHeight <= 0) return;

        double w = _canvas.ActualWidth > 0 ? _canvas.ActualWidth : ActualWidth;
        double h = _canvas.ActualHeight > 0 ? _canvas.ActualHeight : ActualHeight;
        if (w <= 0 || h <= 0) return;

        int newCols = Math.Max(1, (int)(w / _cellWidth));
        int newRows = Math.Max(1, (int)(h / _cellHeight));

        if (newCols != TerminalColumns || newRows != TerminalRows)
        {
            TerminalColumns = newCols;
            TerminalRows = newRows;
            _session.Resize(newCols, newRows);
        }
    }

    private void OnTerminalRedraw()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _session.AcknowledgeRedraw();
            _canvas.Invalidate();
        });
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        if (_textFormat == null || !_measured) { ds.Clear(Color.FromArgb(255, 30, 30, 30)); return; }

        var buffer = _session.Buffer;
        ds.Clear(ColorFromUint(buffer.DefaultBg));

        _session.ProcessBuffer(() =>
        {
            int rows = Math.Min(buffer.Rows, TerminalRows);
            int cols = Math.Min(buffer.Columns, TerminalColumns);

            for (int row = 0; row < rows; row++)
            {
                var line = buffer.GetLine(row);
                float y = row * _cellHeight;

                // Background pass
                for (int col = 0; col < cols; col++)
                {
                    ref var cell = ref line[col];
                    uint bg = cell.BackgroundRgb;
                    if ((cell.Attributes & CellAttributes.Inverse) != 0) bg = cell.ForegroundRgb;
                    if (bg != buffer.DefaultBg)
                        ds.FillRectangle(col * _cellWidth, y, _cellWidth, _cellHeight, ColorFromUint(bg));

                    if (buffer.Selection.Contains(row, col))
                        ds.FillRectangle(col * _cellWidth, y, _cellWidth, _cellHeight, Color.FromArgb(100, 0, 145, 255));
                }

                // Text pass — draw full row using font-engine spacing
                DrawTextRow(ds, line, cols, y, buffer.DefaultFg, buffer.DefaultBg);
            }

            // Cursor
            if (buffer.Cursor.Visible && _cursorVisible &&
                buffer.Cursor.Row >= 0 && buffer.Cursor.Row < rows &&
                buffer.Cursor.Col >= 0 && buffer.Cursor.Col < cols)
            {
                float cx = buffer.Cursor.Col * _cellWidth;
                float cy = buffer.Cursor.Row * _cellHeight;
                var cc = Color.FromArgb(200, 220, 220, 220);
                switch (buffer.Cursor.Shape)
                {
                    case CursorShape.Block: ds.FillRectangle(cx, cy, _cellWidth, _cellHeight, cc); break;
                    case CursorShape.Underline: ds.FillRectangle(cx, cy + _cellHeight - 3, _cellWidth, 3, cc); break;
                    case CursorShape.Bar: ds.FillRectangle(cx, cy, 2, _cellHeight, cc); break;
                }
            }
        });
    }

    private void DrawTextRow(CanvasDrawingSession ds, TerminalLine line, int cols, float y, uint defaultFg, uint defaultBg)
    {
        var sb = new StringBuilder(cols);
        for (int col = 0; col < cols; col++)
        {
            ref var cell = ref line[col];
            int cp = cell.Codepoint;
            if (cp > 0x20 && cp < 0xD800 && (cell.Attributes & CellAttributes.Hidden) == 0)
                sb.Append((char)cp);
            else if (cp > 0xFFFF && (cell.Attributes & CellAttributes.Hidden) == 0)
            { try { sb.Append(char.ConvertFromUtf32(cp)); } catch { sb.Append(' '); } }
            else
                sb.Append(' ');
        }

        string rowText = sb.ToString();
        if (string.IsNullOrWhiteSpace(rowText)) return;

        int runStart = 0;
        while (runStart < cols)
        {
            ref var startCell = ref line[runStart];
            uint runFg = startCell.ForegroundRgb;
            bool runBold = (startCell.Attributes & CellAttributes.Bold) != 0;
            if ((startCell.Attributes & CellAttributes.Inverse) != 0) runFg = startCell.BackgroundRgb;

            int runEnd = runStart + 1;
            while (runEnd < cols)
            {
                ref var next = ref line[runEnd];
                uint nfg = next.ForegroundRgb;
                bool nb = (next.Attributes & CellAttributes.Bold) != 0;
                if ((next.Attributes & CellAttributes.Inverse) != 0) nfg = next.BackgroundRgb;
                if (nfg != runFg || nb != runBold) break;
                runEnd++;
            }

            int charStart = runStart;
            int charLen = Math.Min(runEnd - runStart, rowText.Length - charStart);
            if (charStart < rowText.Length && charLen > 0)
            {
                string runText = rowText.Substring(charStart, charLen);
                if (!string.IsNullOrWhiteSpace(runText))
                {
                    float x = runStart * _cellWidth;
                    var fmt = runBold ? _boldFormat! : _textFormat!;
                    using var layout = new CanvasTextLayout(ds, runText, fmt, cols * _cellWidth, _cellHeight);
                    ds.DrawTextLayout(layout, x, y, ColorFromUint(runFg));
                }
            }
            runStart = runEnd;
        }
    }

    private static Color ColorFromUint(uint argb)
    {
        return Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
    }
}
