using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSConditionalBlock : KSBlock
    {
        public string Key { get; set; }
        public KSExpression Condition { get; set; }

        public bool IsIf => Key == "if";
        public bool IsElseIf => Key == "else if";
    }
}
