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
        public TokenLocation TokenLocation { get; set; }
    }
}
