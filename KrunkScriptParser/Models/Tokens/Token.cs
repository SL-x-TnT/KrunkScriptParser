using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Tokens
{
    public class Token
    {
        public TokenTypes Type { get; set; }
        public string Value { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public Token Prev { get; set; }
        public Token Next { get; set; }
        public int ColumnEnd => Column + (Value ?? String.Empty).Length;
    }
}
