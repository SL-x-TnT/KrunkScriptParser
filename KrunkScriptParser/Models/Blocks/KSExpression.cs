using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSExpression : IExpressionItem, IKSValue
    {
        public IKSValue Value { get; set; }

        public override bool HasType => true;
        public List<IExpressionItem> Items { get; private set; } = new List<IExpressionItem>();

        public KSExpression()
        {
            Priority = MaxPriority;
        }
    }
}
