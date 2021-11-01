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
        private KSExpression ParseExpressionNew(bool insideGroup = false, int depth = 0)
        {
            KSExpression expression = new KSExpression
            {
                Line = _token.Line,
                Column = _token.Column
            };

            /*
            ReadConversions();

            //New group
            if(_token.Value == "(")
            {
                _iterator.Next();

                expression.Items.Add(ParseExpressionNew(true));
            }*/

            //Read remaining items
            while(_token.Value != ")" && _token.Type != TokenTypes.Terminator)
            {
                ReadConversions();

                //New group
                if (_token.Value == "(")
                {
                    _iterator.Next();

                    expression.Items.Add(ParseExpressionNew(true));

                    continue;
                }


                ExpressionOperator op = null;

                if (expression.Items.LastOrDefault() is KSExpression)
                {
                    if (TryReadOperator(out op))
                    {
                        expression.Items.Add(op);

                        ReadConversions();
                    }
                    else
                    {
                        //TODO VERIFY LATER
                    }
                }
                else if (_token.Type == TokenTypes.Operator && _token.Value == "-")
                {
                    //Treat it as a cast?
                    expression.Items.Add(new ForceConversion(KSType.Number, false)
                    {
                        Line = _token.Line,
                        Column = _token.Column
                    });

                    _iterator.Next();
                    //Let the parse value handle any unexpected values
                }

                ExpressionValue value = new ExpressionValue
                {
                    Value = ParseValue(depth),
                    Line = _token.Line,
                    Column = _token.Column
                };

                value.Type = value.Value.Type;

                expression.Items.Add(value);

                //End of object
                if(_token.Type == TokenTypes.Terminator)
                {
                    continue;
                }

                _iterator.Next();

                if(TryReadOperator(out op))
                {
                    expression.Items.Add(op);
                }
                else
                {
                    //Should be done
                    break;
                }
            }

            //Should have a ) needing to be read
            if(insideGroup)
            {
                if(_token.Value != ")")
                {
                    AddValidationException($"Missing ')' at end of group");
                }
                else
                {
                    _iterator.Next();
                }
            }

            //Determine the type
            expression.Type = DetermineExpressionType(expression.Items);

            return expression;

            void ReadConversions()
            {
                while (TryReadCast(out ForceConversion conversion))
                {
                    expression.Items.Add(conversion);
                }
            }
        }

        private KSType DetermineExpressionType(List<IExpressionItem> items)
        {
            KSType currentType = KSType.Unknown;

            LinkedList<IExpressionItem> linkedList = new LinkedList<IExpressionItem>();

            items.ForEach(x => linkedList.AddLast(x));

            int currentPriority = IExpressionItem.MaxPriority;

            while (linkedList.Count > 1)
            {
                LinkedListNode<IExpressionItem> currentNode = linkedList.First;

                while(currentNode != linkedList.Last && currentNode != null)
                {
                    if(currentNode.Value.Priority == currentPriority)
                    {
                        if(currentNode.Value is ForceConversion forceConversion)
                        {
                            currentNode = HandleConversion(currentNode);
                            currentType = currentNode.Value.Type;
                        }
                        else if(currentNode.Value is ExpressionOperator op)
                        {
                            IExpressionItem leftItem = currentNode.Previous?.Value;
                            IExpressionItem rightItem = currentNode.Next?.Value;

                            ValidateValuesNew(op, leftItem, rightItem);

                            if (OperatorReturnsBool(op.Operator))
                            {
                                currentType = KSType.Bool;
                            }
                            else
                            {
                                currentType = leftItem.Type; //Uses left type as it either failed validation or they're the same
                            }

                            //Add new node
                            LinkedListNode<IExpressionItem> newNode = linkedList.AddAfter(currentNode, new ExpressionValue
                            {
                                Type = currentType,
                            });

                            //Remove the 3 nodes and add own
                            linkedList.Remove(leftItem);
                            linkedList.Remove(rightItem);
                            linkedList.Remove(currentNode);

                            currentNode = newNode;
                        }
                    }
                    else if (currentNode.Value.Priority > currentPriority)
                    {
                        //Something went wrong
                    }

                    currentNode = currentNode.Next;
                }

                currentPriority--;
            }

            return linkedList.Last.Value.Type;

            LinkedListNode<IExpressionItem> HandleConversion(LinkedListNode<IExpressionItem> node)
            {
                KSType newType = KSType.Unknown;

                while(node.Value is ForceConversion)
                {
                    node = node.Next;
                }

                while (node.Previous?.Value is ForceConversion forceConversion)
                {
                    if (!forceConversion.IsValid(node.Value.Type))
                    {
                        if (forceConversion.Type == KSType.LengthOf)
                        {
                            AddValidationException($"lengthOf expects an array, '{KSType.String.FullType}', or '{KSType.Any}'. Received '{node.Value.Type}'");
                        }
                        else if (forceConversion.Type == KSType.NotEmpty)
                        {
                            AddValidationException($"notEmpty expects an '{KSType.Object}' or '{KSType.Any}'. Received '{node.Value.Type}'");
                        }
                        else
                        {
                            AddValidationException($"Invalid cast from '{node.Value.Type}' to '{forceConversion.ReturnType.FullType}'");
                        }
                    }

                    newType = forceConversion.ReturnType;

                    node.Value.Type = forceConversion.ReturnType;

                    //Should end up as a normal value
                    node.Value.Priority = 0;

                    node.List.Remove(node.Previous);
                }


                return node;
            }
        }

        private bool TryReadOperator(out ExpressionOperator op)
        {
            op = null;

            string sOp = ParseOperator();

            if(!String.IsNullOrEmpty(sOp))
            {
                op = new ExpressionOperator(sOp)
                {
                    Line = _token.Line,
                    Column = _token.Column
                };

                return true;
            }

            return false;
        }

        private bool TryReadCast(out ForceConversion conversion)
        {
            conversion = null;

            if(_token.Value == "(" && _iterator.PeekNext().Type == TokenTypes.Type)
            {
                _iterator.Next();

                conversion = new ForceConversion(ParseType(), false);
                conversion.Line = _token.Line;
                conversion.Column = _token.Column;

                if(_token.Value != ")")
                {
                    AddValidationException($"Missing ')' on '{conversion.Type}' cast");
                }

                _iterator.Next();

                return true;
            }
            else if (_token.Type == TokenTypes.KeyMethod)
            {
                switch (_token.Value)
                {
                    case "toNum":
                        conversion = new ForceConversion(KSType.Number, true);
                        break;
                    case "toStr":
                        conversion = new ForceConversion(KSType.String, true);
                        break;
                    case "lengthOf":
                        conversion = new ForceConversion(KSType.LengthOf, false, KSType.Number);
                        break;
                    case "notEmpty":
                        conversion = new ForceConversion(KSType.NotEmpty, false, KSType.Bool);
                        break;
                    default:
                        throw new ValidationException($"Unexpected '{_token.Value}' statement", _token.Line, _token.Column);
                }

                conversion.Line = _token.Line;
                conversion.Column = _token.Column;

                _iterator.Next();

                return true;
            }

            return false;
        }

        // Checks whether the left and right values of an operator are the correct + same type
        private void ValidateValuesNew(ExpressionOperator op, IExpressionItem left, IExpressionItem right)
        {
            if(!left.HasType)
            {
                AddValidationException($"Expected a value with a type", left.Line, left.Column, willThrow: true);
            }

            if (!right.HasType)
            {
                AddValidationException($"Expected a value with a type", left.Line, left.Column, willThrow: true);
            }

            KSType leftType = left.Type;
            KSType rightType = right.Type;

            if (leftType != rightType)
            {
                AddValidationException($"Mismatched types. Expected '{leftType?.FullType}'. Received '{leftType?.FullType} {op.Operator} {rightType?.FullType}'", op.Line, op.Column);
            }

            switch (op.Operator)
            {
                case "+":
                    if (rightType != KSType.String && rightType != KSType.Number)
                    {
                        AddValidationException($"Expected '{KSType.String.FullType}' or '{KSType.Number.FullType}' with operator '{op.Operator}'. Received '{leftType.FullType} {op.Operator} {rightType.FullType}'", op.Line, op.Column);
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
                        AddValidationException($"Expected '{KSType.Number.FullType}' with operator '{op.Operator}'. Received '{leftType.FullType} {op.Operator} {rightType.FullType}'", op.Line, op.Column);
                    }
                    break;
                case "&&":
                case "||":
                    if (rightType != KSType.Bool)
                    {
                        AddValidationException($"Expected '{KSType.Bool.FullType}' with operator '{op.Operator}'. Received '{leftType.FullType} {op.Operator} {rightType.FullType}'", op.Line, op.Column);
                    }
                    break;
            }
        }

        /// <summary>
        /// Checks whether the left and right values of an operator are the correct + same type
        /// </summary>
        private void ValidateValues(string op, IKSValue leftValue, IKSValue rightValue)
        {
            KSType leftType = leftValue.Type;
            KSType rightType = rightValue.Type;

            if (leftValue != null)
            {
                if (leftType != rightType && !String.IsNullOrEmpty(op))
                {
                    AddValidationException($"Mismatched types. Expected '{leftType.FullType}'. Received '{leftType.FullType} {op} {rightType.FullType}'");
                }
            }

            if (!String.IsNullOrEmpty(op))
            {
                switch (op)
                {
                    case "+":
                        if (rightType != KSType.String && rightType != KSType.Number)
                        {
                            AddValidationException($"Expected '{KSType.String.FullType}' or '{KSType.Number.FullType}' with operator '{op}'. Received '{leftType.FullType} {op} {rightType.FullType}'");
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

        private string ParseOperator()
        {
            string op = String.Empty;

            // ! may be used to convert a type to a bool, but it shouldn't be at the end of an operator
            while ((_token.Type == TokenTypes.Operator || _token.Type == TokenTypes.Assign) && (String.IsNullOrEmpty(op) || _token.Value != "!"))
            {
                //Possibly negative value and not --
                if (!String.IsNullOrEmpty(op) && _token.Value == "-" && op.Last() != '-')
                {
                    break;
                }

                op += _token.Value;
                _iterator.Next();
            }

            return op;
        }

        /// <summary>
        /// Handles validation of conversions/casts
        /// </summary>
        private void ValidateForcedType(KSType currentType, List<ForceConversion> types)
        {
            foreach (ForceConversion forceConversion in types.Reverse<ForceConversion>())
            {
                bool valid = forceConversion.IsValid(currentType);

                if (!valid)
                {
                    if (forceConversion.Type == KSType.LengthOf)
                    {
                        AddValidationException($"lengthOf expects an array, '{KSType.String.FullType}', or '{KSType.Any}'. Received '{currentType.FullType}'");
                    }
                    else if (forceConversion.Type == KSType.NotEmpty)
                    {
                        AddValidationException($"notEmpty expects an '{KSType.Object}' or '{KSType.Any}'. Received '{currentType.FullType}'");
                    }
                    else
                    {
                        AddValidationException($"Invalid cast from '{currentType.FullType}' to '{forceConversion.ReturnType.FullType}'");
                    }
                }


                currentType = forceConversion.ReturnType;
            }
        }

        private bool OperatorReturnsBool(string op)
        {
            switch (op)
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
}
