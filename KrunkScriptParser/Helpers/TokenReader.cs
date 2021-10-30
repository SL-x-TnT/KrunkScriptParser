﻿using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Helpers
{
    class TokenReader : StreamReader
    {
        public int LineNumber => _lineNumber;
        public int ColumnNumber => _columnNumber;

        private int _lineNumber = 1;
        private int _columnNumber = 1;
        private long _markPosition;

        private HashSet<string> _actionKeywords = new HashSet<string> { "action" };
        private HashSet<string> _modifierKeywords = new HashSet<string> { "public", "static" };

        private HashSet<string> _typeKeywords = new HashSet<string> { "obj", "num", "str", "bool" };
        private HashSet<string> _boolean = new HashSet<string> { "true", "false" };
        private HashSet<char> _puctuation = new HashSet<char> { '(', ')', '[', ']', '{', '}', ',', ':', '.' };
        private HashSet<char> _operators = new HashSet<char> { '+', '-', '*', '/', '<', '>', '!', '&', '|' };
        private HashSet<string> _keywords = new HashSet<string> { "if", "while", "else", "for", "break", "continue", "return" };
        private HashSet<string> _methods = new HashSet<string> { "addTo", "remove", "lengthOf", "notEmpty", "toStr", "toNum" };


        public TokenReader(string text) : base(new MemoryStream(Encoding.UTF8.GetBytes(text)))
        {
        }

        public Token ReadToken()
        {
            SkipWhiteSpace();

            Token token = new Token
            {
                Line = _lineNumber,
                Column = _columnNumber
            };

            char c = PeekChar();

            if (c == '#')
            {
                token.Type = TokenTypes.Comment;
                token.Value = ReadLine();
            }
            else if (c == '=')
            {
                token.Type = TokenTypes.Assign;
                token.Value = ReadChar().ToString();
            }
            else if (c == ';')
            {
                token.Type = TokenTypes.Terminator;
                token.Value = ReadChar().ToString();
            }
            else if (c == '"' || c == '\'')
            {
                token.Type = TokenTypes.String;
                token.Value = ReadString();
            }
            else if(char.IsDigit(c))
            {
                token.Type = TokenTypes.Number;
                token.Value = ReadNumber();
            }
            else if(char.IsLetter(c))
            {
                string name = ReadName();
                token.Type = GetTokenType(name);
                token.Value = name;
            }
            else if (_puctuation.Contains(c))
            {
                token.Type = TokenTypes.Punctuation;
                token.Value = ReadChar().ToString();
            }
            else if (_operators.Contains(c))
            {
                token.Type = TokenTypes.Operator;
                token.Value = ReadChar().ToString();
            }

            return token;
        }

        #region Helpers

        public void SkipWhiteSpace()
        {
            while (char.IsWhiteSpace(PeekChar()))
            {
                if (ReadChar() == '\n')
                {
                    _lineNumber++;
                    _columnNumber = 1;
                }
            }
        }

        public void Mark()
        {
            _markPosition = BaseStream.Position;
        }

        public void Pop()
        {
            BaseStream.Seek(_markPosition, SeekOrigin.Begin);
        }

        public override string ReadLine()
        {
            ++_lineNumber;
            _columnNumber = 1;

            return base.ReadLine();
        }

        public char ReadChar(bool skipWhiteSpace = false)
        {
            if (skipWhiteSpace)
            {
                SkipWhiteSpace();
            }

            char c = (char)base.Read();

            if (c == '\t')
            {
                _columnNumber += 4;
            }
            else
            {
                ++_columnNumber;
            }

            return c;
        }

        public char PeekChar()
        {
            return (char)Peek();
        }

        #endregion

        #region Token Reading

        private string ReadNumber()
        {
            StringBuilder builder = new StringBuilder();
            char prev = (char)0x00;

            while(true)
            {
                char c = PeekChar();

                if((c == 'x' && prev == '0') ||  //Hex
                    char.IsDigit(c) ||           //Digit
                    c == '.')                    //Double/Float   
                {
                    builder.Append(ReadChar());
                }
                else
                {
                    break;
                }

                prev = c;
            }

            return builder.ToString();
        }

        private string ReadName()
        {
            StringBuilder builder = new StringBuilder();

            while (true)
            {
                char c = PeekChar();

                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    builder.Append(ReadChar());
                }
                else
                {
                    break;
                }
            }

            return builder.ToString();
        }

        private string ReadString()
        {
            StringBuilder builder = new StringBuilder();
            char prev = (char)0x00;
            bool inString = false;

            while (true)
            {
                char c = PeekChar();

                if((c == '"' || c == '\'') && prev != '\\')
                {
                    inString = !inString;
                }

                if(c == '\n' || c == '\r')
                {
                    break;
                }

                builder.Append(ReadChar());

                if (!inString)
                {
                    break;
                }
            }

            return builder.ToString();
        }

        private TokenTypes GetTokenType(string name)
        {
            if(_typeKeywords.Contains(name))
            {
                return TokenTypes.Type;
            }
            else if (_boolean.Contains(name))
            {
                return TokenTypes.Bool;
            }
            else if (_actionKeywords.Contains(name))
            {
                return TokenTypes.Action;
            }
            else if (_keywords.Contains(name))
            {
                return TokenTypes.Keyword;
            }
            else if (_methods.Contains(name))
            {
                return TokenTypes.KeyMethod;
            }
            else if (_modifierKeywords.Contains(name))
            {
                return TokenTypes.Modifier;
            }

            return TokenTypes.Name;
        }

        #endregion

    }
}
