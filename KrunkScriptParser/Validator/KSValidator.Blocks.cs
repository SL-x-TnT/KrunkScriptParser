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
        private KSBlock ParseBlock(string blockType, IEnumerable<IKSValue> variables = null)
        {
            KSBlock block = new KSBlock
            {
                Keyword = blockType
            };

            if(_token.Value != "{")
            {
                AddValidationException("Missing '{' for block");
            }
            else
            {
                _iterator.Next();
            }

            AddNewScopeLevel();

            //Add variables created in parameter/for statement
            if (variables != null)
            {
                foreach (IKSValue v in variables)
                {
                    AddDeclaration(v);
                }
            }

            while(_token.Value != "}")
            {
                block.Lines.Add(ParseLine());
            }

            RemoveScopeLevel();

            _iterator.Next();

            return block;
        }

        private IKSValue ParseLine()
        {
            string key = String.Empty;

            //Finds the key and places iterator on the last keyword
            if(_token.Type == TokenTypes.Keyword)
            {
                key = _token.Value;

                Token currentToken = _token;

                while(_iterator.Next().Type == TokenTypes.Keyword)
                {
                    key += _token.Value;
                }

                _iterator.ReturnTo(currentToken);
            }

            IKSValue returnValue = null;

            if (_token.Type == TokenTypes.Keyword)
            {
                //Handle return statements
                if (key == "return")
                {
                    _iterator.Next();

                    returnValue = new KSStatement
                    {
                        Statement = "return",
                        Line = _token.Line,
                        Column = _token.Column,
                        Value = ParseExpression(),
                    };
                }
                else if (IsConditionalBlock(key)) //Handles if/else if/else
                {
                    return ParseConditionalBlock(key);
                }
                else if (IsLoopBlock(key))
                {

                }
            }
            else if (_token.Type == TokenTypes.Type)
            {
                //Handles terminator itself
                returnValue = ParseVariableDeclaration();

                AddDeclaration(returnValue);

                return returnValue;
            }
            else if (_token.Type == TokenTypes.Name || _token.Value == "(") //Variable assignment
            {
                if(_token.Value == "(")
                {
                    Token next = _iterator.PeekNext();

                    if(next.Type != TokenTypes.Type)
                    {
                        AddValidationException($"Expected a type cast. Received: {next.Value}");

                        //Go to end of terminator
                        _iterator.SkipUntil(TokenTypes.Terminator);
                    }
                }

                //Variable assignment, including operators like +=
                //TODO
                returnValue = ParseExpression();
            }
            else
            {
                AddValidationException($"Unexpected value '{_token.Value}'");

                _iterator.SkipUntil(TokenTypes.Terminator);
            }

            if(_token.Type != TokenTypes.Terminator)
            {
                AddValidationException("Missing ';' for line");
            }
            else
            {
                _iterator.Next();
            }

            return returnValue;
        }

        /// <summary>
        /// Parses a block from a keyword (if, else if, else)
        /// </summary>
        private KSBlock ParseConditionalBlock(string key)
        {
            KSConditionalBlock block = new KSConditionalBlock();

            _iterator.Next();

            //else statement don't have conditions
            if (key != "else")
            {
                if (_token.Value != "(")
                {
                    AddValidationException($"Missing start of '{key}' statement condition '('");

                    _iterator.SkipUntil(new HashSet<string> { "{"});
                }
                else
                {
                    block.Condition = ParseExpression();

                    if (_token.Value != ")")
                    {
                        AddValidationException($"Missing end of '{key}' statement condition ')'");
                    }
                    else
                    {
                        _iterator.Next();
                    }
                }

                KSBlock ksBlock = ParseBlock(key);

                block.Lines.AddRange(ksBlock.Lines);
            }

            return block;
        }

        private KSBlock ParseLoopBlock(string key)
        {
            return null;
        }


        /// <summary>
        /// Parses built in array keywords (remove, lengthOf, addTo)
        /// </summary>
        /// <returns></returns>
        private IKSValue ParseArrayMethod()
        {
            return null;
        }

        private bool IsArrayKeyword(string key)
        {
            switch(key)
            {
                case "remove":
                case "lengthOf":
                case "addTo":
                    return true;
            }

            return false;
        }

        private bool IsLoopBlock(string key)
        {
            switch (key)
            {
                case "while":
                case "for":
                    return true;
            }

            return false;
        }

        private bool IsConditionalBlock(string key)
        {
            switch(key)
            {
                case "if":
                case "else if":
                case "else":
                    return true;
            }

            return false;
        }
    }
}
