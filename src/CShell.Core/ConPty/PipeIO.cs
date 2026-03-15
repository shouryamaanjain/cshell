using Microsoft.Win32.SafeHandles;

namespace CShell.Core.ConPty;

internal sealed class PipeIO : IDisposable
{
    private readonly FileStream _stream;

    public PipeIO(SafeFileHandle handle, FileAccess access)
    {
        // CRITICAL: anonymous pipes from CreatePipe are synchronous handles.
        // isAsync must be false, otherwise ReadAsync returns 0 immediately.
        _stream = new FileStream(handle, access, bufferSize: 4096, isAsync: false);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return _stream.Read(buffer, offset, count);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        _stream.Write(data);
        _stream.Flush();
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
