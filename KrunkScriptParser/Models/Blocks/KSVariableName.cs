using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSVariableName : IKSValue
    {
        public KSType Type { get; set; }
        public KSVariable Variable { get; set; }
        public KSAction Action { get; set; }
        public KSObject Object { get; set; }
        public TokenLocation TokenLocation { get; set; }
    }
}
