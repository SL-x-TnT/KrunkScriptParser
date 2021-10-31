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
                    Type = variable?.Type ?? KSType.Any,
                    Variable = variable as KSVariable
                };

                if (variable?.Type.IsArray == true)
                {
                    //Check to see if indexed
                    Token next = _iterator.PeekNext();

                    if (next.Type == TokenTypes.Punctuation && next.Value == "[")
                    {
                        _iterator.Next();
                        _iterator.Next();

                        IKSValue indexer = ParseValue(depth + 1);

                        if (indexer is KSVariableName ksVariable)
                        {
                            if (ksVariable.Variable.Type.IsArray && ksVariable.Variable.Type.ArrayDepth > 1)
                            {
                                //TODO: Nested arrays
                                AddValidationException(new ValidationException($"Nested arrays currently not validated", _token.Line, _token.Column, level: Level.Info));

                                _iterator.SkipUntil(new HashSet<string> { ",", "}", ";" });
                            }
                            else if (indexer.Type == KSType.Object)
                            {
                                KSObject obj = ParseObjectProperties(ksVariable.Variable);
                            }
                        }

                        if (indexer.Type != KSType.Number)
                        {
                            AddValidationException(new ValidationException($"Expected 'num' found '{indexer.Type}'", _token.Line, _token.Column));
                        }

                        _iterator.Next();
                    }
                }
                else if (variable.Type == KSType.Object)
                {
                    //Check to see if property
                }

                return variableName;
            }
            else if (_token.Type == TokenTypes.Type) //Arrays
            {
                return ParseArray();
            }
            else
            {
                throw new ValidationException($"Expected value expression. Found: {_token.Value}", _token.Line, _token.Column);
            }

            throw new Exception("Fix this");

            
        }

        private KSObject ParseObjectProperties(KSVariable variable)
        {
            KSObject ksObject = variable.Value as KSObject;

            return null;
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
                    throw new ValidationException($"Expected ':' found '='", _token.Line, _token.Column);
                }

                _iterator.Next();

                KSExpression expression = ParseExpression(depth: depth + 1);

                expression.ForcedType = KSType.Any;

                if (!ksObject.Properties.TryAdd(name, expression))
                {
                    AddValidationException(new ValidationException($"Property '{name}' already exists", _token.Line, _token.Column));

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
                        AddValidationException(new ValidationException($"Unexpected value '{_token.Value}", _token.Line, _token.ColumnEnd));
                    }
                }
                else
                {
                    //We were missing a comma
                    if (_token.Type == TokenTypes.Name)
                    {
                        AddValidationException(new ValidationException($"Missing ','", _token.Prev.Line, _token.Prev.ColumnEnd));
                    }
                    else if (_token.Type != TokenTypes.Terminator)
                    {
                        throw new ValidationException($"Unexpected value '{_token.Value}'", _token.Line, _token.Column);
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

                        AddValidationException(new ValidationException($"Nested arrays currently not validated", _token.Line, _token.Column, level: Level.Info));

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
                IKSValue value = ParseValue(depth + 1);

                if (ksArray.Type != value.Type)
                {
                    AddValidationException(new ValidationException($"Expected type '{ksArray.Type.Name}'. Received '{value.Type.FullType}'", _token.Line, _token.Column, level: Level.Error));
                }

                ksArray.Values.Add(value);

                _iterator.Next();

                if (_token.Type == TokenTypes.Punctuation)
                {
                    if (_token.Value == ",")
                    {
                        _iterator.Next();
                    }
                    else if (_token.Value == "]")
                    {
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

            //Checks for methods/objects
            while (true)
            {
                _iterator.Next();

                if ((_token.Value == "." && (prev.Type == TokenTypes.Name || prev.Type == TokenTypes.GlobalObject)) ||
                    prev.Value == "." && (_token.Type == TokenTypes.Name || _token.Type == TokenTypes.GlobalObject))
                {
                    isObj = true;

                    name += _token.Value;
                }
                else if (_token.Value == "(")
                {
                    isAction = true;

                    break;
                }
                else
                {
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
                    AddValidationException(new ValidationException($"Action properties on objects currently not supported", initialToken.Line, initialToken.Column));
                }
                else if (isObj)
                {
                    if (!TryGetDeclaration(initialToken.Value, out IKSValue value))
                    {
                        AddValidationException(new ValidationException($"Variable '{_token.Value}' not defined", _token.Line, _token.Column));

                        //Attempt to fix
                        _iterator.SkipUntil(new HashSet<string> { ";", ",", "}" });

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
                        AddValidationException(new ValidationException($"Action '{_token.Value}' is not defined", _token.Line, _token.Column));

                        //Attempt to fix
                        _iterator.SkipUntil(new HashSet<string> { ";", ",", "}" });

                        return null;
                    }

                    if (!(foundAction is KSAction))
                    {
                        AddValidationException(new ValidationException($"'{_token.Value}' is not an action", _token.Line, _token.Column));

                        return null;
                    }

                    ValidateArguments(arguments, foundAction as KSAction);
                }
                else //Normal variable
                {
                    if (!TryGetDeclaration(initialToken.Value, out IKSValue value))
                    {
                        AddValidationException(new ValidationException($"Variable '{_token.Value}' not defined", _token.Line, _token.Column));

                        //Attempt to fix
                        _iterator.SkipUntil(new HashSet<string> { ";", ",", "}" });

                        return null;
                    }

                    variable = value as KSVariable;
                }
            }
            else //Globals
            {
                if (!_krunkerGlobalVariables.TryGetValue(name, out IKSValue v))
                {
                    AddValidationException(new ValidationException($"Global '{name}' is not defined", _token.Line, _token.Column));

                    return null;
                }

                if (v is KSVariable globalVariable)
                {
                    variable = globalVariable;
                }
                else if (v is KSAction ksAction)
                {
                    _iterator.Next();

                    variable = ksAction;

                    List<IKSValue> arguments = ParseArguments();
                    ValidateArguments(arguments, ksAction);


                }
            }

            return variable;
        }
    }
}
