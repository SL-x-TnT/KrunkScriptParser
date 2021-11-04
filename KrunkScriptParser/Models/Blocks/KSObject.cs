using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSObject : IKSValue
    {
        public KSType Type { get; set; } = KSType.Object;
        public Dictionary<string, IKSValue> Properties { get; private set; } = new Dictionary<string, IKSValue>();
        public TokenLocation TokenLocation { get; set; }
    }
}
