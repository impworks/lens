using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.Text;

namespace Lens.Rider.Backend.Psi.Parsing;

/// <remarks>
/// The entire buffer is a single TEXT token, then EOF. There is nothing to gain from splitting it:
/// no highlighting, no completion and no navigation is served from the backend.
/// </remarks>
public class LensLexer(IBuffer buffer) : ILexer<int>
{
    private const int AtToken = 0;
    private const int AtEof = 1;

    private int _position = AtToken;

    public void Start() => _position = buffer.Length > 0 ? AtToken : AtEof;

    public void Advance() => _position = AtEof;

    public int CurrentPosition
    {
        get => _position;
        set => _position = value;
    }

    object ILexer.CurrentPosition
    {
        get => CurrentPosition;
        set => CurrentPosition = (int) value;
    }

    public TokenNodeType TokenType => _position == AtToken ? LensTokenType.TEXT : null;

    public int TokenStart => _position == AtToken ? 0 : buffer.Length;

    public int TokenEnd => buffer.Length;

    public IBuffer Buffer => buffer;
}

public class LensLexerFactory : ILexerFactory
{
    public ILexer CreateLexer(IBuffer buffer) => new LensLexer(buffer);
}
