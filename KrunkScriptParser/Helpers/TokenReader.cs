using KrunkScriptParser.Models;
using KrunkScriptParser.Models.Tokens;
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

        private static readonly HashSet<string> _actionKeywords = new HashSet<string> { "action" };
        private static readonly HashSet<string> _modifierKeywords = new HashSet<string> { "public", "static" };
        private static readonly HashSet<string> _typeKeywords = new HashSet<string> { "obj", "num", "str", "bool" };
        private static readonly HashSet<string> _boolean = new HashSet<string> { "true", "false" };
        private static readonly HashSet<char> _puctuation = new HashSet<char> { '(', ')', '[', ']', '{', '}', ',', '.' };
        private static readonly HashSet<char> _operators = new HashSet<char> { '+', '-', '*', '/', '<', '>', '!', '&', '|', '?', ':', '%', '~' };
        private static readonly HashSet<string> _keywords = new HashSet<string> { "if", "while", "else", "for", "break", "continue", "return" };
        private static readonly HashSet<string> _methods = new HashSet<string> { "addTo", "remove", "lengthOf", "notEmpty", "toStr", "toNum" };
        private static readonly HashSet<string> _globalObjects = new HashSet<string> { "GAME", "UTILS", "Math"};


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
            else if(char.IsLetter(c) || c == '_')
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
            else if (c != '\uffff')
            {
                throw new ValidationException($"Unknown value '{c}' found at line {LineNumber} column {ColumnNumber}", new TokenLocation(token), new TokenLocation(token));
            }

            return token;
        }

        public List<Token> ReadAllTokens()
        {
            List<Token> tokens = new List<Token>();
            Token prevToken = null;

            Token currentToken;

            do
            {
                currentToken = ReadToken();

                if (currentToken.Type != TokenTypes.Unknown)
                {
                    currentToken.Prev = prevToken;

                    if (prevToken != null)
                    {
                        prevToken.Next = currentToken;
                    }

                    tokens.Add(currentToken);
                }

                prevToken = currentToken;
            } while (currentToken.Type != TokenTypes.Unknown);

            return tokens;
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
                //_columnNumber += 4;
                ++_columnNumber;
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

            bool isHex = false;
            bool isENotation = false;

            //Check for hex
            if(PeekChar() == '0')
            {
                builder.Append(ReadChar());

                if (PeekChar() == 'x' || PeekChar() == 'X')
                {
                    isHex = true;

                    builder.Append(ReadChar());
                }
            }

            while(true)
            {
                char c = PeekChar();

                if(c == 'e' && !isENotation && !isHex)
                {
                    isENotation = true;

                    builder.Append(ReadChar());

                    c = PeekChar();

                    if (c == '-' || c == '+')
                    {
                        builder.Append(ReadChar());
                    }
                }
                else if(isHex && IsHex(c) ||         //Hex
                    char.IsDigit(c) ||          //Digit
                    (!isHex && c == '.'))       //Double/Float   
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

                prev = c;
            }

            return builder.ToString();
        }

        private bool IsHex(char c)
        {
            return (c >= 'a' && c <= 'f') ||
                   (c >= 'A' && c <= 'F');
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
            else if (_globalObjects.Contains(name))
            {
                return TokenTypes.GlobalObject;
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
