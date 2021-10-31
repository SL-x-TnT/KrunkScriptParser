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
        private KSBlock ParseBlock(string blockType, IEnumerable<IKSValue> variables)
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
            foreach(IKSValue v in variables)
            {
                AddDeclaration(v);
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
                return ParseVariableDeclaration();
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
        /// Parses a block from a keyword (if, else if, else, while, for)
        /// </summary>
        private KSBlock ParseConditionalBlock(string key)
        {
            return null;
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
