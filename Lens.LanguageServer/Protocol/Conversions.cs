using Lens.LanguageServer.Core;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
// this assembly sits inside the Lens namespace, so the names the compiler also uses - SymbolKind,
// DiagnosticSeverity - have to be told apart explicitly
using SymbolKind = Lens.Analysis.SymbolKind;
using ProtocolSymbolKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind;
using ProtocolSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;

namespace Lens.LanguageServer.Protocol
{
    /// <summary>
    /// The boundary between what the language services say and what the protocol carries.
    ///
    /// Everything protocol-shaped lives on this side of it, so that the services stay usable by an
    /// editor plugin that never speaks the protocol at all.
    /// </summary>
    internal static class Conversions
    {
        /// <summary>
        /// The protocol's view of a range.
        /// </summary>
        public static Range ToRange(TextRange range)
        {
            return new Range(
                new Position(range.Start.Line, range.Start.Character),
                new Position(range.End.Line, range.End.Character)
            );
        }

        /// <summary>
        /// The services' view of a position.
        /// </summary>
        public static TextPosition ToPosition(Position position)
        {
            return new TextPosition(position.Line, position.Character);
        }

        /// <summary>
        /// How an editor should draw a problem.
        /// </summary>
        public static ProtocolSeverity ToSeverity(ProblemSeverity severity)
        {
            return severity == ProblemSeverity.Error
                ? ProtocolSeverity.Error
                : ProtocolSeverity.Warning;
        }

        /// <summary>
        /// The icon an editor shows beside a name.
        /// </summary>
        public static CompletionItemKind ToCompletionKind(SymbolKind kind)
        {
            switch (kind)
            {
                case SymbolKind.Local: return CompletionItemKind.Variable;
                case SymbolKind.Parameter: return CompletionItemKind.Variable;
                case SymbolKind.Function: return CompletionItemKind.Function;
                case SymbolKind.Record: return CompletionItemKind.Struct;
                case SymbolKind.RecordField: return CompletionItemKind.Field;
                case SymbolKind.AlgebraicType: return CompletionItemKind.Enum;
                case SymbolKind.TypeLabel: return CompletionItemKind.EnumMember;
                case SymbolKind.HostType: return CompletionItemKind.Class;
                case SymbolKind.GlobalVariable: return CompletionItemKind.Variable;
                case SymbolKind.Member: return CompletionItemKind.Method;
                case SymbolKind.Keyword: return CompletionItemKind.Keyword;
                default: return CompletionItemKind.Text;
            }
        }

        /// <summary>
        /// The icon an editor shows in the outline.
        /// </summary>
        public static ProtocolSymbolKind ToSymbolKind(SymbolKind kind)
        {
            switch (kind)
            {
                case SymbolKind.Local: return ProtocolSymbolKind.Variable;
                case SymbolKind.Parameter: return ProtocolSymbolKind.Variable;
                case SymbolKind.Function: return ProtocolSymbolKind.Function;
                case SymbolKind.Record: return ProtocolSymbolKind.Struct;
                case SymbolKind.RecordField: return ProtocolSymbolKind.Field;
                case SymbolKind.AlgebraicType: return ProtocolSymbolKind.Enum;
                case SymbolKind.TypeLabel: return ProtocolSymbolKind.EnumMember;
                case SymbolKind.HostType: return ProtocolSymbolKind.Class;
                case SymbolKind.GlobalVariable: return ProtocolSymbolKind.Variable;
                case SymbolKind.Member: return ProtocolSymbolKind.Method;
                default: return ProtocolSymbolKind.Namespace;
            }
        }
    }
}
