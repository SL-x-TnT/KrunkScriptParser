using KrunkScriptParser.Models.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Expressions
{
    public class ExpressionValue : ExpressionItem
    {
        public IKSValue Value { get; set; }
        public override bool HasType => true;
        public override int Priority { get; set; }
    }
}
