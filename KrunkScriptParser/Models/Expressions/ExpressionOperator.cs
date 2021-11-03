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
        private static HashSet<KSType> _allTypes = new HashSet<KSType> { KSType.Bool, KSType.String, KSType.Bool, KSType.Any, KSType.Number, KSType.Object };
        private static Dictionary<string, HashSet<KSType>> _assignmentOperators = new Dictionary<string, HashSet<KSType>>
        {
            { "+", new HashSet<KSType>{KSType.String, KSType.Number } },
            {"-", new HashSet<KSType>{KSType.String, KSType.Number } },
            {"**", new HashSet<KSType>{ KSType.Number } },
            {"*", new HashSet<KSType>{ KSType.Number } },
            {"/", new HashSet<KSType>{KSType.Number } } ,
            {"%",  new HashSet<KSType>{KSType.Number } },
            {"<<", new HashSet<KSType>{KSType.Number } },
            {">>", new HashSet<KSType>{ KSType.Number } },
            {">>>", new HashSet<KSType>{ KSType.Number } },
            {"|", new HashSet<KSType>{KSType.Number } },
            {"^", new HashSet<KSType>{KSType.Number } },
            {"&", new HashSet<KSType>{KSType.Number } },
            {"&&", new HashSet<KSType>{KSType.Bool } },
            {"||", new HashSet<KSType>{KSType.Bool } },
            {"=", _allTypes},
            {"++", new HashSet<KSType>{KSType.Number } },
            {"--", new HashSet<KSType>{KSType.Number } },
        };


        public string Operator { get; set; }
        public HashSet<KSType> ValidTypes { get; private set; } = new HashSet<KSType>();
        public bool ArrayValid { get; private set; }
        public KSType ReturnType { get; set; }
        public bool IsAssignment { get; set; }
        public bool IsPostfix => Operator == "++" || Operator == "--";
        public bool IsTernaryCondition => Operator == "?";

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
                case ".": //Member access -- Not used yet
                    ValidTypes.Add(KSType.Object);
                    ValidTypes.Add(KSType.Any);
                    Priority = MaxPriority - 1;
                    ReturnType = KSType.Any;
                    break;
                case "[": //Array index -- Not used yet
                    ValidTypes.Add(KSType.Any);
                    ArrayValid = true;
                    Priority = MaxPriority - 1;
                    break;
                case "++":
                case "--":
                    ValidTypes = _assignmentOperators[Operator];
                    Priority = MaxPriority - 2;
                    break;
                case "**": //Power
                    ValidTypes = _assignmentOperators[Operator];
                    Priority = MaxPriority - 3;
                    break;
                case "+":
                case "-":
                case "/":
                case "%":
                case "*":
                    ValidTypes = _assignmentOperators[Operator];
                    Priority = MaxPriority - 4;
                    break;
                case "<<":
                case ">>":
                case ">>>":
                    ValidTypes = _assignmentOperators[Operator];
                    Priority = MaxPriority - 5;
                    break;
                case ">":
                case "<":
                case ">=":
                case "<=":
                    ValidTypes.Add(KSType.Number);
                    ReturnType = KSType.Bool;
                    Priority = MaxPriority - 6;
                    break;
                case "==":
                case "!=":
                    ValidTypes.Add(KSType.Bool);
                    ValidTypes.Add(KSType.String);
                    ValidTypes.Add(KSType.Number);
                    ReturnType = KSType.Bool;
                    Priority = MaxPriority - 7;
                    break;
                case "&":
                case "^":
                case "|":
                    ValidTypes = _assignmentOperators[Operator];
                    Priority = MaxPriority - 8;
                    break;
                case "&&":
                case "||":
                    ValidTypes = _assignmentOperators[Operator];
                    ReturnType = KSType.Bool;
                    Priority = MaxPriority - 9;
                    break;
                case "?": //Ternary (condition ? true : false)
                    ValidTypes.Add(KSType.Bool);
                    Priority = MaxPriority - 10;
                    break;
                case ":": //
                    ValidTypes = _allTypes;
                    Priority = MaxPriority - 10;
                    break;
                default:
                    //Possibly assignment operators
                    if(Operator?.EndsWith("=") == true)
                    {
                        string op = Operator;

                        if (op.Length > 1)
                        {
                            op = Operator[0..^1];
                        }

                        if (_assignmentOperators.TryGetValue(op, out HashSet<KSType> validTypes))
                        {
                            IsAssignment = true;
                            ValidTypes = validTypes;
                            Priority = MaxPriority - 11;
                            break;
                        }
                    }

                    Priority = int.MaxValue; //Invalid operators
                    break;
            }
        }

        public override bool HasType => false;
    }
}
