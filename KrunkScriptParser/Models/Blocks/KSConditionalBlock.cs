using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSConditionalBlock : KSBlock
    {
        public KSExpression Condition { get; set; }
    }
}
