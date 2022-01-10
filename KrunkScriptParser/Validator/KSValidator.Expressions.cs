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
                TokenLocation = new TokenLocation(_token)
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
            while(_token.Value != ")" && _token.Value != "," && _token.Type != TokenTypes.Terminator)
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
                        TokenLocation = new TokenLocation(_token)
                    });

                    _iterator.Next();

                    // New group expression  -(1 + 2)
                    if(_token.Value == "(")
                    {
                        expression.Items.Add(ParseExpression());

                        continue;
                    }

                    //Let the parse value handle any unexpected values
                }

                ExpressionValue value = new ExpressionValue
                {
                    Value = ParseValue(depth),
                    TokenLocation = new TokenLocation(_token.Prev)
                };

                value.Type = new KSType(value.Value.Type);

                expression.Items.Add(value);

                //End of object
                if(_token.Type == TokenTypes.Terminator)
                {
                    continue;
                }
                
                if (depth == 0 || (_token.Value != "," && _token.Value != "]"))
                {
                    //Console.WriteLine($"{_token.Value} | {value.Value.GetType()}");

                    //_iterator.Next();
                }

                if(TryReadOperator(out op))
                {
                    if(op.IsAssignment)
                    {
                        if(expression.HasAssignment)
                        {
                            AddValidationException($"Invalid operator '{op.Operator}'", op.TokenLocation);
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
                                    // = operator can't have casts to the left
                                    if(op.Operator == "=")
                                    {
                                        if (!showedError)
                                        {
                                            AddValidationException($"Invalid left-hand side in assignment", conversion.TokenLocation, conversion.EndTokenLocation);
                                            showedError = true;
                                        }
                                    }

                                    continue;
                                }

                                if (!showedError)
                                {
                                    AddValidationException($"Invalid left-hand side in assignment", item.TokenLocation);
                                    showedError = true;
                                }

                                    break;
                            }

                            if(!hasVariable && !showedError)
                            {
                                AddValidationException($"Invalid left-hand side in assignment", op.TokenLocation);
                            }
                        }

                        expression.HasAssignment = true;
                    }
                    else if(op.IsPostfix)
                    {
                        bool hasVariable = false;
                        bool showedError = false;

                        if(insideGroup)
                        {
                            AddValidationException($"Invalid group around operator '{op.Operator}'", expression.Items.First().TokenLocation, expression.Items.Last().EndTokenLocation ?? expression.Items.Last().TokenLocation);

                            showedError = true;
                        }

                        foreach (ExpressionItem item in expression.Items)
                        {
                            if (item is ExpressionValue v)
                            {
                                if (!hasVariable && v.Value is KSVariableName variable)
                                {
                                    hasVariable = true;

                                    continue;
                                }
                            }
                            else if (item is ForceConversion conversion && conversion.ReturnType == KSType.Number)
                            {
                                continue;
                            }

                            showedError = true;
                            AddValidationException($"Invalid input with {op.Operator}", item.TokenLocation);

                            break;
                        }

                        if (!hasVariable && !showedError)
                        {
                            AddValidationException($"Invalid input with {op.Operator}", op.TokenLocation);
                        }

                        expression.HasPostfix = true;
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
                    AddValidationException($"Missing ')' at end of group", _token.Prev);
                }
                else
                {
                    _iterator.Next();
                }

                if(_token.Value == ".")
                {
                    AddValidationException($"Invalid member property access. Assign to a new variable first", _token);

                    _iterator.SkipUntil(TokenTypes.Terminator);
                }
                else if (_token.Value == "[")
                {
                    AddValidationException($"Invalid array access. Assign to a new variable first", _token);
                    _iterator.SkipUntil(TokenTypes.Terminator);
                }
            }

            //Determine the type
            expression.Type = DetermineExpressionType(expression.Items) ?? KSType.Unknown;

            if(expression.Items.Count == 0 || (expression.HasAssignment && expression.Items.Count == 2))
            {
                AddValidationException($"Empty expression found for assignment", _token.Prev, _token);
            }

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

                while(currentNode != null)
                {
                    if(currentNode.Value.Priority == currentPriority)
                    {
                        if (currentNode.Value is ForceConversion forceConversion)
                        {
                            currentNode = HandleConversion(currentNode);
                            currentType = currentNode.Value.Type;
                        }
                        else if (currentNode.Value is ExpressionOperator op)
                        {
                            ExpressionItem leftItem = currentNode.Previous?.Value;
                            ExpressionItem rightItem = null;

                            if (!op.IsTernaryCondition)
                            {
                                rightItem = currentNode.Next?.Value;
                            }

                            ValidateValues(op, leftItem, rightItem);


                            if (op.ReturnType != null)
                            {
                                currentType = op.ReturnType;
                            }
                            else
                            {
                                currentType = leftItem.Type; //Uses left type as it either failed validation or they're the same
                            }

                            LinkedListNode<ExpressionItem> newNode = new LinkedListNode<ExpressionItem>(new ExpressionValue
                            {
                                Type = currentType,
                                TokenLocation = leftItem?.TokenLocation ?? rightItem?.TokenLocation,
                                EndTokenLocation = rightItem?.EndTokenLocation ?? leftItem?.EndTokenLocation
                            });

                            LinkedListNode<ExpressionItem> prevNode = currentNode;

                            //Add new node, if it's not a ternary '?' op
                            if (!op.IsTernaryCondition)
                            {
                                linkedList.AddAfter(currentNode, newNode);
                                currentNode = newNode;
                            }
                            else
                            {
                                currentNode = currentNode.Next;
                            }

                            //Remove the nodes used
                            linkedList.Remove(leftItem);
                            linkedList.Remove(rightItem);
                            linkedList.Remove(prevNode);

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
                        AddValidationException($"Failed to determine expression type", items.First().TokenLocation, items.Last().TokenLocation);

                        return KSType.Unknown;
                    }

                    currentNode = currentNode.Next;
                }

                currentPriority--;
            }

            return linkedList.Last?.Value?.Type;

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
                            AddValidationException($"lengthOf expects an array, '{KSType.String.FullType}', or '{KSType.Any}'. Received '{node.Value.Type}'", forceConversion.TokenLocation, forceConversion.EndTokenLocation);
                        }
                        else if (forceConversion.Type == KSType.NotEmpty)
                        {
                            AddValidationException($"notEmpty expects an '{KSType.Object}' or '{KSType.Any}'. Received '{node.Value.Type}'", forceConversion.TokenLocation, forceConversion.EndTokenLocation);
                        }
                        else if (forceConversion.IsConvert)
                        {
                            //Add in valid types
                            AddValidationException($"Invalid convert from '{node.Value.Type}' to '{forceConversion.ReturnType.FullType}'", forceConversion.TokenLocation, forceConversion.EndTokenLocation);
                        }
                        else
                        {
                            AddValidationException($"Invalid cast from '{node.Value.Type}' to '{forceConversion.ReturnType.FullType}'", forceConversion.TokenLocation, forceConversion.EndTokenLocation);
                        }
                    }
                    else if(forceConversion.Type == KSType.Bool && node.Value.Type == KSType.Object)
                    {
                        AddValidationException($"Object types will always return false when using '!'. Use 'notEmpty' to check for an empty object", forceConversion.TokenLocation, forceConversion.EndTokenLocation, Level.Warning);
                    }
                    else if (forceConversion.Type == node.Value.Type && !forceConversion.IsConvert && forceConversion.IsTypeCast)
                    {
                        AddValidationException($"Unnecessary cast to '{forceConversion.Type}'", forceConversion.TokenLocation, forceConversion.EndTokenLocation, Level.Info);
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
                    TokenLocation = new TokenLocation(_token)
                };

                if(op.Priority == int.MaxValue)
                {
                    AddValidationException($"Unexpected value '{op.Operator}' found", op.TokenLocation);

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
                conversion.TokenLocation = new TokenLocation(_token);

                _iterator.Next();

                return true;
            }
            else if (_token.Value == "~") //Treating as a cast
            {
                conversion = new ForceConversion(KSType.Number, false);
                conversion.TokenLocation = new TokenLocation(_token);

                _iterator.Next();

                return true;
            }
            else if(_token.Value == "(" && _iterator.PeekNext().Type == TokenTypes.Type)
            {
                TokenLocation tokenStart = new TokenLocation(_token);

                _iterator.Next();

                conversion = new ForceConversion(ParseType(), false, null, true, true);
                conversion.TokenLocation = tokenStart;
                conversion.EndTokenLocation = new TokenLocation(_token);

                if (_token.Value != ")")
                {
                    AddValidationException($"Missing ')' on '{conversion.Type}' cast", _token.Prev);
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
                        AddValidationException($"Unexpected '{_token.Value}' statement", _token, willThrow: true);
                        break;
                }

                conversion.TokenLocation = new TokenLocation(_token);

                _iterator.Next();

                return true;
            }

            return false;
        }

        private void ValidateValues(ExpressionOperator op, ExpressionItem left, ExpressionItem right)
        {
            if(left?.HasType == false)
            {
                AddValidationException($"Expected a value with a type", left.TokenLocation, willThrow: true);
            }

            if (right?.HasType == false)
            {
                AddValidationException($"Expected a value with a type", right.TokenLocation, willThrow: true);
            }

            KSType leftType = left?.Type ?? KSType.Unknown;
            KSType rightType = right?.Type ?? KSType.Unknown;
            bool showedError = false;
            ExpressionItem rightItem = right ?? left;

            if(op.IsPostfix)
            {
                if(leftType != KSType.Number)
                {
                    AddValidationException($"Mismatched type. Expected '{KSType.Number}'. Received '{leftType?.FullType}{op.Operator}'", left.TokenLocation, rightItem.EndTokenLocation ?? rightItem.TokenLocation);
                }
            }
            else if (op.IsTernaryCondition)
            {
                if(leftType != KSType.Bool)
                {
                    AddValidationException($"Invalid ternary condition. Expected '{KSType.Bool}'. Received '{leftType?.FullType}'", left.TokenLocation, rightItem.EndTokenLocation ?? rightItem.TokenLocation);
                }
            }
            else if (leftType != rightType)
            {
                //Special condition
                if (op.Operator != "=" || leftType != KSType.Any)
                {
                    AddValidationException($"Mismatched types. Expected '{leftType?.FullType}'. Received '{leftType?.FullType} {op.Operator} {rightType?.FullType}'", left.TokenLocation, rightItem.EndTokenLocation ?? rightItem.TokenLocation);

                    showedError = true;
                }
            }
            
            //PATCH: Exit early on assignments with valid types
            if(op.Operator == "=" && leftType == rightType)
            {
                return;
            }

            if(!op.IsPostfix && !op.IsTernaryCondition && !op.ValidTypes.Contains(rightType))
            {
                //Special condition
                if (op.Operator != "=" || leftType != KSType.Any)
                {
                    if (!showedError)
                    {
                        string expected = $"'{String.Join("' or '", op.ValidTypes)}'";

                        AddValidationException($"Expected {expected} with operator '{op.Operator}'. Received '{leftType.FullType} {op.Operator} {rightType.FullType}'", left.TokenLocation, rightItem.EndTokenLocation ?? rightItem.TokenLocation);
                    }
                }
            }
        }

        private string ParseOperator()
        {
            string op = String.Empty;

            // ! may be used to convert a type to a bool, but it shouldn't be at the end of an operator
            while ((_token.Type == TokenTypes.Operator || _token.Type == TokenTypes.Assign) && (String.IsNullOrEmpty(op) || (_token.Value != "!" && _token.Value != "~")))
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
