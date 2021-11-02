using KrunkScriptParser.Models.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Expressions
{
    public class ExpressionOperator : ExpressionItem
    {
        public string Operator { get; set; }
        public HashSet<KSType> ValidTypes { get; private set; } = new HashSet<KSType>();
        public bool ArrayValid { get; private set; }
        public KSType ReturnType { get; set; }

        public ExpressionOperator(string op)
        {
            Operator = op;
            Initialize();
        }

        private void Initialize()
        {
            //Groups are the top priority
            switch (Operator ?? "")
            {
                case ".": //Member access
                    ValidTypes.Add(KSType.Object);
                    ValidTypes.Add(KSType.Any);
                    Priority = MaxPriority - 1;
                    ReturnType = KSType.Any;
                    break;
                case "[": //Array index
                    ValidTypes.Add(KSType.Any);
                    ArrayValid = true;
                    Priority = MaxPriority - 1;
                    break;
                case "**": //Power
                    ValidTypes.Add(KSType.Number);
                    Priority = MaxPriority - 2;
                    break;
                case "+":
                    ValidTypes.Add(KSType.Number);
                    ValidTypes.Add(KSType.String);
                    Priority = MaxPriority - 3;
                    break;
                case "-":
                case "/":
                case "*":
                    ValidTypes.Add(KSType.Number);
                    Priority = MaxPriority - 3;
                    break;
                case "<<":
                case ">>":
                case ">>>":
                    ValidTypes.Add(KSType.Number);
                    Priority = MaxPriority - 4;
                    break;
                case ">":
                case "<":
                case ">=":
                case "<=":
                    ValidTypes.Add(KSType.Number);
                    ReturnType = KSType.Bool;
                    Priority = MaxPriority - 5;
                    break;
                case "==":
                case "!=":
                    ValidTypes.Add(KSType.Bool);
                    ValidTypes.Add(KSType.String);
                    ValidTypes.Add(KSType.Number);
                    ReturnType = KSType.Bool;
                    Priority = MaxPriority - 6;
                    break;
                case "&&":
                case "||":
                    ValidTypes.Add(KSType.Bool);
                    ReturnType = KSType.Bool;
                    Priority = MaxPriority - 7;
                    break;
                default:
                    Priority = int.MaxValue; //Invalid operators
                    break;
            }
        }

        public override bool HasType => false;
    }
}
