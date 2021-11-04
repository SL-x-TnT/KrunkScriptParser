using KrunkScriptParser.Models.Expressions;
using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSExpression : ExpressionItem, IKSValue, IKSEndToken
    {
        public IKSValue Value { get; set; }

        public override bool HasType => true;
        public List<ExpressionItem> Items { get; private set; } = new List<ExpressionItem>();
        public bool HasAssignment { get; set; }
        public bool HasPostfix { get; set; }
        public TokenLocation EndTokenLocation => Items.LastOrDefault()?.TokenLocation;

        public KSExpression()
        {
            Priority = MaxPriority;
        }
    }
}
