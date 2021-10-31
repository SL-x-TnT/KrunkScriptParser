using KrunkScriptParser.Models;
using KrunkScriptParser.Models.Blocks;
using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Validator
{
    public partial class KSValidator
    {
        /// <summary>
        /// Parses an entire expression. Ex: num a = 1 + 1
        /// Recursively parses inner expression. Ex: num a = (1 + 1) + 1;
        /// </summary>
        private KSExpression ParseExpression(IKSValue prevValue = null, int depth = 0)
        {
            KSExpression expression = new KSExpression();

            List<(KSType, bool)> forcedTypes = new List<(KSType, bool)>();

            while (_token.Type == TokenTypes.KeyMethod)
            {
                switch (_token.Value)
                {
                    case "toNum":
                        forcedTypes.Add((KSType.Number, true));
                        break;
                    case "toStr":
                        forcedTypes.Add((KSType.String, true));
                        break;
                    default:
                        throw new ValidationException($"Unexpected '{_token.Value}' statement", _token.Line, _token.Column);
                }

                _iterator.Next();
            }

            if (_token.Type == TokenTypes.Punctuation)
            {
                while (_token.Value == "(")
                {
                    //Check to see if it's a cast
                    _iterator.Next();

                    if (_token.Type == TokenTypes.Type)
                    {
                        forcedTypes.Add((ParseType(), false));

                        if (_token.Type != TokenTypes.Punctuation || _token.Value != ")")
                        {
                            AddValidationException($"Missing ')'");
                        }
                        else
                        {
                            _iterator.Next();
                        }
                    }
                    else //New Group
                    {
                        KSExpression innerExpression = ParseExpression();

                        expression.Value = innerExpression;
                        expression.Type = innerExpression.CurrentType;

                        ValidateForcedType(expression.CurrentType, forcedTypes);
                        expression.ForcedType = forcedTypes.LastOrDefault().Item1;

                        return expression;
                    }
                }
            }

            //Bool converts
            while (_token.Type == TokenTypes.Operator && _token.Value == "!")
            {
                forcedTypes.Add((KSType.Bool, true));

                _iterator.Next();
            }

            expression.Value = ParseGroup(null, forcedTypes);
            expression.Type = expression.Value.Type;

            if (forcedTypes?.Count > 0)
            {
                ValidateForcedType(expression.CurrentType, forcedTypes);
                expression.ForcedType = forcedTypes.FirstOrDefault().Item1;
            }

            return expression;

        }

        /// <summary>
        /// Parses a group. Groups are values within parentheses
        /// </summary>
        /// <returns></returns>
        private IKSValue ParseGroup(KSExpression value = null, List<(KSType, bool)> initialTypes = null)
        {
            IKSValue leftValue = value;

            KSGroup group = new KSGroup();
            group.Type = value?.CurrentType ?? initialTypes?.LastOrDefault().Item1;

            if (value != null)
            {
                group.Values.Add(value);
            }

            while (_token.Type != TokenTypes.Terminator && //Line ends
                _token.Value != ")" &&                     //Group ends
                _token.Value != "," &&                     //Object property ends
                _token.Value != "}")                       //Object property ends
            {
                string op = ParseOperator();

                if (!String.IsNullOrEmpty(op) && (leftValue == null || !IsValidOperator(op)))
                {
                    AddValidationException($"Invalid operator '{op}'");
                }

                IKSValue rightValue = null;

                //New group / cast, parse expression
                if (_token.Value == "(" || _token.Value == "!" || _token.Type == TokenTypes.KeyMethod)
                {
                    rightValue = ParseExpression(leftValue);
                }
                else
                {
                    rightValue = ParseValue();

                    if (initialTypes != null && initialTypes.Count > 0)
                    {
                        ValidateForcedType(rightValue.Type, initialTypes);
                        rightValue.Type = initialTypes.FirstOrDefault().Item1;
                        initialTypes = null;
                    }
                }

                group.Values.Add(rightValue);

                if (leftValue != null)
                {
                    ValidateValues(op, leftValue, rightValue);
                }

                if (group.Type == null)
                {
                    group.Type = rightValue.Type;
                }
                else if(op == "==") //Equality, so the group type should be a bool
                {
                    group.Type = KSType.Bool;
                }

                leftValue = rightValue;

                if (_token.Type != TokenTypes.Terminator && _token.Type != TokenTypes.Operator)
                {
                    _iterator.Next();
                }

            }

            return group;

            // Returns whether or not the operator is valid
            bool IsValidOperator(string op)
            {
                switch (op)
                {
                    case "+":
                    case "-":
                    case "/":
                    case "*":
                    case "<":
                    case ">":
                    case "<<":
                    case ">>":
                    case "<=":
                    case ">=":
                    case ">>>":
                    case "|":
                    case "&":
                    case "||":
                    case "&&":
                    case "==":
                    case "!=":
                        return true;
                }

                return false;
            }

            // Checks whether the left and right values of an operator are the correct + same type
            void ValidateValues(string op, IKSValue leftValue, IKSValue rightValue)
            {
                KSType leftType = leftValue.Type;
                KSType rightType = rightValue.Type;

                if (leftValue is KSExpression lv)
                {
                    leftType = lv.CurrentType;
                }

                if (rightValue is KSExpression rv)
                {
                    rightType = rv.CurrentType;
                }

                if (leftValue != null)
                {
                    //PATCH: Verify the right group isn't an operation that returns a bool
                    if (op == "&&" || op == "||")
                    {
                        Token currentToken = _token;

                        _iterator.Next();

                        string tempOp = ParseOperator();

                        _iterator.ReturnTo(currentToken);

                        if(OperatorReturnsBool(tempOp))
                        {
                            rightType = KSType.Bool;
                        }
                    }

                    if (leftType != rightType)
                    {
                        AddValidationException($"Mismatched types. Expected '{leftType.FullType}'. Received {leftType.FullType} {op} {rightType.FullType}");
                    }
                }

                if (!String.IsNullOrEmpty(op))
                {
                    switch (op)
                    {
                        case "+":
                            if (rightType != KSType.String && rightType != KSType.Number)
                            {
                                AddValidationException($"Expected '{KSType.String.FullType}' or '{KSType.Number.FullType}' with operator '{op}'. Received {leftType.FullType} {op} {rightType.FullType}");
                            }

                            break;
                        case "-":
                        case "*":
                        case "/":
                        case "<<":
                        case ">>":
                        case "<<<":
                        case "<=":
                        case ">=":
                            if (rightType != KSType.Number)
                            {
                                AddValidationException($"Expected '{KSType.Number.FullType}' with operator '{op}'. Received {leftType.FullType} {op} {rightType.FullType}");
                            }
                            break;
                        case "&&":
                        case "||":
                            if (rightType != KSType.Bool)
                            {
                                AddValidationException($"Expected '{KSType.Bool.FullType}' with operator '{op}'. Received {leftType.FullType} {op} {rightType.FullType}");
                            }
                            break;
                    }
                }
            }

            bool OperatorReturnsBool(string op)
            {
                switch(op)
                {
                    case "<":
                    case ">":
                    case "<=":
                    case ">=":
                    case "==":
                    case "!=":
                        return true;
                }

                return false;
            }
        }

        private string ParseOperator()
        {
            string op = String.Empty;

            // ! may be used to convert a type to a bool, but it shouldn't be at the end of an operator
            while ((_token.Type == TokenTypes.Operator || _token.Type == TokenTypes.Assign) && (String.IsNullOrEmpty(op) || _token.Value != "!"))
            {
                op += _token.Value;
                _iterator.Next();
            }

            return op;
        }

        /// <summary>
        /// Handles validation of conversions/casts
        /// </summary>
        private void ValidateForcedType(KSType currentType, List<(KSType, bool)> types)
        {
            foreach ((KSType t, bool force) type in types.Reverse<(KSType, bool)>())
            {
                //Was a cast, verify there's no type changes
                if (!type.force && currentType != KSType.Any)
                {
                    if (currentType != type.t)
                    {
                        AddValidationException($"Invalid cast from '{currentType.FullType}' to '{type.t.FullType}'");
                    }
                }

                currentType = type.t;
            }
        }
    }
}
