using Wmux.Core.Buffer;
using Wmux.Core.ConPty;
using Wmux.Core.VtParser;

namespace Wmux.Core.Terminal;

public sealed class TerminalSession : IDisposable
{
    private readonly ConPtySession _conPty = new();
    private readonly VtStateMachine _parser;
    private readonly TerminalEmulator _emulator;
    private readonly OscParser _oscParser = new();
    private CancellationTokenSource _cts = new();
    private Thread? _readThread;
    private bool _disposed;

    // Redraw coalescing
    private volatile bool _redrawPending;
    private readonly object _redrawLock = new();

    public Guid Id { get; } = Guid.NewGuid();
    public TerminalBuffer Buffer { get; }
    public TerminalEmulator Emulator => _emulator;
    public long TotalBytesRead { get; private set; }

    public event Action? Redraw;
    public event Action<string>? TitleChanged;
    public event Action<string>? DirectoryChanged;
    public event Action<string, string>? NotificationReceived;

    public TerminalSession(int cols, int rows)
    {
        Buffer = new TerminalBuffer(cols, rows);
        _emulator = new TerminalEmulator(Buffer, _oscParser);
        _parser = new VtStateMachine(_emulator);

        _emulator.TitleChanged += title => TitleChanged?.Invoke(title);
        _oscParser.DirectoryChanged += dir => DirectoryChanged?.Invoke(dir);
        _oscParser.NotificationReceived += (title, body) => NotificationReceived?.Invoke(title, body);
    }

    public void Start(string shellPath, string? workingDirectory = null)
    {
        _conPty.Create(Buffer.Columns, Buffer.Rows, shellPath, workingDirectory);

        // Use a dedicated thread for synchronous blocking read on the pipe
        _readThread = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = "ConPTY-Read"
        };
        _readThread.Start();
    }

    private void ReadLoop()
    {
        var buf = new byte[8192];
        while (!_cts.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                bytesRead = _conPty.ReadOutput(buf);
            }
            catch
            {
                break;
            }

            if (bytesRead == 0) break;

            TotalBytesRead += bytesRead;

            lock (_redrawLock)
            {
                for (int i = 0; i < bytesRead; i++)
                    _parser.Advance(buf[i]);
            }

            if (!_redrawPending)
            {
                _redrawPending = true;
                Redraw?.Invoke();
            }
        }
    }

    public void ProcessBuffer(Action action)
    {
        lock (_redrawLock)
        {
            action();
        }
    }

    public void AcknowledgeRedraw()
    {
        _redrawPending = false;
    }

    public void SendInput(ReadOnlySpan<byte> data)
    {
        if (_disposed) return;
        Buffer.ScrollOffset = 0;
        _conPty.WriteInput(data);
    }

    public void SendText(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        SendInput(bytes);
    }

    public void Resize(int cols, int rows)
    {
        if (_disposed) return;
        lock (_redrawLock)
        {
            Buffer.Resize(cols, rows);
        }
        _conPty.Resize(cols, rows);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _conPty.Dispose();
        _readThread?.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }
}
