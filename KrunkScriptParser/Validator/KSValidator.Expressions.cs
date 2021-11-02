using KrunkScriptParser.Models;
using KrunkScriptParser.Models.Blocks;
using KrunkScriptParser.Models.Expressions;
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
        private KSExpression ParseExpression(bool insideGroup = false, int depth = 0)
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

                    expression.Items.Add(ParseExpression(true));

                    continue;
                }


                ExpressionOperator op = null;

                if (expression.Items.LastOrDefault() is KSExpression)
                {
                    if (TryReadOperator(out op))
                    {
                        expression.Items.Add(op);

                        continue;
                        //ReadConversions();
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
                    if(op.IsAssignment)
                    {
                        if(expression.HasAssignment)
                        {
                            AddValidationException($"Invalid operator '{op.Operator}'", op.Line, op.Column);
                        }
                        else
                        {
                            bool hasVariable = false;
                            bool showedError = false;

                            foreach(ExpressionItem item in expression.Items)
                            {
                                if(item is ExpressionValue v)
                                {
                                    if (!hasVariable && v.Value is KSVariableName variable)
                                    {
                                        hasVariable = true;

                                        continue;
                                    }
                                }
                                else if(item is ForceConversion conversion && conversion.ValidLeftHand)
                                {
                                    continue;
                                }

                                showedError = true;
                                AddValidationException($"Invalid left-hand side in assignment", item.Line, item.Column);

                                break;
                            }

                            if(!hasVariable && !showedError)
                            {
                                AddValidationException($"Invalid left-hand side in assignment");
                            }
                        }

                        expression.HasAssignment = true;
                    }

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

        private KSType DetermineExpressionType(List<ExpressionItem> items)
        {
            KSType currentType = KSType.Unknown;

            LinkedList<ExpressionItem> linkedList = new LinkedList<ExpressionItem>();

            items.ForEach(x => linkedList.AddLast(x));

            int currentPriority = ExpressionItem.MaxPriority;

            while (linkedList.Count > 1)
            {
                LinkedListNode<ExpressionItem> currentNode = linkedList.First;

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
                            ExpressionItem leftItem = currentNode.Previous?.Value;
                            ExpressionItem rightItem = currentNode.Next?.Value;

                            ValidateValues(op, leftItem, rightItem);


                            if (op.ReturnType != null)
                            {
                                currentType = op.ReturnType;
                            }
                            else
                            {
                                currentType = leftItem.Type; //Uses left type as it either failed validation or they're the same
                            }

                            //Add new node
                            LinkedListNode<ExpressionItem> newNode = linkedList.AddAfter(currentNode, new ExpressionValue
                            {
                                Type = currentType,
                            });

                            //Remove the 3 nodes and add own
                            linkedList.Remove(leftItem);
                            linkedList.Remove(rightItem);
                            linkedList.Remove(currentNode);

                            currentNode = newNode;
                        }
                        else if (currentNode.Value is KSExpression expression)
                        {
                            //Should already be handled
                            expression.Priority = 0;
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

            LinkedListNode<ExpressionItem> HandleConversion(LinkedListNode<ExpressionItem> node)
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

                if(op.Priority == int.MaxValue)
                {
                    AddValidationException($"Unexpected value '{op.Operator}' found");

                    return false;
                }

                return true;
            }

            return false;
        }

        private bool TryReadCast(out ForceConversion conversion)
        {
            conversion = null;

            if(_token.Value == "!")
            {
                conversion = new ForceConversion(KSType.Bool, true);
                conversion.Line = _token.Line;
                conversion.Column = _token.Column;

                _iterator.Next();

                return true;
            }
            else if(_token.Value == "~")
            {
                conversion = new ForceConversion(KSType.Number, false);
                conversion.Line = _token.Line;
                conversion.Column = _token.Column;

                _iterator.Next();

                return true;
            }
            else if(_token.Value == "(" && _iterator.PeekNext().Type == TokenTypes.Type)
            {
                _iterator.Next();

                conversion = new ForceConversion(ParseType(), false, null, true);
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

        private void ValidateValues(ExpressionOperator op, ExpressionItem left, ExpressionItem right)
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
                //Special condition
                if (op.Operator != "=" || leftType != KSType.Any)
                {
                    AddValidationException($"Mismatched types. Expected '{leftType?.FullType}'. Received '{leftType?.FullType} {op.Operator} {rightType?.FullType}'", op.Line, op.Column);
                }
            }

            if(!op.ValidTypes.Contains(rightType))
            {
                //Special condition
                if (op.Operator != "=" || leftType != KSType.Any)
                {
                    string expected = $"'{String.Join("' or '", op.ValidTypes)}'";

                    AddValidationException($"Expected {expected} with operator '{op.Operator}'. Received '{leftType.FullType} {op.Operator} {rightType.FullType}'", op.Line, op.Column);
                }
            }
        }

        private string ParseOperator()
        {
            string op = String.Empty;

            // ! may be used to convert a type to a bool, but it shouldn't be at the end of an operator
            while ((_token.Type == TokenTypes.Operator || _token.Type == TokenTypes.Assign) && (String.IsNullOrEmpty(op) || _token.Value != "!"))
            {
                //Possibly negative value and not --. Ignoring + unary operator
                if (!String.IsNullOrEmpty(op) && _token.Value == "-" && op.Last() != '-')
                {
                    break;
                }

                op += _token.Value;
                _iterator.Next();
            }

            return op;
        }
    }
}
