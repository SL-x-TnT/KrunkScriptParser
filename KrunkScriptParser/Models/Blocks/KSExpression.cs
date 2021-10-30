using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSExpression : IKSValue
    {
        public KSType Type { get; set; }
        public KSType ForcedType { get; set; }
        public IKSValue Value { get; set; }

        public KSType CurrentType => ForcedType ?? Type;
    }
}
