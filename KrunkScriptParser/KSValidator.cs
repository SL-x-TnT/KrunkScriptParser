using KrunkScriptParser.Helpers;
using KrunkScriptParser.Models;
using KrunkScriptParser.Models.Blocks;
using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser
{
    public class KSValidator
    {
        public List<ValidationException> ValidationExceptions { get; private set; } = new List<ValidationException>();

        private LinkedList<Dictionary<string, IKSValue>> _declarations = new LinkedList<Dictionary<string, IKSValue>>();
        private LinkedListNode<Dictionary<string, IKSValue>> _declarationNode;

        private Dictionary<string, IKSValue> _krunkerGlobalVariables = new Dictionary<string, IKSValue>();

        private TokenIterator _iterator;
        private Token _token => _iterator.Current;
        private TokenReader _reader;

        public event EventHandler<ValidationException> OnValidationError;

        public KSValidator(string text)
        {
            _reader = new TokenReader(text);
        }

        public bool Validate()
        {
            InitializeGlobals();

            try
            {
                ParseTokens();

                return true;
            }
            catch(Exception ex)
            {
                AddValidationException(new ValidationException($"Parser failed due to an exception. Message: {ex.Message}", _token.Line, _token.Column));

                return false;
            }
        }

        private void InitializeTokens()
        {
            _iterator = new TokenIterator(_reader.ReadAllTokens());
        }

        private void ParseTokens()
        {
            InitializeTokens();

            SkipComments();

            //Variables
            _declarationNode = new LinkedListNode<Dictionary<string, IKSValue>>(new Dictionary<string, IKSValue>());
            _declarations.AddFirst(_declarationNode);

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
                    AddValidationException(ex);

                    if (!ex.CanContinue)
                    {
                        break;
                    }

                    _iterator.SkipLine();
                }
            }
        }

        private void InitializeGlobals()
        {
            if(_krunkerGlobalVariables.Count > 0)
            {
                return;
            }

            //Read file + parse file
            try
            {
                string text = File.ReadAllText("globalObjects.krnk");

                ParseGlobalObjects(text);
            }
            catch(ValidationException ex)
            {
                AddValidationException(ex);
                AddValidationException(new ValidationException($"Failed to parse 'globalObjects.krnk' file. Additional errors may occur", _token.Line, _token.Column, level: Level.Warning));
            }
        }

        private void ParseGlobalObjects(string text)
        {
            TokenReader reader = new TokenReader(text);
            _iterator = new TokenIterator(reader.ReadAllTokens());
            
            while(_iterator.PeekNext() != null)
            {
                KSType returnType = KSType.Void;

                if (_token.Type == TokenTypes.Type)
                {
                    returnType = ParseType();
                }

                string name = String.Empty;
                bool globalFinished = false;

                while ((_token.Type != TokenTypes.Type && _token.Value != "("))
                {
                    if(!String.IsNullOrEmpty(name) && _token.Type == TokenTypes.GlobalObject && globalFinished)
                    {
                        break;
                    }

                    name += _token.Value;

                    _iterator.Next();

                    if (_token.Type == TokenTypes.Name)
                    {
                        globalFinished = true;
                    }
                }

                //Action
                if(_token.Value == "(")
                {
                    _iterator.Next();

                    List<KSParameter> parameters = ParseParameters(true);

                    _krunkerGlobalVariables.TryAdd(name, new KSAction
                    {
                        Type = returnType,
                        Parameters = parameters,
                        Text = name
                    });

                    _iterator.Next(false);
                }
                else //Property
                {
                    _krunkerGlobalVariables.TryAdd(name, new KSVariable
                    {
                        Name = name,
                        Type = returnType
                    });
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
                AddValidationException(new ValidationException($"Nested arrays currently not validated", _token.Line, _token.Column, level: Level.Info));

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
                AddValidationException(new ValidationException($"Variable expected type '{variable.Type.FullType}'. Received '{expression.CurrentType.FullType}'", _token.Line, _token.Column, level: Level.Error));
            }

            //Line terminator
            if (_token.Type != TokenTypes.Terminator)
            {
                AddValidationException(new ValidationException($"Expected ';'", _token.Prev.Line, _token.Prev.ColumnEnd));

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
                            AddValidationException(new ValidationException($"Missing ')'", _token.Line, _token.Column));
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

            if (forcedTypes != null)
            {
                ValidateForcedType(expression.CurrentType, forcedTypes);
                expression.ForcedType = forcedTypes.FirstOrDefault().Item1;
            }

            if(expression.Type == null)
            { 
            }

            return expression;

        }

        private void ValidateForcedType(KSType currentType, List<(KSType, bool)> types)
        {
            foreach ((KSType t, bool force) type in types.Reverse<(KSType, bool)>())
            {
                //Was a cast, verify there's no type changes
                if (!type.force && currentType != KSType.Any)
                {
                    if (currentType != type.t)
                    {
                        AddValidationException(new ValidationException($"Invalid cast to '{type.t.FullType}'", _token.Line, _token.Column));
                    }
                }

                currentType = type.t;
            }
        }

        private IKSValue ParseGroup(KSExpression value = null, List<(KSType, bool)> initialTypes = null)
        {
            IKSValue leftValue = value;

            KSGroup group = new KSGroup();
            group.Type = value?.CurrentType ?? initialTypes?.LastOrDefault().Item1;

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
                    AddValidationException(new ValidationException($"Invalid operator '{op}'", _token.Line, _token.Column));
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

                    if(initialTypes != null && initialTypes.Count > 0)
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

                leftValue = rightValue;

                if (_token.Type != TokenTypes.Terminator && _token.Type != TokenTypes.Operator)
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
                        AddValidationException(new ValidationException($"Mismatched types. Expected '{leftType.FullType}'. Received {leftType.FullType} {op} {rightType.FullType}", 
                            _token.Line, _token.Column));
                    }
                }

                if (!String.IsNullOrEmpty(op))
                {
                    switch (op)
                    {
                        case "+":
                            if (rightType != KSType.String && rightType != KSType.Number)
                            {
                                AddValidationException(new ValidationException($"Expected '{KSType.String.FullType}' or '{KSType.Number.FullType}' with operator '{op}'. Received {leftType.FullType} {op} {rightType.FullType}", _token.Line, _token.Column));
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
                                AddValidationException(new ValidationException($"Expected '{KSType.Number.FullType}' with operator '{op}'. Received {leftType.FullType} {op} {rightType.FullType}", _token.Line, _token.Column));
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

            IKSValue ParseName()
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

                if(initialToken.Type == TokenTypes.Name)
                { 
                    if(isAction && isObj)
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
                            Name = initialToken.Value,
                            Type = KSType.Any
                        };
                    }
                    else if(isAction)
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

                    if(v is KSVariable globalVariable)
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

        private List<KSParameter> ParseParameters(bool isGlobalMethod = false)
        {
            List<KSParameter> parameters = new List<KSParameter>();

            while ((_token.Type == TokenTypes.Type || _token.Type == TokenTypes.Action) || 
                _token.Value == "." ||
                (isGlobalMethod && _token.Value == "any")
                )
            {
                KSType type = null;

                //Global methods can have actions
                if (_token.Type == TokenTypes.Action && isGlobalMethod)
                {
                    type = KSType.Action;

                    _iterator.Next();
                }
                else if (_token.Value == "any" && isGlobalMethod)
                {
                    type = KSType.Any;

                    _iterator.Next();
                }
                else
                {
                    type = ParseType();
                }

                bool optional = false;

                if (isGlobalMethod && _token.Value == "?")
                {
                    optional = true;
                    _iterator.Next();
                }

                string multiProp = String.Empty;

                while (_token.Type == TokenTypes.Punctuation)
                {
                    multiProp += _token.Value;

                    _iterator.Next();
                }

                bool isMultiProp = false;

                if (!String.IsNullOrEmpty(multiProp))
                {
                    if (multiProp == "...")
                    {
                        if (isGlobalMethod)
                        {
                            isMultiProp = true;
                        }
                        else
                        {
                            AddValidationException(new ValidationException($"Spread notation '...' not supported", _token.Line, _token.Column));
                        }
                    }
                    else
                    {
                        AddValidationException(new ValidationException($"Unexpected value '{multiProp}'", _token.Line, _token.Column));
                    }
                }

                if (_token.Type != TokenTypes.Name)
                {
                    //Getting too tired to save invalid code
                    throw new ValidationException($"Expected parameter name. Received '{_token.Value}'", _token.Line, _token.Column);
                }

                parameters.Add(new KSParameter
                {
                    Name = _token.Value,
                    Type = type,
                    MultiProp = isMultiProp,
                    Optional = optional
                });

                _iterator.Next();

                //Got more
                if (_token.Value == ",")
                {
                    _iterator.Next();
                }
            }

            if(_token.Value != ")")
            {
                AddValidationException(new ValidationException($"Expected ')'. Received '{_token.Value}'", _token.Line, _token.Column));

                _iterator.SkipUntil(new HashSet<string> { ")", ";" });
            }

            return parameters;
        }

        private void ValidateArguments(List<IKSValue> arguments, KSAction action)
        {
            int parameterIndex = 0;
            int expected = action.Parameters.Count(x => !x.Optional);

            foreach (KSExpression argument in arguments)
            {
                if(parameterIndex >= action.Parameters.Count)
                {
                    AddValidationException(new ValidationException($"Invalid argument count. Received {arguments.Count} and expected {action.Parameters.Count}", _token.Line, _token.Column));

                    return;
                }

                KSParameter parameter = action.Parameters[parameterIndex];

                if(parameter.Type.FullType != argument.CurrentType.FullType && parameter.Type != KSType.Any)
                {
                    Level level = Level.Error;

                    if(action.Global)
                    {
                        level = Level.Warning;
                    }

                    AddValidationException(new ValidationException($"Expected type '{parameter.Type.FullType}' for parameter '{parameter.Type.FullType}'. Received '{argument.CurrentType.FullType}'", _token.Line, _token.Column, level: level));
                }

                if(!parameter.MultiProp)
                {
                    parameterIndex++;
                }
            }

            if (parameterIndex < expected)
            {
                AddValidationException(new ValidationException($"Invalid argument count. Received {arguments.Count} and expected {expected}", _token.Line, _token.Column));
            }
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
            bool alreadyDeclared = TryGetDeclaration(variable.Name, out IKSValue _);

            if(!_declarationNode.Value.TryAdd(variable.Name, variable))
            {
                throw new ValidationException($"'{variable.Name}' has already been declared", _iterator.Current.Line, _iterator.Current.Column, true);
            }

            //Variable name is declared higher up, but that's valid
            if(alreadyDeclared)
            {
                AddValidationException(new ValidationException($"Variable '{variable.Name}' hiding previously declared variable. Consider renaming", _iterator.Current.Line, _iterator.Current.Column, level: Level.Warning));
            }
        }

        private bool TryGetDeclaration(string name, out IKSValue variable)
        {
            LinkedListNode<Dictionary<string, IKSValue>> currentNode = _declarationNode;
            variable = null;

            do
            {
                if(currentNode.Value.TryGetValue(name, out IKSValue value))
                {
                    variable =  value;

                    return true;
                }

                currentNode = currentNode.Previous;
            } while (currentNode != null);

            return false;
        }

        private void AddValidationException(ValidationException ex)
        {
            ValidationExceptions.Add(ex);

            OnValidationError?.Invoke(this, ex);
        }

        private class TokenIterator
        {
            public Token Current => _token;
            private Token _token;

            public TokenIterator(IEnumerable<Token> tokens)
            {
                Token prevToken = null;

                foreach(Token token in tokens)
                {
                    if(_token == null)
                    {
                        _token = token;
                    }

                    if (prevToken != null)
                    {
                        prevToken.Next = token;
                    }

                    token.Prev = prevToken;

                    prevToken = token;
                }
            }

            public Token PeekNext()
            {
                Token t = _token?.Next;

                while (t?.Type == TokenTypes.Comment)
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

                if(_token?.Type == TokenTypes.Comment)
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
