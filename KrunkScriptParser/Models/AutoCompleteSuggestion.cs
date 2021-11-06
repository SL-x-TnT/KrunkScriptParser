using System;
using System.Collections.Generic;
using System.Text;

namespace KrunkScriptParser.Models
{
    public enum SuggestionType
    {
        Text = 1,
        Method,
        Function,
        Constructor,
        Field,
        Variable,
        Class,
        Interface,
        Module,
        Property,
        Unit,
        Value,
        Enum,
        Keyword,
        Snippet,
        Color,
        File,
        Reference,
        Folder,
        EnumMember,
        Constant,
        Struct,
        Event,
        Operator,
        TypeParameter
    };

    public class AutoCompleteSuggestion
    {
        public string Text { get; set; }
        public string Details { get; set; }
        public SuggestionType Type { get; set; } = SuggestionType.Method;
        public string InsertTextFormat { get; set; }
    }
}
