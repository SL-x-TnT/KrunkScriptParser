using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSGroup : IKSValue
    {
        public KSType Type { get; set; }
        public List<IKSValue> Values { get; private set; } = new List<IKSValue>();
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
