﻿using KrunkScriptParser.Helpers;
using KrunkScriptParser.Models;
using KrunkScriptParser.Models.Blocks;
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
        public List<ValidationException> ValidationExceptions { get; private set; } = new List<ValidationException>();

        private LinkedList<Dictionary<string, IKSValue>> _declarations = new LinkedList<Dictionary<string, IKSValue>>();
        private LinkedListNode<Dictionary<string, IKSValue>> _declarationNode;
        private LinkedList<KSBlock> _blocks = new LinkedList<KSBlock>();
        private LinkedListNode<KSBlock> _blockNode;

        private Dictionary<string, IKSValue> _krunkerGlobalVariables = new Dictionary<string, IKSValue>();

        private TokenIterator _iterator;
        private Token _token => _iterator.Current;
        private TokenReader _reader;

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
            InitializeGlobals();

            try
            {
                ParseTokens();

                return true;
            }
            catch(Exception ex)
            {
                AddValidationException($"Parser failed due to an exception. Message: {ex.Message}");

                return false;
            }
        }

        private void ParseTokens()
        {
            InitializeTokens();

            SkipComments();

            //Variables
            _declarationNode = new LinkedListNode<Dictionary<string, IKSValue>>(new Dictionary<string, IKSValue>());
            _declarations.AddFirst(_declarationNode);

            //Blocks to determine whether continue/break statements are valid
            _blockNode = new LinkedListNode<KSBlock>(new KSBlock { Keyword = "global" });
            _blocks.AddFirst(_blockNode);

            while (_token != null)
            {
                Token currentToken = null;

                try
                {
                    currentToken = _token;
                    bool hasType = false;

                    if(_token.Type == TokenTypes.Type)
                    {
                        //Parse type to see next token
                        ParseType();

                        hasType = true;
                    }

                    Token nextToken = _token;
                    _iterator.ReturnTo(currentToken);

                    if (hasType)
                    {
                        if(nextToken.Type == TokenTypes.Name)
                        {
                            ParseVariableDeclaration();
                        }
                        else if(nextToken.Type == TokenTypes.Action)
                        {
                            ParseAction();
                        }
                        else
                        {
                            throw new ValidationException($"Unexpected input {nextToken.Value}. Expected variable name or action", nextToken.Line, nextToken.Column);
                        }
                    }
                    else
                    {
                        //Hook or action without return type
                        if(nextToken.Type == TokenTypes.Action || nextToken.Value == "public")
                        {
                            KSAction action = ParseAction();
                        }
                        else
                        {
                            throw new ValidationException($"Unexpected input {nextToken.Value}. Expected 'public' or 'action'", nextToken.Line, nextToken.Column);
                        }
                    }
                }
                catch (ValidationException ex)
                {
                    AddValidationException(ex);

                    break;
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
                AddValidationException($"Expected a type. Received '{_token.Value}'", willThrow: true);
            }

            KSType type = new KSType
            {
                Name = _token.Value
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
            }

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
                Line = _token.Line,
                Column = _token.Column,
                Name = ""
            };

            //Expecting a name
            if (_token.Type != TokenTypes.Name)
            {
                AddValidationException($"Expected variable name. Found: {_token.Value}");
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
                    AddValidationException($"{variable.Name} must have a value");

                    _iterator.SkipLine();
                }

                AddValidationException($"Expected '='. Found: {_token.Value}");

                _iterator.SkipLine();

                return variable;
            }

            _iterator.Next();

            //Expecting a value
            KSExpression expression = ParseExpression();

            variable.Value = expression;

            if (variable.Type.FullType != expression.Type.FullType)
            {
                AddValidationException($"Variable '{variable.Name}' expected type '{variable.Type.FullType}'. Received '{expression.Type.FullType}'");
            }

            if(expression.HasPostfix)
            {
                AddValidationException($"Postfix increment/decrement can not currently be assigned to a variable or used in an expression");
            }

            //Line terminator
            if (_token.Type != TokenTypes.Terminator)
            {
                AddValidationException($"Expected ';'", column: _token.Prev.ColumnEnd);

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

        #region Utilities

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
            bool alreadyDeclared = false;

            if(variable is KSAction action)
            {
                name = action.Name;
            }
            else if(variable is KSVariable v)
            {
                name = v.Name;
                alreadyDeclared = TryGetDeclaration(name, out IKSValue _);
            }
            else if (variable is KSParameter parameter)
            {
                name = parameter.Name;
            }

            if(!_declarationNode.Value.TryAdd(name, variable))
            {
                IKSValue value = _declarationNode.Value[name];

                AddValidationException($"Variable/Action '{name}' has already been declared ({value.Line}:{value.Column})");

                return;
            }

            //Variable name is declared higher up, but that's valid
            if(alreadyDeclared)
            {
                AddValidationException($"Variable '{name}' hiding previously declared variable", level: Level.Warning);
            }
        }

        /// <summary>
        /// Attempts to find a declared action/variable starting from the current block level
        /// </summary>
        private bool TryGetDeclaration(string name, out IKSValue ksValue)
        {
            LinkedListNode<Dictionary<string, IKSValue>> currentNode = _declarationNode;
            ksValue = null;

            do
            {
                if(currentNode.Value.TryGetValue(name, out IKSValue value))
                {
                    ksValue =  value;

                    if(ksValue is KSAction action)
                    {
                        action.WasCalled = true;
                    }
                    else if (ksValue is KSVariable variable)
                    {
                        variable.WasCalled = true;
                    }

                    return true;
                }

                currentNode = currentNode.Previous;
            } while (currentNode != null);

            return false;
        }

        /// <summary>
        /// Adds a new scope level for variable declarations. Called when entering a new block (actions, if, else, for, while, etc)
        /// </summary>
        private void AddNewScopeLevel(KSBlock block)
        {
            _declarationNode = new LinkedListNode<Dictionary<string, IKSValue>>(new Dictionary<string, IKSValue>());
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
            _declarations.RemoveLast();
            _declarationNode = _declarations.Last;

            _blocks.RemoveLast();
            _blockNode = _blocks.Last;
        }

        /// <summary>
        /// Adds a validation exception. Will continue parsing
        /// </summary>
        private void AddValidationException(ValidationException ex)
        {
            ValidationExceptions.Add(ex);

            OnValidationError?.Invoke(this, ex);
        }

        private void AddValidationException(string message, int? line = null, int? column = null, Level level = Level.Error, bool willThrow = false)
        {
            line = line ?? _token?.Line ?? 0;
            column = column ?? _token?.Column ?? 0;

            ValidationException error = new ValidationException(message, line.Value, column.Value, level);

            if (willThrow)
            {
                throw error;
            }
            else
            {
                AddValidationException(error);
            }
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
                        throw new ValidationException($"Parser entered an infinite loop. Stopping validation...", _token.Line, _token.Column);
                    }

                    return _token;
                }
            }

            private Token _token;
            private int _counter = 0;

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
                _counter = 0;

                if(checkEOF)
                {
                    if(_token == null)
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
