namespace CShell.Core.VtParser;

public interface IVtHandler
{
    void Print(int codepoint);
    void Execute(byte controlCode);
    void CsiDispatch(byte final, ReadOnlySpan<int> parameters, ReadOnlySpan<byte> intermediates);
    void EscDispatch(byte final, ReadOnlySpan<byte> intermediates);
    void OscDispatch(ReadOnlySpan<byte> payload);
}
