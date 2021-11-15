using KrunkScriptParser.Helpers;
using KrunkScriptParser.Models;
using KrunkScriptParser.Models.Blocks;
using KrunkScriptParser.Models.Expressions;
using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Validator
{
    public partial class KSValidator
    {
        private enum ValidatorPass { Declarations, Final };

        public List<ValidationException> ValidationExceptions { get; private set; } = new List<ValidationException>();

        private LinkedList<Dictionary<string, IKSValue>> _declarations = new LinkedList<Dictionary<string, IKSValue>>();
        private LinkedListNode<Dictionary<string, IKSValue>> _declarationNode;
        private LinkedList<KSBlock> _blocks = new LinkedList<KSBlock>();
        private LinkedListNode<KSBlock> _blockNode;

        private static Dictionary<string, IKSValue> _defaultDeclarations = new Dictionary<string, IKSValue>();
        private static Dictionary<string, IKSValue> _krunkerGlobalVariables = new Dictionary<string, IKSValue>();

        private TokenIterator _iterator;
        private Token _token
        {
            get
            {
                return _iterator?.Current;
            }
        }

        private TokenReader _reader;
        private ValidatorPass _pass = ValidatorPass.Declarations;
        private bool _hookDeclared = false;

        //Used for auto complete
        internal List<KSBlock> _completionBlocks = new List<KSBlock>();

        public event EventHandler<ValidationException> OnValidationError;

        public KSValidator(string text)
        {
            _reader = new TokenReader(text);
        }

        /// <summary>
        /// Validates the KrunkScript text
        /// </summary>
        /// <returns>Whether or not the validator encountered an unknown error</returns>
        public bool Validate()
        {
            try
            {
                InitializeGlobals();

                AddNewScopeLevel(new KSBlock { Keyword = "global", TokenLocation = new TokenLocation(_token) });

                InitializeTokens();

                foreach (ValidatorPass pass in Enum.GetValues(typeof(ValidatorPass)))
                {
                    _iterator.Reset();

                    _pass = pass;

                    ParseTokens();
                }

                //Validate calls

                //Final global scope
                RemoveScopeLevel();

                return true;
            }

            catch (ValidationException ex)
            {
                AddValidationException(ex, _pass);

                return false;
            }
            catch (Exception ex)
            {
                AddValidationException($"Parser failed due to an exception. Message: {ex.Message}", _token, pass: _pass);

                return false;
            }
        }

        private void ParseTokens()
        {

            SkipComments();

            while (_token != null)
            {
                Token currentToken = null;

                currentToken = _token;

                bool hasType = false;

                if (_token.Type == TokenTypes.Type)
                {
                    //Parse type to see next token
                    ParseType();

                    hasType = true;
                }

                Token nextToken = _token;
                _iterator.ReturnTo(currentToken);

                if (hasType)
                {
                    if (nextToken.Type == TokenTypes.Name)
                    {
                        ParseVariableDeclaration();
                    }
                    else if (nextToken.Type == TokenTypes.Action)
                    {
                        ParseAction();
                    }
                    else
                    {
                        AddValidationException($"Unexpected input {nextToken.Value}. Expected variable name or action", nextToken, willThrow: true);
                    }
                }
                else
                {
                    //Hook or action without return type
                    if (nextToken.Type == TokenTypes.Action || nextToken.Value == "public")
                    {
                        KSAction action = ParseAction();
                    }
                    else
                    {
                        AddValidationException($"Unexpected input {nextToken.Value}. Expected 'public' or 'action'", nextToken, willThrow: true);
                    }
                }
            }
        }

        /// <summary>
        /// Generates the token iterator
        /// </summary>
        private void InitializeTokens()
        {
            _iterator = new TokenIterator(_reader.ReadAllTokens());
        }

        /// <summary>
        /// Parses a type token
        /// </summary>
        private KSType ParseType()
        {
            if(_token.Type != TokenTypes.Type)
            {
                AddValidationException($"Expected a type. Received '{_token.Value}'", _token, willThrow: true);
            }

            KSType type = new KSType
            {
                Name = _token.Value,
                TokenLocation = new TokenLocation(_token)
            };

            _iterator.Next();

            bool isDeclared = false;
            bool wasDeclared = false;

            Token currentToken = _token;

            while (TryReadIndexer(out IKSValue v, ref isDeclared))
            {
                type.IncreaseDepth();

                //Array values
                if(wasDeclared && _token.Value != "]")
                {
                    return type;
                }

                //Array values got declared, so return to prior expression to read values
                if(isDeclared)
                {
                    _iterator.ReturnTo(currentToken);
                    _iterator.Next();

                    return type;
                }

                wasDeclared = isDeclared;

                currentToken = _token;
            }

            type.EndTokenLocation = new TokenLocation(_token.Prev);

            return type;
        }

        /// <summary>
        /// Parses variable declaration
        /// </summary>
        private KSVariable ParseVariableDeclaration()
        {
            KSVariable variable = new KSVariable
            {
                Type = ParseType(),
                TokenLocation = new TokenLocation(_token),
                Name = ""
            };

            //Expecting a name
            if (_token.Type != TokenTypes.Name)
            {
                AddValidationException($"Expected variable name. Found: {_token.Value}", _token);
            }
            else
            {
                variable.Name = _token.Value;

                _iterator.Next();
            }

            AddDeclaration(variable);

            //TODO: Nested array support
            if (variable.Type.ArrayDepth > 1)
            {/*
                AddValidationException($"Nested arrays currently not validated", level: Level.Info);

                _iterator.SkipUntil(TokenTypes.Terminator);

                _iterator.Next();

                return variable;*/
            }

            //Expecting =
            if (_token.Type != TokenTypes.Assign)
            {
                if (_token.Type == TokenTypes.Terminator)
                {
                    AddValidationException($"{variable.Name} must have a value", _token);

                    _iterator.SkipLine();
                }

                AddValidationException($"Expected '='. Found: {_token.Value}", _token);

                _iterator.SkipLine();

                return variable;
            }

            _iterator.Next();

            //Expecting a value
            KSExpression expression = ParseExpression();

            variable.Value = expression;

            if (variable.Type.FullType != expression.Type.FullType)
            {
                AddValidationException($"Variable '{variable.Name}' expected type '{variable.Type.FullType}'. Received '{expression.Type.FullType}'", expression.TokenLocation, _token);
            }

            if(expression.HasPostfix)
            {
                AddValidationException($"Postfix increment/decrement can not currently be assigned to a variable or used in an expression", expression.TokenLocation, _token);
            }

            //Line terminator
            if (_token.Type != TokenTypes.Terminator)
            {
                AddValidationException($"Expected ';'", _token);

                int line = _token.Line;

                //Go to new line/1 token after terminator
                while((_token.Type == TokenTypes.Punctuation || _token.Type == TokenTypes.Terminator) && _token.Line == line)
                {
                    _iterator.Next();
                }
            }
            else
            {
                _iterator.Next(false);
            }

            return variable;
        }

        #region Utilities

        public List<AutoCompleteSuggestion> AutoCompleteSuggestions(string text, int line, int column)
        {
            text = text.Trim();

            //Assume is a variable declaration
            if(text.Contains(" "))
            {
                return new List<AutoCompleteSuggestion>();
            }

            //Values are 0 indexed
            line++;
            column++;

            KSBlock block = null;

            //Get block
            for (int i = _completionBlocks.Count - 2; i >= 0; i--)
            {
                block = _completionBlocks[i];

                if((block.TokenLocation.Line < line ) || (block.TokenLocation.Line == line && block.TokenLocation.Column >= column))
                {
                    block = _completionBlocks[i + 1];

                    break;
                }
            }

            if(block == null)
            {
                return new List<AutoCompleteSuggestion>();
            }

            List<AutoCompleteSuggestion> suggestions = new List<AutoCompleteSuggestion>();

            string[] parts = text.Split('.');

            if (parts.Length == 0 || String.IsNullOrEmpty(parts[0]))
            {
                return suggestions;
            }

            text = parts.First();

            while(block != null)
            {
                foreach(IKSValue value in block.Declarations.Values)
                {
                    if(value is KSVariable variable)
                    {
                        if (variable.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                        {
                            //Object properties
                            if (variable.Name == text && parts.Length > 1)
                            {
                                if(variable.TryReadObject(out KSObject ksObject))
                                {
                                    ksObject = ReadObjectPath(ksObject, parts.Skip(1).ToArray()) as KSObject;
                                }

                                if (ksObject != null)
                                {
                                    foreach (KeyValuePair<string, IKSValue> property in ksObject.Properties)
                                    {
                                        if (property.Value is KSAction action)
                                        {
                                            StringBuilder detailBuilder = new StringBuilder($"{action.Type} {action.Name}(");
                                            detailBuilder.AppendJoin(", ", action.Parameters.Select(x => $"{x.Type} {x.Name}"));
                                            detailBuilder.Append(")");

                                            StringBuilder formatBuilder = new StringBuilder($"{property.Key}(");
                                            formatBuilder.AppendJoin(", ", action.Parameters.Select((x, i) => $"${{{i + 1}:{x.Name}}}"));
                                            formatBuilder.Append(")");

                                            suggestions.Add(new AutoCompleteSuggestion
                                            {
                                                Text = property.Key,
                                                Type = SuggestionType.Method,
                                                Details = detailBuilder.ToString(),
                                                InsertTextFormat = formatBuilder.ToString(),
                                            });
                                        }
                                        else
                                        {
                                            suggestions.Add(new AutoCompleteSuggestion
                                            {
                                                Text = property.Key,
                                                Type = SuggestionType.Variable,
                                                Details = $"{property.Value.Type} {String.Join(".", parts.Take(parts.Length - 1))}.{property.Key}"
                                            });
                                        }
                                    }
                                }

                                break;
                            }
                            else if(parts.Length == 1)
                            {
                                suggestions.Add(new AutoCompleteSuggestion
                                {
                                    Text = variable.Name,
                                    Type = SuggestionType.Variable,
                                    Details = $"{variable.Type} {variable.Name}"
                                });
                            }
                        }
                    }
                    else if(value is KSAction action && parts.Length == 1) //Custom actions, so none will be multiple parts
                    {
                        if (action.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                        {
                            StringBuilder detailBuilder = new StringBuilder($"{action.Type} {action.Name}(");
                            detailBuilder.AppendJoin(", ", action.Parameters.Select(x => $"{x.Type} {x.Name}"));
                            detailBuilder.Append(")");

                            StringBuilder formatBuilder = new StringBuilder($"{action.Name}(");
                            formatBuilder.AppendJoin(", ", action.Parameters.Select((x, i) => $"${{{i + 1}:{x.Name}}}"));
                            formatBuilder.Append(")");

                            suggestions.Add(new AutoCompleteSuggestion
                            {
                                Text = action.Name,
                                Type = SuggestionType.Method,
                                Details = detailBuilder.ToString(),
                                InsertTextFormat = formatBuilder.ToString()
                            });
                        }
                    }
                }

                block = block.ParentBlock;
            }

            return suggestions;

            IKSValue ReadObjectPath(KSObject ksObject, string[] path)
            {
                bool ended = false;

                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (String.IsNullOrEmpty(parts[i]))
                    {
                        ksObject = null;

                        break;
                    }

                    if (!ksObject.Properties.TryGetValue(parts[i], out IKSValue pValue))
                    {
                        if(ended)
                        {
                            ksObject = null;
                        }

                        ended = true;

                        continue;
                    }

                    if (pValue is KSExpression expression)
                    {
                        //End of values
                        if (!expression.TryReadObject(out ksObject))
                        {
                            break;
                        }
                    }
                    else if (pValue is KSObject tObj)
                    {
                        ksObject = tObj;
                    }
                    else
                    {
                        ksObject = null;

                        break;
                    }
                }

                return ksObject;
            }
        }

        /// <summary>
        /// Manually skip comments. Comments are automatically skipped with _iterator.Next()
        /// </summary>
        private void SkipComments()
        {
            while (_iterator.Current?.Type == TokenTypes.Comment)
            {
                _iterator.Next();
            }
        }

        /// <summary>
        /// Adds a variable/action to the current block level
        /// </summary>
        private void AddDeclaration(IKSValue variable)
        {
            string name = String.Empty;

            if(variable is KSAction action)
            {
                if(action.IsHook)
                {
                    _hookDeclared = true;
                }

                name = action.Name;

                if (!action.IsHook && _hookDeclared && _pass == ValidatorPass.Declarations)
                {
                    AddValidationException($"Action {name} is declared after a hook (public) action. Place before all hooks to prevent issues", variable.TokenLocation, level: Level.Warning, pass: _pass);
                }
            }
            else if(variable is KSVariable v)
            {
                name = v.Name;

                //Variable name is declared higher up, but that's valid
                if (TryGetDeclaration(name, out IKSValue declaredVariable, true))
                {
                    AddValidationException($"Variable '{name}' hiding previously declared variable ({declaredVariable.TokenLocation.Line}:{declaredVariable.TokenLocation.Column})", variable.TokenLocation, level: Level.Warning);
                }

                if (_hookDeclared && _pass == ValidatorPass.Declarations)
                {
                    AddValidationException($"Variable {name} is declared after a hook (public) action. Place before all hooks to prevent issues", variable.TokenLocation, level: Level.Warning, pass: _pass);
                }
            }
            else if (variable is KSParameter parameter)
            {
                name = parameter.Name;
            }

            if(!_declarationNode.Value.TryAdd(name, variable))
            {
                IKSValue value = _declarationNode.Value[name];

                //Only show errors when we're inside an action or during declarations
                if (_declarations.Count > 1 || _pass == ValidatorPass.Declarations)
                {
                    AddValidationException($"Variable/Action '{name}' has already been declared ({value.TokenLocation.Line}:{value.TokenLocation.Column})", variable.TokenLocation, pass: _pass);
                }

                return;
            }
        }

        private void UpdateDeclaration(IKSValue value)
        {
            if(value is KSVariable variable)
            {
                string[] parts = variable.Name.Split('.');

                if(!TryGetDeclaration(parts[0], out IKSValue declaration))
                {
                    return;
                }

                if (declaration.Type != KSType.Object)
                {
                    return;
                }

                if(declaration is KSVariable ksVariable && ksVariable.TryReadObject(out KSObject ksObject))
                {
                    for(int i = 1; i < parts.Length; i++)
                    {
                        string property = parts[i];

                        //Slow, but lazy
                        if(property.Contains("[") || property.Contains("]"))
                        {
                            break;
                        }

                        if (!ksObject.Properties.TryGetValue(parts[i], out IKSValue v))
                        {
                            KSObject newObj = new KSObject
                            {
                                Type = KSType.Any
                            };

                            ksObject.Properties.TryAdd(parts[i], newObj);
                            ksObject = newObj;
                        }
                        else
                        {
                            if(v is KSExpression expression)
                            {
                                expression.TryReadObject(out ksObject);
                            }
                            else if (v is KSObject obj)
                            {
                                ksObject = obj;
                            }
                        }
                    }
                }
            }
            else if (value is KSAction action) //Likely will only be global values
            {

            }
        }

        /// <summary>
        /// Attempts to find a declared action/variable starting from the current block level
        /// </summary>
        private bool TryGetDeclaration(string name, out IKSValue ksValue, bool outsideBlock = false)
        {
            LinkedListNode<Dictionary<string, IKSValue>> currentNode = _declarationNode;
            ksValue = null;

            if(outsideBlock)
            {
                currentNode = currentNode.Previous;
            }

            if (currentNode == null)
            {
                return false;
            }

            do
            {
                if(currentNode.Value.TryGetValue(name, out IKSValue value))
                {
                    ksValue =  value;

                    if(ksValue is KSAction action)
                    {
                        if(action.CallInformation == null)
                        {
                            action.CallInformation = new CallInfo
                            {
                                CallLocation = new TokenLocation(_token)
                            };
                        }

                        break;
                    }
                    else if (ksValue is KSVariable variable)
                    {
                        if(variable.CallInformation == null)
                        {
                            variable.CallInformation = new CallInfo
                             {
                                CallLocation = new TokenLocation(_token)
                             };
                        }

                        ksValue = (IKSValue)variable.Clone();

                        break;
                    }

                    return true;
                }

                currentNode = currentNode.Previous;
            } while (currentNode != null);

            return ksValue != null;
        }

        /// <summary>
        /// Adds a new scope level for variable declarations. Called when entering a new block (actions, if, else, for, while, etc)
        /// </summary>
        private void AddNewScopeLevel(KSBlock block)
        {
            var declarations = new Dictionary<string, IKSValue>();

            if(block.Keyword == "global")
            {
                declarations = new Dictionary<string, IKSValue>(_defaultDeclarations);
            }

            _completionBlocks.Add(block);
            block.Declarations = declarations;
            block.ParentBlock = _blockNode?.Value;

            _declarationNode = new LinkedListNode<Dictionary<string, IKSValue>>(declarations);
            _declarations.AddLast(_declarationNode);

            _blockNode = new LinkedListNode<KSBlock>(block);
            _blocks.AddLast(_blockNode);
        }

        /// <summary>
        /// Removes the last scope level when exiting a block (actions, if, else, for, while, etc)
        /// </summary>
        private void RemoveScopeLevel()
        {
            //Add info warning for declared variables that weren't used
            foreach(KeyValuePair<string, IKSValue> declaration in _declarationNode.Value)
            {
                if (declaration.Value is KSParameter parameter && parameter.IsHookParameter)
                {
                    continue;
                }

                if (declaration.Value is KSVariable variable)
                {
                    if(!variable.WasCalled)
                    {
                        AddValidationException($"Variable '{variable.Name}' was declared and never used", variable.TokenLocation, level: Level.Info);
                    }
                }
                else if(declaration.Value is KSAction action && !action.IsHook)
                {
                    if(!action.WasCalled)
                    {
                        AddValidationException($"Action '{action.Name}' was declared and never called", action.TokenLocation, level: Level.Info);
                    }
                }
            }


            _declarations.RemoveLast();
            _declarationNode = _declarations.Last;

            _blocks.RemoveLast();
            _blockNode = _blocks.Last;
        }

        /// <summary>
        /// Adds a validation exception. Will continue parsing
        /// </summary>
        private void AddValidationException(ValidationException ex, ValidatorPass pass = ValidatorPass.Final)
        {
            if (_pass == pass)
            {
                ValidationExceptions.Add(ex);

                OnValidationError?.Invoke(this, ex);
            }
        }

        private void AddValidationException(string message, TokenLocation startToken, TokenLocation endToken = null, Level level = Level.Error, bool willThrow = false, ValidatorPass pass = ValidatorPass.Final)
        {
            startToken ??= new TokenLocation(_token);
            endToken ??= startToken;

            ValidationException error = new ValidationException(message, startToken, endToken, level);

            if (willThrow)
            {
                throw error;
            }
            else
            {
                AddValidationException(error, pass);
            }
        }

        private void AddValidationException(string message, TokenLocation startToken, Token endToken, Level level = Level.Error, bool willThrow = false, ValidatorPass pass = ValidatorPass.Final)
        {
            TokenLocation endLocation = endToken == null ? startToken : new TokenLocation(endToken);

            AddValidationException(message, startToken, endLocation, level, willThrow, pass);
        }

        private void AddValidationException(string message, Token startToken, Token endToken = null, Level level = Level.Error, bool willThrow = false, ValidatorPass pass = ValidatorPass.Final)
        {
            endToken ??= startToken;

            AddValidationException(message, new TokenLocation(startToken), new TokenLocation(endToken), level, willThrow, pass);
        }

        #endregion

        #region Token Iterator

        private class TokenIterator
        {
            public Token Current
            {
                get
                {
                    //Failsafe to prevent infinite loops due to parsing bugs
                    //Guessing 10k won't ever be hit under normal conditions
                    if(++_counter >= 10000)
                    {
                        throw new ValidationException($"Parser entered an infinite loop. Stopping validation...", new TokenLocation(_token), new TokenLocation(_token));
                    }

                    return _token;
                }
            }
            public Token Previous { get; private set; }

            private Token _token;
            private int _counter = 0;
            private Token _initialToken;

            public TokenIterator(IEnumerable<Token> tokens)
            {
                _initialToken = tokens.FirstOrDefault();

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

            public void Reset()
            {
                _token = _initialToken;
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
                _counter = 0;

                Previous = _token ?? Previous;
                _token = _token?.Next;


                if (checkEOF && _token == null)
                {
                    throw new ValidationException("Unexpected end of file", new TokenLocation(Previous), new TokenLocation(Previous));
                }

                if (_token?.Type == TokenTypes.Comment)
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
            
            public void ReturnTo(Token token)
            {
                if(_token == token)
                {
                    return;
                }

                while(Prev() != token)
                {
                    
                }
            }

            public Token SkipLine()
            {
                while(_token.Line == _token.Next?.Line)
                {
                    Next();
                }

                return Next();
            }

            public void SkipBlock()
            {
                if(_token.Value != "{")
                {
                    return;
                }

                int count = 1;

                while(Next() != null)
                {
                    if(_token.Value == "{")
                    {
                        ++count;
                    }
                    else if (_token.Value == "}")
                    {
                        --count;
                    }

                    if(count == 0)
                    {
                        Next(false);

                        break;
                    }
                }
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

        #endregion
    }
}
