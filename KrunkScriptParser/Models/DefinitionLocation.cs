using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.Text;

namespace KrunkScriptParser.Models
{
    public class DefinitionLocation
    {
        public TokenLocation StartLocation { get; set; }
        public TokenLocation EndLocation { get; set; }
    }
}
