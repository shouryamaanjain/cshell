using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Wmux.Core.ConPty;

public sealed class ConPtySession : IDisposable
{
    private IntPtr _hPC;
    private IntPtr _hProcess;
    private IntPtr _hThread;
    private PipeIO? _writer;
    private PipeIO? _reader;
    private bool _disposed;

    public int ProcessId { get; private set; }
    public bool IsRunning => !_disposed && _hProcess != IntPtr.Zero;

    public void Create(int cols, int rows, string commandLine, string? workingDirectory = null)
    {
        // Create pipes for ConPTY <-> app communication
        var sa = new SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            bInheritHandle = false
        };

        if (!ConPtyNative.CreatePipe(out var inputReadSide, out var inputWriteSide, ref sa, 0))
            throw new InvalidOperationException($"CreatePipe (input) failed: {Marshal.GetLastWin32Error()}");

        if (!ConPtyNative.CreatePipe(out var outputReadSide, out var outputWriteSide, ref sa, 0))
        {
            inputReadSide.Dispose();
            inputWriteSide.Dispose();
            throw new InvalidOperationException($"CreatePipe (output) failed: {Marshal.GetLastWin32Error()}");
        }

        // Create pseudo console
        var size = new COORD((short)cols, (short)rows);
        int hr = ConPtyNative.CreatePseudoConsole(size, inputReadSide, outputWriteSide, 0, out _hPC);
        if (hr != 0)
        {
            inputReadSide.Dispose();
            inputWriteSide.Dispose();
            outputReadSide.Dispose();
            outputWriteSide.Dispose();
            throw new InvalidOperationException($"CreatePseudoConsole failed: 0x{hr:X8}");
        }

        // Create the child process attached to the pseudo console
        CreateAttachedProcess(commandLine, workingDirectory);

        // Close the sides of the pipes that the ConPTY owns
        inputReadSide.Dispose();
        outputWriteSide.Dispose();

        // Keep our sides for reading/writing
        _writer = new PipeIO(inputWriteSide, FileAccess.Write);
        _reader = new PipeIO(outputReadSide, FileAccess.Read);
    }

    private void CreateAttachedProcess(string commandLine, string? workingDirectory)
    {
        // Determine attribute list size
        var listSize = IntPtr.Zero;
        ConPtyNative.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref listSize);

        var attributeList = Marshal.AllocHGlobal(listSize);
        try
        {
            if (!ConPtyNative.InitializeProcThreadAttributeList(attributeList, 1, 0, ref listSize))
                throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");

            if (!ConPtyNative.UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    ConPtyNative.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    _hPC,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
                throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");

            var si = new STARTUPINFOEX
            {
                StartupInfo = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFOEX>() },
                lpAttributeList = attributeList
            };

            if (!ConPtyNative.CreateProcessW(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    ConPtyNative.EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero,
                    workingDirectory,
                    ref si,
                    out var pi))
                throw new InvalidOperationException($"CreateProcessW failed: {Marshal.GetLastWin32Error()}");

            _hProcess = pi.hProcess;
            _hThread = pi.hThread;
            ProcessId = pi.dwProcessId;
        }
        finally
        {
            ConPtyNative.DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(attributeList);
        }
    }

    public void Resize(int cols, int rows)
    {
        if (_hPC != IntPtr.Zero)
        {
            ConPtyNative.ResizePseudoConsole(_hPC, new COORD((short)cols, (short)rows));
        }
    }

    public void WriteInput(ReadOnlySpan<byte> data)
    {
        _writer?.Write(data);
    }

    public int ReadOutput(byte[] buffer)
    {
        if (_reader == null) return 0;
        try
        {
            return _reader.Read(buffer, 0, buffer.Length);
        }
        catch (IOException)
        {
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _writer?.Dispose();
        _reader?.Dispose();

        if (_hPC != IntPtr.Zero)
        {
            ConPtyNative.ClosePseudoConsole(_hPC);
            _hPC = IntPtr.Zero;
        }

        if (_hProcess != IntPtr.Zero)
        {
            ConPtyNative.TerminateProcess(_hProcess, 0);
            ConPtyNative.CloseHandle(_hProcess);
            _hProcess = IntPtr.Zero;
        }

        if (_hThread != IntPtr.Zero)
        {
            ConPtyNative.CloseHandle(_hThread);
            _hThread = IntPtr.Zero;
        }
    }
}
