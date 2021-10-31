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
                AddValidationException(new ValidationException($"Parser failed due to an exception. Message: {ex.Message}", _token.Line, _token.Column));

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

        /// <summary>
        /// Parses variable declaration
        /// </summary>
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
        /// Adds a variable to the current block level
        /// </summary>
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
                AddValidationException(new ValidationException($"Variable '{variable.Name}' hiding previously declared variable", _iterator.Current.Line, _iterator.Current.Column, level: Level.Info));
            }
        }

        /// <summary>
        /// Attempts to find a declared action/variable starting from the current block level
        /// </summary>
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

        /// <summary>
        /// Adds a validation exception. Will continue parsing
        /// </summary>
        private void AddValidationException(ValidationException ex)
        {
            ValidationExceptions.Add(ex);

            OnValidationError?.Invoke(this, ex);
        }

        #endregion

        #region Token Iterator

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

        #endregion
    }
}
