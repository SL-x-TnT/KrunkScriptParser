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
        /// Parses the declared value. Could be an object, num, str, etc
        /// </summary>
        private IKSValue ParseValue(int depth = 0)
        {
            if (_token.Type == TokenTypes.Terminator)
            {
                throw new ValidationException($"Variables", _token.Line, _token.Column);
            }

            if (_token.Type == TokenTypes.Punctuation)
            {
                //Object
                if (_token.Value == "{")
                {
                    IKSValue obj = ParseObject(depth);

                    return obj;
                }
            }
            else if (_token.Type == TokenTypes.Operator)
            {
                //Boolean conversion
                if (_token.Value == "!")
                {
                    //convertType = "bool";
                    //_iterator.Next();
                }
            }

            if (_token.Type == TokenTypes.String || _token.Type == TokenTypes.Number || _token.Type == TokenTypes.Bool)
            {
                KSPrimitiveValue value = new KSPrimitiveValue
                {
                    Value = _token.Value,
                    Type = _token.Type == TokenTypes.String ? KSType.String :
                                _token.Type == TokenTypes.Bool ? KSType.Bool : KSType.Number
                };

                return value;
            }
            else if (_token.Type == TokenTypes.Name || _token.Type == TokenTypes.GlobalObject) //Setting using another variable or GAME/UTILS
            {
                IKSValue variable = ParseName();

                KSVariableName variableName = new KSVariableName
                {
                    Type = new KSType(variable?.Type),
                    Variable = variable as KSVariable
                };

                if (_iterator.PeekNext().Value == "[")
                {
                    _iterator.Next();

                    while (TryReadIndexer(out IKSValue _))
                    {
                        variableName.Type.DecreaseDepth();

                        if (variableName.Type != KSType.Any && variableName.Type.ArrayDepth < 0 && variableName.Type != KSType.String)
                        {
                            AddValidationException($"Type '{variableName.Type}' can not be indexed");
                        }

                        _iterator.Next();
                    }
                }

                return variableName;
            }
            else if (_token.Type == TokenTypes.Type) //Arrays
            {
                return ParseArray();
            }
            else
            {
                AddValidationException($"Expected value. Found: {_token.Value}", willThrow: true);


                return null;
            }
        }

        private bool TryReadIndexer(out IKSValue value)
        {
            value = null;

            if (_token.Value != "[")
            {
                return false;
            }

            _iterator.Next();

            value = ParseExpression(depth: 1);

            if (_token.Value != "]")
            {
                AddValidationException($"Missing end of indexer ']'");
            }

            if(value.Type != KSType.Number)
            {
                AddValidationException($"Array indexer expects type '{KSType.Number}'. Received '{value.Type}'");
            }

            return true;
        }

        private KSObject ParseObject(int depth = 0)
        {
            _iterator.Next();

            KSObject ksObject = new KSObject();

            while ((_token.Type != TokenTypes.Punctuation || _token.Value != "}") && _token.Type != TokenTypes.Terminator)
            {
                //Property name
                if (_token.Type != TokenTypes.Name)
                {
                    throw new ValidationException($"Expected property name. Found: '{_token.Value}'", _token.Line, _token.Column);
                }

                string name = _token.Value;

                _iterator.Next();

                //Expecting :
                if (_token.Type != TokenTypes.Punctuation || _token.Value != ":")
                {
                    AddValidationException($"Expected ':' found '{_token.Value}'", willThrow: true);
                }

                _iterator.Next();

                //KSExpression expression = ParseExpression(depth: depth + 1);
                KSExpression expression = ParseExpression(depth: depth + 1);

                expression.Type = KSType.Any;

                if (!ksObject.Properties.TryAdd(name, expression))
                {
                    AddValidationException($"Property '{name}' already exists");

                    _iterator.SkipUntil(TokenTypes.Name | TokenTypes.Punctuation); //Skip until the next name, comma, or }
                }

                if (_token.Type == TokenTypes.Punctuation)
                {
                    if (_token.Value == "," || _token.Value == "}")
                    {
                        Token next = _iterator.PeekNext();

                        if (next.Value == "}" || next.Type == TokenTypes.Name)
                        {
                            _iterator.Next();
                        }
                    }
                    else if (_token.Value != "}") //A missing comma at the end is no issue
                    {
                        AddValidationException($"Unexpected value '{_token.Value}", column: _token.ColumnEnd);
                    }
                }
                else
                {
                    //We were missing a comma
                    if (_token.Type == TokenTypes.Name)
                    {
                        AddValidationException($"Missing ','", column: _token.ColumnEnd);
                    }
                    else if (_token.Type != TokenTypes.Terminator)
                    {
                        AddValidationException($"Unexpected value '{_token.Value}'", willThrow: true);
                    }
                }
            }

            return ksObject;
        }

        private KSArray ParseArray(int depth = 0)
        {
            KSArray ksArray = new KSArray();

            ksArray.Type = ParseType(true);

            //Empty array
            if (_token.Type == TokenTypes.Punctuation && _token.Value == "]")
            {
                return ksArray;
            }

            while (_token.Type == TokenTypes.Punctuation)
            {
                Token nextToken = _iterator.PeekNext();

                if (nextToken.Type == TokenTypes.Punctuation)
                {
                    //Skip nested arrays
                    if (nextToken.Value == "[")
                    {
                        _iterator.Next();

                        AddValidationException($"Nested arrays currently not validated", level: Level.Info);

                        int nestedDepth = 1;

                        //Attempt to save
                        while (nestedDepth > 0)
                        {
                            _iterator.Next();

                            if (_token.Type == TokenTypes.Punctuation)
                            {
                                if (_token.Value == "[")
                                {
                                    ++nestedDepth;
                                }
                                else if (_token.Value == "]")
                                {
                                    --nestedDepth;
                                }
                            }
                        }

                        return ksArray;
                    }

                    if (_token.Value == "]") //Allow it to continue
                    {
                        break;
                    }
                    else
                    {
                        throw new ValidationException($"Expected ']'. Found '{_token.Value}'", _token.Line, _token.ColumnEnd);
                    }
                }
            }

            //End of array declaration
            while (_token.Type != TokenTypes.Punctuation || _token.Value != "]")
            {
                IKSValue value = ParseExpression(depth: depth + 1);

                if (ksArray.Type.Name != value.Type.FullType)
                {
                    AddValidationException($"Expected type '{ksArray.Type.Name}'. Received '{value.Type.FullType}'", level: Level.Error);
                }

                ksArray.Values.Add(value);

                if (_token.Type == TokenTypes.Punctuation)
                {
                    if (_token.Value == ",")
                    {
                        _iterator.Next();
                    }
                    else if (_token.Value == "]")
                    {
                        //PATCH: Has some more indexers
                        while (_iterator.PeekNext().Value == "[")
                        {
                            _iterator.Next();

                            if (TryReadIndexer(out IKSValue _))
                            {
                                ksArray.Type.DecreaseDepth();

                                if(ksArray.Type.ArrayDepth < 0 && ksArray.Type != KSType.String) //Strings indexed returns strings which can be indexed
                                {
                                    AddValidationException($"Type '{ksArray.Type}' can not be indexed");
                                }
                            }
                        }

                        return ksArray;
                    }
                    else
                    {
                        throw new ValidationException($"Expected ']' or ','. Found '{_token.Value}'", _token.Line, _token.ColumnEnd);
                    }
                }
            }

            return ksArray;
        }

        private IKSValue ParseName()
        {
            Token initialToken = _token;

            Token prev = _token;
            bool isAction = false;
            bool isObj = false;
            string name = initialToken.Value;

            Token lastIndexerToken = null;

            //Checks for methods/objects/array indexes
            while (true)
            {
                _iterator.Next();

                if ((_token.Value == "." && (prev.Type == TokenTypes.Name || prev.Type == TokenTypes.GlobalObject)) ||
                    prev.Value == "." && (_token.Type == TokenTypes.Name || _token.Type == TokenTypes.GlobalObject))
                {
                    isObj = true;
                    lastIndexerToken = null;

                    name += _token.Value;
                }
                else if (_token.Value == "(")
                {
                    isAction = true;
                    lastIndexerToken = null;

                    break;
                }
                else if (_token.Value == "[" && isObj)
                {
                    lastIndexerToken = _token;

                    TryReadIndexer(out IKSValue value);

                    continue;
                }
                else
                {
                    //PATCH to handle all indexes in object properties. Should handle this as an operator
                    if (lastIndexerToken != null)
                    {
                        _iterator.ReturnTo(lastIndexerToken);
                    }

                    _iterator.Prev();

                    break;
                }

                prev = _token;
            }

            IKSValue variable = null;

            if (initialToken.Type == TokenTypes.Name)
            {
                if (isAction && isObj)
                {
                    //Maybe later add a "global" to variable object types
                    AddValidationException($"Action properties on objects currently not supported. Method: '{name}'", level: Level.Info);
                }
                else if (isObj)
                {
                    if (!TryGetDeclaration(initialToken.Value, out IKSValue value))
                    {
                        AddValidationException($"Variable '{initialToken.Value}' not defined in this scope");

                        //Attempt to fix
                        //_iterator.SkipUntil(new HashSet<string> { ";", ",", "}" });

                        return null;
                    }

                    variable = new KSVariable
                    {
                        Name = name,
                        Type = KSType.Any
                    };
                }
                else if (isAction)
                {
                    List<IKSValue> arguments = ParseArguments();
                    IKSValue foundAction;

                    if (!TryGetDeclaration(initialToken.Value, out foundAction))
                    {
                        AddValidationException($"Action '{_token.Value}' is not defined");

                        //Attempt to fix
                        //_iterator.SkipUntil(new HashSet<string> { ";", ",", "}" });

                        return null;
                    }

                    if (foundAction is not KSAction)
                    {
                        AddValidationException($"'{initialToken.Value}' is not an action");

                        return null;
                    }

                    ValidateArguments(arguments, foundAction as KSAction);

                    variable = foundAction;
                }
                else //Normal variable
                {
                    if (!TryGetDeclaration(initialToken.Value, out IKSValue value))
                    {
                        AddValidationException($"Variable '{_token.Value}' not defined in this scope");

                        //Attempt to fix
                        //_iterator.SkipUntil(new HashSet<string> { ";", ",", "}" });

                        return null;
                    }

                    variable = value as KSVariable;
                }
            }
            else //Globals
            {
                if (!_krunkerGlobalVariables.TryGetValue(name, out IKSValue v))
                {
                    AddValidationException($"Global '{name}' is not defined");

                    return null;
                }

                if (v is KSVariable globalVariable)
                {
                    variable = globalVariable;
                }
                else if (v is KSAction ksAction)
                {
                    //_iterator.Next();

                    variable = ksAction;

                    List<IKSValue> arguments = ParseArguments();
                    ValidateArguments(arguments, ksAction);
                }
            }

            return variable;
        }
    }
}
