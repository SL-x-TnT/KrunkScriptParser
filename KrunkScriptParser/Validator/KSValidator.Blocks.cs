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

            int conditionalState = int.MaxValue; //0, 1, 2 = if, else if, else
            Token currentToken = _token;

            while(_token.Value != "}")
            {
                IKSValue line = ParseLine();

                //If we're not getting anywhere, throw an exception
                if(currentToken == _token)
                {
                    AddValidationException("Failed to parse line", willThrow: true);
                }

                if(line is KSConditionalBlock conditionalBlock)
                {
                    if(conditionalBlock.IsIf)
                    {
                        conditionalState = 0;
                    }
                    else if (conditionalBlock.IsElseIf && conditionalState <= 2)
                    {
                        conditionalState = 2;
                    }
                    else 
                    {
                        if(conditionalState > 2)
                        {
                            AddValidationException($"'{conditionalBlock.Key}' block without an 'if' block", conditionalBlock.Line, conditionalBlock.Column);
                        }

                        conditionalState = int.MaxValue;
                    }
                }
                else
                {
                    conditionalState = int.MaxValue;
                }

                block.Lines.Add(line); 
                currentToken = _token;
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
                    key += $" {_token.Value}";
                    currentToken = _token;
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


                    KSStatement statement = new KSStatement
                    {
                        Statement = "return",
                        Line = _token.Line,
                        Column = _token.Column,
                    };
                    
                    //Has a value
                    if (_token.Type != TokenTypes.Terminator)
                    {
                        statement.Value = ParseExpressionNew();
                    }

                    returnValue = statement;
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

                return returnValue;
            }
            else if (_token.Type == TokenTypes.Name || _token.Value == "(") //Variable assignment
            {
                if(_token.Value == "(")
                {
                    Token next = _iterator.PeekNext();

                    if(next.Type != TokenTypes.Type)
                    {
                        AddValidationException($"Expected a type cast. Received '{next.Value}'");

                        //Go to end of terminator
                        _iterator.SkipUntil(TokenTypes.Terminator);
                    }
                }

                //Variable assignment, including operators like +=
                //TODO
                _iterator.SkipUntil(TokenTypes.Terminator);

                //returnValue = ParseExpression();
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
            KSConditionalBlock block = new KSConditionalBlock
            {
                Key = key,
                Line = _token.Line,
                Column = _token.Column
            };

            _iterator.Next();

            //else statement don't have conditions
            if (key != "else")
            {
                if (_token.Value != "(")
                {
                    AddValidationException($"Missing start of '{key}' statement condition '('");

                    _iterator.SkipUntil(new HashSet<string> { "{" });
                }
                else
                {
                    _iterator.Next();

                    block.Condition = ParseExpressionNew();

                    if (_token.Value != ")")
                    {
                        AddValidationException($"Missing end of '{key}' statement condition ')'");
                    }
                    else
                    {
                        _iterator.Next();
                    }

                    if(block.Condition.Type != KSType.Bool)
                    {
                        AddValidationException($"if/else if statement conditions require type '{KSType.Bool}'. Received '{block.Condition.Type}'");
                    }
                }
            }

            KSBlock ksBlock = ParseBlock(key);

            block.Lines.AddRange(ksBlock.Lines);

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
