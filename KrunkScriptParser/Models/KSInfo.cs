using KrunkScriptParser.Helpers;
using KrunkScriptParser.Models.Blocks;
using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models
{
    public class KSInfo
    {
        public List<ValidationException> ValidationExceptions { get; private set; } = new List<ValidationException>();

        private LinkedList<Dictionary<string, KSAction>> _actions = new LinkedList<Dictionary<string, KSAction>>();
        private LinkedList<Dictionary<string, KSVariable>> _variables = new LinkedList<Dictionary<string, KSVariable>>();
        private LinkedListNode<Dictionary<string, KSVariable>> _variableNode;
        private LinkedListNode<Dictionary<string, KSAction>> _actionNode;

        private Dictionary<string, KSAction> _krunkerGlobalVariables = new Dictionary<string, KSAction>();

        private TokenIterator _iterator;
        private Token _token => _iterator.Current;

        private static List<KSAction> _globalKrunkerObjects = new List<KSAction>();

        private void Initialize()
        {
            if(_globalKrunkerObjects.Count > 0)
            {
                return;
            }

            //Read file + parse file
            try
            {
                string text = File.ReadAllText("globalObjects.krnk");

                ParseGlobalObjects(text);
            }
            catch(Exception ex)
            {
                ValidationExceptions.Add(new ValidationException($"Failed to parse 'globalObjects.krnk' file. Additional errors may occur", 0, 0, level: Level.Warning));
            }
        }

        private void ParseGlobalObjects(string text)
        {
            TokenReader reader = new TokenReader(text);
            _iterator = new TokenIterator(reader.ReadToken());

            while(!reader.EndOfStream)
            {
                KSType returnType = ParseType();
                IKSValue value = ParseName();

                if(value is KSAction)
                {

                }
                else
                {

                }
            }
        }

        public void ParseTokens(Token token)
        {
            Initialize();

            if (token == null)
            {
                return;
            }

            _iterator = new TokenIterator(token);

            SkipComments();

            //Variables
            _variableNode = new LinkedListNode<Dictionary<string, KSVariable>>(new Dictionary<string, KSVariable>());
            _variables.AddFirst(_variableNode);

            //Actions
            _actionNode = new LinkedListNode<Dictionary<string, KSAction>>(new Dictionary<string, KSAction>());
            _actions.AddFirst(_actionNode);

            while (_token != null)
            {
                try
                {
                    //Only variables
                    if (_token.Type == TokenTypes.Type && _iterator.PeekNext()?.Type != TokenTypes.Action)
                    {
                        KSVariable variable = ParseVariableDeclaration();

                        AddVariable(variable);

                        continue;
                    }

                    if (_token.Type == TokenTypes.Type && _iterator.PeekNext()?.Type == TokenTypes.Action) //Function with return type
                    {

                    }

                    if (_token.Type == TokenTypes.Terminator || (_token.Type == TokenTypes.Punctuation && _token.Value == "}"))
                    {
                        _iterator.Next();
                    }
                }
                catch (ValidationException ex)
                {
                    ValidationExceptions.Add(ex);

                    if (!ex.CanContinue)
                    {
                        break;
                    }

                    _iterator.SkipLine();
                }
            }
        }

        private KSType ParseType(bool isArrayDeclaration = false)
        {
            if(_token.Type != TokenTypes.Type)
            {
                throw new ValidationException($"Expected a type. Received: '{_token.Value}'", _token.Line, _token.Column);
            }

            KSType type = new KSType
            {
                Name = _token.Value
            };

            _iterator.Next();

            //Checking for an array
            while (_token.Type == TokenTypes.Punctuation && _token.Value == "[")
            {
                type.IsArray = true;
                type.ArrayDepth++;

                _iterator.Next();

                if(isArrayDeclaration)
                {
                    return type;
                }

                if (_token.Type != TokenTypes.Punctuation || _token.Value != "]")
                {
                    throw new ValidationException($"Expected ']' found '{_token.Value}'", _token.Line, _token.Column);
                }

                _iterator.Next();
            }

            return type;
        }

        private KSVariable ParseVariableDeclaration()
        {
            KSVariable variable = new KSVariable
            {
                Type = ParseType()
            };

            //Expecting a name
            if (_token.Type == TokenTypes.Name)
            {
                variable.Name = _token.Value;

                _iterator.Next();
            }
            else
            {
                throw new ValidationException($"Expected variable name. Found: {_token.Value}", _token.Line, _token.Column);
            }

            //TODO: Nested array support
            if (variable.Type.ArrayDepth > 1)
            {
                ValidationExceptions.Add(new ValidationException($"Nested arrays currently not validated", _token.Line, _token.Column, level: Level.Info));

                _iterator.SkipUntil(TokenTypes.Terminator);

                _iterator.Next();

                return variable;
            }

            //Expecting =
            if (_token.Type != TokenTypes.Assign)
            {
                if (_token.Type == TokenTypes.Terminator)
                {
                    throw new ValidationException($"{variable.Name} must have a value", _token.Line, _token.Column, true);
                }

                throw new ValidationException($"Expected '='. Found: {_token.Value}", _token.Line, _token.Column, true);
            }

            _iterator.Next();

            //Expecting a value
            KSExpression expression = ParseExpression();

            variable.Value = expression;

            if (variable.Type.FullType != expression.CurrentType.FullType)
            {
                ValidationExceptions.Add(new ValidationException($"Expected type '{variable.Type.FullType}. Received '{expression.CurrentType.FullType}'", _token.Line, _token.Column, level: Level.Error));
            }

            //Line terminator
            if (_token.Type != TokenTypes.Terminator)
            {
                ValidationExceptions.Add(new ValidationException($"Expected ';'", _token.Prev.Line, _token.Prev.ColumnEnd));

                int line = _token.Line;

                //Go to new line/1 token after terminator
                while((_token.Type == TokenTypes.Punctuation || _token.Type == TokenTypes.Terminator) && _token.Line == line)
                {
                    _iterator.Next();
                }
            }
            else
            {
                _iterator.Next();
            }

            return variable;
        }

        private KSExpression ParseExpression(IKSValue prevValue = null, int depth = 0)
        {
            KSExpression expression = new KSExpression();

            List<(KSType, bool)> forcedTypes = new List<(KSType, bool)>();

            while(_token.Type == TokenTypes.KeyMethod)
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
                            ValidationExceptions.Add(new ValidationException($"Missing ')'", _token.Line, _token.Column));
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

                        SetForcedType(expression, forcedTypes);

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

            expression.Value = ParseGroup(null, forcedTypes.FirstOrDefault().Item1);
            expression.Type = expression.Value.Type;

            SetForcedType(expression, forcedTypes);


            return expression;

            void SetForcedType(KSExpression expression, List<(KSType, bool)> types)
            {
                KSType currentType = expression.CurrentType;

                foreach((KSType t, bool force) type in types.Reverse<(KSType, bool)>())
                {
                    //Was a cast, verify there's no type changes
                    if(!type.force && currentType != KSType.Any)
                    {
                        if (currentType != type.t)
                        {
                            ValidationExceptions.Add(new ValidationException($"Invalid cast to '{type.t.FullType}'", _token.Line, _token.Column));
                        }
                    }

                    currentType = type.t;
                }
            }
        }

        private IKSValue ParseGroup(KSExpression value = null, KSType forcedType = null)
        {
            IKSValue leftValue = value;

            KSGroup group = new KSGroup();
            group.Type = value?.CurrentType ?? forcedType;

            if(value != null)
            {
                group.Values.Add(value);
            }

            while (_token.Type != TokenTypes.Terminator && //Line ends
                _token.Value != ")" &&                     //Group ends
                _token.Value != "," &&                     //Object property ends
                _token.Value != "}")                       //Object property ends
            {
                string op = String.Empty;

                while (_token.Type == TokenTypes.Operator && (String.IsNullOrEmpty(op) || (_token.Value == _token.Prev.Value)))
                {
                    op += _token.Value;
                    _iterator.Next();
                }

                if (!String.IsNullOrEmpty(op) && (leftValue == null || !IsValidOperator(op)))
                {
                    ValidationExceptions.Add(new ValidationException($"Invalid operator '{op}'", _token.Line, _token.Column));
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

                    if(forcedType != null)
                    {
                        rightValue.Type = forcedType;
                        forcedType = null;
                    }
                }

                group.Values.Add(rightValue);

                if (leftValue != null)
                {
                    ValidateValues(op, leftValue, rightValue);
                }

                group.Type = rightValue.Type;

                leftValue = rightValue;

                if (_token.Type != TokenTypes.Terminator)
                {
                    _iterator.Next();
                }

            }

            return group;

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
                    case ">>>":
                    case "|":
                    case "&":
                    case "||":
                    case "&&":
                        return true;
                }

                return false;
            }

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
                    if(leftType != rightType)
                    {
                        ValidationExceptions.Add(new ValidationException($"Mismatched types. Expected '{leftType.FullType}'. Received '{rightType.FullType}'", _token.Line, _token.Column));
                    }
                }

                if (!String.IsNullOrEmpty(op))
                {
                    switch (op)
                    {
                        case "+":
                            if (rightType != KSType.String && rightType != KSType.Number)
                            {
                                ValidationExceptions.Add(new ValidationException($"Expected '{KSType.String.FullType}' or '{KSType.Number.FullType}'. Received '{rightType.FullType}'", _token.Line, _token.Column));
                            }

                            break;
                        case "-":
                        case "*":
                        case "/":
                        case "<<":
                        case ">>":
                        case "<<<":
                            if(rightType != KSType.Number)
                            {
                                ValidationExceptions.Add(new ValidationException($"Expected '{KSType.Number.FullType}'. Received '{rightType.FullType}'", _token.Line, _token.Column));
                            }
                            break;
                    }
                }
            }
        }

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
                    _iterator.Next();
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
                    Type = variable.Type,
                    //Variable = variable
                };

                if (variable.Type.IsArray)
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
                                ValidationExceptions.Add(new ValidationException($"Nested arrays currently not validated", _token.Line, _token.Column, level: Level.Info));

                                _iterator.SkipUntil(new HashSet<string> { ",", "}", ";" });
                            }
                            else if (indexer.Type == KSType.Object)
                            {
                                KSObject obj = ParseObjectProperties(ksVariable.Variable);
                            }
                        }

                        if (indexer.Type != KSType.Number)
                        {
                            ValidationExceptions.Add(new ValidationException($"Expected 'num' found '{indexer.Type}'", _token.Line, _token.Column));
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
                throw new ValidationException($"Expected value expression. Found: {_token.Value}", _token.Line, _token.ColumnEnd);
            }

            throw new Exception("Fix this");

            KSObject ParseObjectProperties(KSVariable variable)
            {
                KSObject ksObject = variable.Value as KSObject;

                return null;
            }

            KSObject ParseObject(int depth = 0)
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
                        ValidationExceptions.Add(new ValidationException($"Property '{name}' already exists", _token.Line, _token.Column));

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
                            ValidationExceptions.Add(new ValidationException($"Unexpected value '{_token.Value}", _token.Line, _token.ColumnEnd));
                        }
                    }
                    else
                    {
                        //We were missing a comma
                        if (_token.Type == TokenTypes.Name)
                        {
                            ValidationExceptions.Add(new ValidationException($"Missing ','", _token.Prev.Line, _token.Prev.ColumnEnd));
                        }
                        else if(_token.Type != TokenTypes.Terminator)
                        {
                            throw new ValidationException($"Unexpected value '{_token.Value}'", _token.Line, _token.Column);
                        }
                    }
                }

                return ksObject;
            }

            KSArray ParseArray(int depth = 0)
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

                            ValidationExceptions.Add(new ValidationException($"Nested arrays currently not validated", _token.Line, _token.Column, level: Level.Info));

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
                        ValidationExceptions.Add(new ValidationException($"Expected type '{ksArray.Type.Name}. Received '{value.Type.FullType}'", _token.Line, _token.Column, level: Level.Error));
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

            IKSValue ParseName()
            {
                Token initialToken = _token;

                Token prev = _token;
                bool isAction = false;
                bool isObj = false;

                while (true)
                {
                    _iterator.Next();

                    if ((_token.Value == "." && (prev.Type == TokenTypes.Name || prev.Type == TokenTypes.GlobalObject)) ||
                        prev.Value == "." && (_token.Type == TokenTypes.Name || _token.Type == TokenTypes.GlobalObject))
                    {
                        isObj = true;
                    }
                    else if (_token.Value == "(")
                    {
                        isAction = true;

                        break;
                    }
                    else
                    {
                        break;
                    }

                    prev = _token;
                }

                if (isAction)
                {
                    _iterator.Next();

                    List<IKSValue> values = ParseArguments();

                    if (initialToken.Type == TokenTypes.Name)
                    {
                        if (isObj)
                        {
                            ValidationExceptions.Add(new ValidationException($"Action properties on objects currently not supported", initialToken.Line, initialToken.Column));
                        }
                        else
                        {
                            if (!TryGetAction(initialToken.Value, values, out KSAction action))
                            {
                                ValidationExceptions.Add(new ValidationException($"Action '{_token.Value}' not defined", _token.Line, _token.Column));

                                //Attempt to fix
                                _iterator.SkipUntil(new HashSet<string> { ";", ",", "}" });

                                return null;
                            }
                        }
                    }
                    else
                    {

                    }
                }

                if (initialToken.Type == TokenTypes.Name)
                {
                }
                else //Global
                {
                }

                KSVariable variable = null;

                if (_token.Type == TokenTypes.Name)
                {
                    if (!TryGetVariable(initialToken.Value, out variable))
                    {
                        ValidationExceptions.Add(new ValidationException($"Variable '{_token.Value}' not defined", _token.Line, _token.Column));

                        //Attempt to fix
                        _iterator.SkipUntil(new HashSet<string> { ";", ",", "}" });

                        return null;
                    }
                }

                _iterator.Next();

                return null;
            }
        }


        private List<IKSValue> ParseArguments()
        {
            List<IKSValue> values = new List<IKSValue>();

            while(_token.Value != ")" || _token.Value == ",")
            {
                values.Add(ParseExpression());

                if (_token.Value == ")")
                {
                    break;
                }
                else
                {
                    //Another parameter
                    _iterator.Next();
                }
            }

            return values;
        }

        private List<KSParameter> ParseParameters(bool allowSpreadNotation = false)
        {
            List<KSParameter> parameters = new List<KSParameter>();

            while (_token.Type != TokenTypes.Type || _token.Value == ".")
            {
                KSType type = ParseType();

                if (_token.Type != TokenTypes.Name)
                {
                    //Getting too tired to save invalid code
                    throw new ValidationException($"Expected parameter name. Received '{_token.Value}'", _token.Line, _token.Column);
                }

                string multiProp = String.Empty;

                while (_token.Value == ".")
                {
                    multiProp += _token.Value;

                    _iterator.Next();
                }

                bool isMultiProp = false;

                if (!String.IsNullOrEmpty(multiProp))
                {
                    if (multiProp == "...")
                    {
                        if (allowSpreadNotation)
                        {
                            isMultiProp = true;
                        }
                        else
                        {
                            ValidationExceptions.Add(new ValidationException($"Spread notation '...' not supported", _token.Line, _token.Column));
                        }
                    }
                    else
                    {
                        ValidationExceptions.Add(new ValidationException($"Unexpected value '{multiProp}'", _token.Line, _token.Column));
                    }
                }

                parameters.Add(new KSParameter
                {
                    Name = _token.Value,
                    Type = type,
                    MultiProp = isMultiProp
                });

                _iterator.Next();

                //Got more
                if (_token.Value == ",")
                {
                    _iterator.Next();
                }
            }

            return parameters;
        }

        private void SkipComments()
        {
            while (_iterator.Current?.Type == TokenTypes.Comment)
            {
                _iterator.Next();
            }
        }

        private void AddVariable(KSVariable variable)
        {
            bool alreadyDeclared = TryGetVariable(variable.Name, out KSVariable _);

            if(!_variableNode.Value.TryAdd(variable.Name, variable))
            {
                throw new ValidationException($"Variable '{variable.Name}' has already been declared", _iterator.Current.Line, _iterator.Current.Column, true);
            }

            //Variable name is declared higher up, but that's valid
            if(alreadyDeclared)
            {
                ValidationExceptions.Add(new ValidationException($"Variable '{variable.Name}' hiding previously declared variable. Consider renaming", _iterator.Current.Line, _iterator.Current.Column, level: Level.Warning));
            }
        }

        private bool TryGetVariable(string name, out KSVariable variable)
        {
            LinkedListNode<Dictionary<string, KSVariable>> currentNode = _variableNode;
            variable = null;

            do
            {
                if(currentNode.Value.TryGetValue(name, out KSVariable value))
                {
                    variable =  value;

                    return true;
                }

                currentNode = currentNode.Previous;
            } while (currentNode != null);

            return false;
        }

        private bool TryGetAction(string name, List<IKSValue> arguments, out KSAction action)
        {
            LinkedListNode<Dictionary<string, KSAction>> currentNode = _actionNode;
            action = null;

            do
            {
                if (currentNode.Value.TryGetValue(name, out KSAction value))
                {
                    action = value;

                    return true;
                }

                currentNode = currentNode.Previous;
            } while (currentNode != null);

            return false;
        }


        private class TokenIterator
        {
            public Token Current => _token;
            private Token _token;

            public TokenIterator(Token token)
            {
                _token = token;
            }

            public Token PeekNext()
            {
                Token t = _token?.Next;

                while (t.Type == TokenTypes.Comment)
                {
                    t = t.Next;
                }

                return t;
            }

            public Token PeekPrev()
            {
                Token t = _token?.Prev;

                while(t.Type == TokenTypes.Comment)
                {
                    t = t.Prev;
                }

                return t;
            }

            public Token Next(bool checkEOF = true)
            {
                if(checkEOF)
                {
                    if(_token.Next == null)
                    {
                        throw new ValidationException("Unexpected end of file", _token.Line, _token.Column);
                    }
                }

                _token = _token?.Next;

                if(_token.Type == TokenTypes.Comment)
                {
                    return Next();
                }

                return _token;
            }

            public Token Prev()
            {
                _token = _token?.Prev;

                return _token;
            }

            public Token SkipLine()
            {
                while(_token.Line == _token.Next?.Line)
                {
                    Next();
                }

                return Next();
            }

            public void SkipUntil(TokenTypes type)
            {
                while(!type.HasFlag(_token.Type))
                {
                    Next();
                }
            }

            public void SkipUntil(HashSet<string> validValues)
            {
                while(!validValues.Contains(_token.Value))
                {
                    Next();
                }
            }
        }
    }
}
