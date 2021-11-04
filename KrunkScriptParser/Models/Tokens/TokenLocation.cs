using System;
using System.Collections.Generic;
using System.Text;

namespace KrunkScriptParser.Models.Tokens
{
    public class TokenLocation
    {
        public int Line { get; private set; }
        public int Column { get; private set; }
        public int TokenLength { get; private set; }
        public int ColumnEnd => Column + TokenLength;

        public TokenLocation(Token token)
        {
            Line = token?.Line ?? 0;
            Column = token?.Column ?? 0;
            TokenLength = token?.Value.Length ?? 0;
        }
    }
}
