using KrunkScriptParser.Models.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSParameter : KSVariable
    {
        public bool MultiProp { get; set; }
        public bool Optional { get; set; }
        public bool IsHookParameter { get; set; }
    }
}
