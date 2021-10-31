using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSLoopBlock : KSBlock
    {
        public IKSValue Assignment { get; set; } //For loops 
        public KSExpression Condition { get; set; }
        public KSExpression Increment { get; set; }//For loops 
    }
}
