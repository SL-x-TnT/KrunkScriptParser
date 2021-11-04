using KrunkScriptParser.Helpers;
using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    class KSComment : IKSValue
    {
        public string Text { get; private set; }
        public KSType Type { get; set; }
        public TokenLocation TokenLocation { get; set; }
    }
}
