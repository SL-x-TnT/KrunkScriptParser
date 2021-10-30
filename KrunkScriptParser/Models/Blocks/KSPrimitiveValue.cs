using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSPrimitiveValue : IKSValue
    {
        public KSType Type { get; set; }
        public string Value { get; set; }
    }
}
