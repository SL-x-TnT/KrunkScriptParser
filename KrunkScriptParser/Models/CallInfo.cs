using KrunkScriptParser.Models.Blocks;
using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.Text;

namespace KrunkScriptParser.Models
{
    public class CallInfo
    {
        public TokenLocation CallLocation { get; set; }
        public bool Global { get; set; }
    }
}
