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
                //Throwing as I don't know if I handle a null IKSValue
                AddValidationException($"Unexpected ';'", _token, willThrow: true);
            }

            if (_token.Type == TokenTypes.Punctuation)
            {
                //Object
                if (_token.Value == "{")
                {
                    IKSValue obj = ParseObject(depth);

                    _iterator.Next();

                    return obj;
                }
            }

            if (_token.Type == TokenTypes.Type || (_token.Type == TokenTypes.String && _iterator.PeekNext().Value == "[")) //Arrays
            {
                return ParseArray(_token.Type == TokenTypes.String);
            }
            else if (_token.Type == TokenTypes.String || _token.Type == TokenTypes.Number || _token.Type == TokenTypes.Bool)
            {
                KSPrimitiveValue value = new KSPrimitiveValue
                {
                    Value = _token.Value,
                    Type = _token.Type == TokenTypes.String ? KSType.String :
                                _token.Type == TokenTypes.Bool ? KSType.Bool : KSType.Number
                };

                if (_token.Type == TokenTypes.String)
                {
                    if (!value.Value.EndsWith("\"") && !value.Value.EndsWith("'"))
                    {
                        AddValidationException($"Unterminated string literal", _token);
                    }
                }
                else if(_token.Type == TokenTypes.Number)
                {
                    //Leading 0s breaks scripts without throwing a validation exception
                    string sValue = value.Value.Split('.').First();

                    if(sValue.Length > 1 && sValue.StartsWith("0") && !value.Value.Contains("x", StringComparison.InvariantCultureIgnoreCase))
                    {
                        AddValidationException($"Leading 0s in numbers causes scripts to break", _token);
                    }
                }

                _iterator.Next();

                return value;
            }
            else if (_token.Type == TokenTypes.Name || _token.Type == TokenTypes.GlobalObject) //Setting using another variable or GAME/UTILS
            {
                IKSValue variable = ParseName();

                KSVariableName variableName = new KSVariableName
                {
                    Type = new KSType(variable?.Type),
                    Variable = variable as KSVariable,
                    Action = variable as KSAction,
                    Object = variable as KSObject,
                    TokenLocation = variable?.TokenLocation
                };

                //Happens when using an action in a method
                if(variableName.Variable?.Value is KSAction)
                {
                    variableName.Type = KSType.Action;
                }

                Token nextToken = _iterator.PeekNext();

                if (nextToken.Value == "[")
                {
                    _iterator.Next();
                }
                
                bool isDeclared = false;
                bool lastWasObjectProperty = false; //...

                while(true)
                {
                    bool willContinue = false;

                    if(TryReadIndexer(out IKSValue _, ref isDeclared))
                    {
                        lastWasObjectProperty = false;

                        //Have to decrease on actions
                        if (variableName.Action != null)
                        {
                            variableName.Type.DecreaseDepth();
                        }

                        if (variableName.Type != KSType.Any && variableName.Type.ArrayDepth < 0 && variableName.Type != KSType.String)
                        {
                            AddValidationException($"Type '{variableName.Type}' can not be indexed", variableName.TokenLocation, _token);
                        }

                        //Has another
                        if (_iterator.PeekNext().Value == "[")
                        {
                            _iterator.Next();
                        }

                        willContinue = true;
                    }

                    nextToken = _iterator.PeekNext();

                    if(nextToken.Value == ".")
                    {
                        _iterator.Next();

                        //It's an action that isn't global
                        if (variableName.Action?.Global == false)
                        {
                            AddValidationException($"Invalid member property access. Assign method result to a new variable first", nextToken);

                            _iterator.SkipUntil(TokenTypes.Terminator);
                            _iterator.Prev(); //Will call .Next before method is over

                            break;
                        }
                        else if (variableName.Action?.Global == true) //These are fine
                        {
                            //Type is now any
                            variableName.Type = KSType.Any;

                            willContinue = true;
                        }

                        _iterator.Next();

                        if (_token.Type != TokenTypes.Name)
                        {
                            AddValidationException($"Missing property member", _token);
                            _iterator.SkipUntil(TokenTypes.Terminator);
                            _iterator.Prev(); //Will call .Next before method is over
                        }
                        else if(_iterator.PeekNext().Value == "[")
                        {
                            _iterator.Next();
                            //lastWasObjectProperty = true;
                        }
                    }

                    if (!willContinue)
                    {
                        break;
                    }
                }


                if (!lastWasObjectProperty)
                {
                    _iterator.Next();
                }

                return variableName;
            }
            else
            {
                AddValidationException($"Expected value. Found: '{_token.Value}'", _token, willThrow: true);


                return null;
            }
        }

        private bool TryReadIndexer(out IKSValue value, ref bool isDeclared, bool ignoreType = false)
        {
            value = null;

            if (_token.Value != "[")
            {
                return false;
            }

            _iterator.Next();

            if(_token.Value == "]")
            {
                _iterator.Next();

                return true;
            }

            bool wasDeclared = isDeclared;

            isDeclared = true;

            value = ParseExpression(depth: 1);

            if (_token.Value != "]" && !wasDeclared &&_token.Value != ",")
            {
                AddValidationException($"Missing end of indexer ']'", _token);
            }

            KSExpression expression = value as KSExpression;

            if(!ignoreType && value.Type != KSType.Number && (wasDeclared || (expression.Items.Count == 1)))
            {
                AddValidationException($"Array indexer expects type '{KSType.Number}'. Received '{value.Type}'", expression.TokenLocation, expression.EndTokenLocation);
            }

            if(_iterator.PeekNext().Value == "[")
            {
                _iterator.Next();
            }

            return true;
        }

        private KSObject ParseObject(int depth = 0)
        {
            _iterator.Next();

            KSObject ksObject = new KSObject
            {
            };

            while (_token.Value != "}" && _token.Type != TokenTypes.Terminator)
            {
                DocumentationInfo documentation = ParseDocumentationInfo();

                if(_token.Type == TokenTypes.Type)
                {
                    KSType type = ParseType();

                    AddValidationException($"Objects do not support declaring types for property members", type.TokenLocation, type.EndTokenLocation);
                }

                //Property name
                if (_token.Type != TokenTypes.Name)
                {
                    AddValidationException($"Expected property name. Found: '{_token.Value}'", _token, willThrow: true);
                }

                string name = _token.Value;
                TokenLocation nameLocation = new TokenLocation(_token);

                _iterator.Next();

                //Expecting :
                if (_token.Value != ":")
                {
                    AddValidationException($"Expected ':' found '{_token.Value}'", _token, willThrow: true);
                }

                _iterator.Next();

                //KSExpression expression = ParseExpression(depth: depth + 1);
                KSExpression expression = ParseExpression(depth: depth + 1);
                expression.Documentation = documentation;
                expression.Type = KSType.Any;

                if (!ksObject.Properties.TryAdd(name, expression))
                {
                    AddValidationException($"Property '{name}' already exists", nameLocation);

                    _iterator.SkipUntil(TokenTypes.Name | TokenTypes.Punctuation); //Skip until the next name, comma, or }
                }

                if (_token.Type == TokenTypes.Punctuation)
                {
                    if (_token.Value == "," || _token.Value == "}")
                    {
                        Token next = _iterator.PeekNext();

                        if (next.Value == "}" || next.Type == TokenTypes.Name || next.Type == TokenTypes.Type)
                        {
                            _iterator.Next();
                        }
                    }
                    else if (_token.Value != "}") //A missing comma at the end is no issue
                    {
                        AddValidationException($"Unexpected value '{_token.Value}", _token);
                    }
                }
                else
                {
                    //We were missing a comma
                    if (_token.Type == TokenTypes.Name)
                    {
                        AddValidationException($"Missing ','", _token.Prev);
                    }
                    else if (_token.Type != TokenTypes.Terminator)
                    {
                        AddValidationException($"Unexpected value '{_token.Value}'", _token, willThrow: true);
                    }

                    //Go back 1 as we'll go to the next token after this method
                    _iterator.Prev();
                    break;
                }
            }

            return ksObject;
        }

        //Parses the creation of an array
        private KSArray ParseArray(bool isString)
        {
            KSArray ksArray = new KSArray();

            if(isString)
            {
                ksArray.Type = KSType.String;
                _iterator.Next();
            }
            else
            {
                ksArray.Type = ParseType();
            }

            KSType valueType = new KSType(ksArray.Type);
            valueType.DecreaseDepth();

            //Empty array
            if (_iterator.PeekPrev().Value == "]")
            {
                return ksArray;
            }

            //String aren't arrays, but we're using this method for the indexing below
            if (!isString)
            {
                ksArray.Values.AddRange(ParseArrayInitialization(valueType));
            }

            bool isDeclared = true;

            //For some reason an array was declared then indexed
            while(TryReadIndexer(out IKSValue arrayVal, ref isDeclared))
            {
                if(_token.Value == ",")
                {
                    AddValidationException($"Array index can not contain an array declaration", arrayVal.TokenLocation);

                    _iterator.SkipUntil(new HashSet<string> { "]" });
                }

                ksArray.Type.DecreaseDepth();

                if (ksArray.Type.ArrayDepth < 0 && ksArray.Type != KSType.String) //Strings indexed returns strings which can be indexed
                {
                    AddValidationException($"Type '{ksArray.Type}' can not be indexed", arrayVal.TokenLocation);
                }
            }

            return ksArray;
        }

        private List<KSExpression> ParseArrayInitialization(KSType valueType)
        {
            List<KSExpression> values = new List<KSExpression>();

            //Read expression for all values
            while (true)
            {
                //Depth of 1 so objects don't need ;
                KSExpression arrayValue = ParseExpression(depth: 1);

                if (arrayValue.Type != valueType)
                {
                    AddValidationException($"Expected type '{arrayValue.Type}'. Received '{valueType}'", arrayValue.TokenLocation, level: Level.Error);
                }

                values.Add(arrayValue);

                //More values
                if (_token.Value == ",")
                {
                    _iterator.Next();
                }
                else if (_token.Value == "]")
                {
                    _iterator.Next();

                    break;
                }
                else //...
                {
                    AddValidationException($"Unexpected value '{_token.Value}'", _token);
                    break;
                }
            }

            return values;
        }

        private IKSValue ParseName()
        {
            Token initialToken = _token;

            Token prev = _token;
            bool isAction = false;
            bool isObj = false;
            string name = initialToken.Value;
            int depthDecrease = 0;
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
                    if (!isObj && initialToken.Type != TokenTypes.GlobalObject && TryGetDeclaration(initialToken.Value, out IKSValue ksValue) && ksValue is not KSAction)
                    {
                        //Not an action, so back 1 step
                        _iterator.Prev();
                    }
                    else
                    {
                        isAction = true;
                        lastIndexerToken = null;
                    }
                    break;
                }
                else if (_token.Value == "[")
                {
                    lastIndexerToken = _token;

                    bool isDeclared = false;

                    while (TryReadIndexer(out IKSValue value, ref isDeclared))
                    {
                        if (!isObj)
                        {
                            depthDecrease++;
                        }

                        name += "[]";
                    }

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
                    //AddValidationException($"Action properties on objects currently not supported. Method: '{name}'", initialToken, _token, level: Level.Info);

                    //Parse the arguments to get them out of the way. Don't currently have support to know the parameters
                    List<KSExpression> arguments = ParseArguments();

                    //_iterator.Next();

                    IKSValue value = VerifyIsObject(initialToken.Value);

                    if (value == null)
                    {
                        return null;
                    }

                    //For now, this is for static variable assignment
                    if(value is KSVariable ksVariable)
                    {
                        if (ksVariable.TryReadObject(out KSObject ksObject))
                        {

                            string[] nameParts = name.Split('.').Skip(1).ToArray();

                            foreach (string namePart in nameParts)
                            {
                                if (!ksObject.Properties.TryGetValue(namePart, out IKSValue v))
                                {
                                    break;
                                }

                                ksObject = v as KSObject;
                                variable = v;
                            }
                        }

                        if(variable is KSAction ksAction)
                        {
                            return ksAction;
                        }
                    }

                }
                else if (isObj)
                {
                    IKSValue value = VerifyIsObject(initialToken.Value);

                    if(value == null)
                    {
                        return null;
                    }

                    variable = new KSVariable
                    {
                        Name = name,
                        Type = KSType.Any,
                        Value = value
                    };
                }
                else if (isAction)
                {
                    List<KSExpression> arguments = ParseArguments();
                    IKSValue foundAction;

                    //_iterator.Next();

                    if (!TryGetDeclaration(initialToken.Value, out foundAction))
                    {
                        AddValidationException($"Action '{initialToken.Value}' is not defined", initialToken, _token);

                        //Attempt to fix
                        //_iterator.SkipUntil(new HashSet<string> { ";", ",", "}" });

                        return null;
                    }

                    if (foundAction is not KSAction)
                    {
                        AddValidationException($"'{initialToken.Value}' is not an action", initialToken, _token);

                        return null;
                    }

                    ValidateArguments(arguments, foundAction as KSAction);

                    variable = foundAction;
                }
                else //Normal variable
                {
                    if (!TryGetDeclaration(initialToken.Value, out IKSValue value))
                    {
                        AddValidationException($"Variable '{_token.Value}' not defined in this scope", initialToken, _token);

                        //Attempt to fix
                        //_iterator.SkipUntil(new HashSet<string> { ";", ",", "}" });

                        return null;
                    }

                    KSType newType = new KSType(value.Type);

                    //PATCH: Indexes again
                    for (int i = 0; i < depthDecrease; i++)
                    {
                        newType.DecreaseDepth();
                    }

                    variable = new KSVariable
                    {
                        Name = name,
                        Type = newType,
                        Value = value
                    };
                }
            }
            else //Globals
            {
                if (!_krunkerGlobalVariables.TryGetValue(name, out IKSValue v))
                {
                    AddValidationException($"Global '{name}' is not defined", initialToken, _token);

                    //Attempting to call an undefined action
                    if(_token.Value == "(")
                    {
                        List<KSExpression> arguments = ParseArguments();
                    }

                    return null;
                }

                if (v is KSVariable globalVariable)
                {
                    variable = globalVariable;
                }
                else if (v is KSObject ksObject)
                {
                    return ksObject;
                }
                else if (v is KSAction ksAction)
                {
                    //_iterator.Next();

                    variable = ksAction;

                    List<KSExpression> arguments = ParseArguments();
                    ValidateArguments(arguments, ksAction);
                }
            }

            return variable;

            IKSValue VerifyIsObject(string name)
            {
                if (!TryGetDeclaration(name, out IKSValue value))
                {
                    AddValidationException($"Variable '{name}' not defined in this scope", initialToken);

                    //Attempt to fix
                    //_iterator.SkipUntil(new HashSet<string> { ";", ",", "}" });

                    return null;
                }

                //PATCH: Checking to verify the type is an obj
                if (value is KSVariable variableName)
                {
                    KSType tempType = new KSType(variableName.Type);

                    for (int i = 0; i < depthDecrease; i++)
                    {
                        tempType.DecreaseDepth();
                    }

                    if (tempType != KSType.Object)
                    {
                        AddValidationException($"Property member access '.' requires type '{KSType.Object}'. Received '{tempType}' for variable '{variableName.Name}'", initialToken, _token);
                    }

                    if (tempType.ArrayDepth < 0)
                    {
                        AddValidationException($"Type '{tempType}' can not be indexed", initialToken, _token);
                    }

                    return variableName;
                }

                return null;
            }
        }
    }
}
