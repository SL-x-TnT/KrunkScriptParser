using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class ExpressionValue : IExpressionItem
    {
        public IKSValue Value { get; set; }
        public override bool HasType => true;
        public override int Priority { get; set; }
    }

    public class ExpressionOperator : IExpressionItem
    {
        public string Operator { get; set; }
        public KSType LeftType { get; set; }
        public KSType RightType { get; set; }
        public KSType ReturnType { get; set; }

        public ExpressionOperator(string op)
        {
            Operator = op;
            GetPriority();
        }

        private int GetPriority()
        {
            switch (Operator ?? "")
            {
                case ">":
                case "<":
                case ">=":
                case "<=":
                    return MaxPriority - 2;
                case "&&":
                case "||":
                case "==":
                    return MaxPriority - 3;
                default:
                    return MaxPriority - 4;
            }
        }

        public override bool HasType => false;
    }

    public abstract class IExpressionItem
    {
        public const int MaxPriority = 4;
        public abstract bool HasType { get; }
        public KSType Type { get; set; }
        public virtual int Priority { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
