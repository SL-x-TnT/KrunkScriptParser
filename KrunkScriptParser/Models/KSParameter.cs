using KrunkScriptParser.Models.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models
{
    public class KSParameter : IKSValue
    {
        public KSType Type { get; set; }
        public string Name { get; set; }
        public bool MultiProp { get; set; }
    }
}
