using KrunkScriptParser.Models;
using KrunkScriptParser.Models.Blocks;
using KrunkScriptParser.Models.Expressions;
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
        private KSBlock ParseBlock(string blockType, KSAction action = null)
        {
            KSBlock block = new KSBlock
            {
                Keyword = blockType,
                TokenLocation = new TokenLocation(_token)
            };

            if(_token.Value != "{")
            {
                AddValidationException("Missing '{' for block", _token.Prev);
            }
            else
            {
                _iterator.Next();
            }

            AddNewScopeLevel(block);

            //Add variables created in parameter/for statement
            if (action != null)
            {
                foreach (KSParameter v in action.Parameters)
                {
                    AddDeclaration(v);
                }
            }

            int conditionalState = int.MaxValue; //0, 1, 2 = if, else if, else
            Token currentToken = _token;

            //End of file
            if (_token == null)
            {
                return block;
            }

            while (_token.Value != "}")
            {
                TokenLocation lineStart = new TokenLocation(_token);

                IKSValue line = ParseLine();

                //If we're not getting anywhere, throw an exception
                if(currentToken == _token)
                {
                    AddValidationException("Failed to parse line", lineStart, willThrow: true);
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
                            AddValidationException($"'{conditionalBlock.Key}' block without an 'if' block", conditionalBlock.TokenLocation);
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

            _iterator.Next(false);

            //End of file
            if(_token == null)
            {
                return block;
            }

            if (blockType != "action" && _token.Type == TokenTypes.Terminator)
            {
                AddValidationException($"Unnecessary terminator ';'", _token, level: Level.Warning);

                _iterator.Next();
            }

            return block;
        }

        private IKSValue ParseLine()
        {
            TokenLocation lineStart = new TokenLocation(_token);

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
                        TokenLocation = new TokenLocation(_token)
                    };
                    
                    //Has a value
                    if (_token.Type != TokenTypes.Terminator)
                    {
                        statement.Value = ParseExpression();
                    }

                    returnValue = statement;
                }
                else if (IsConditionalBlock(key)) //Handles if/else if/else
                {
                    return ParseConditionalBlock(key);
                }
                else if (IsLoopBlock(key))
                {
                    return ParseLoopBlock(key);
                }
                else if (key == "break" || key == "continue")
                {
                    _iterator.Next();

                    var currentNode = _blockNode;

                    bool found = false;

                    while(currentNode != null)
                    {
                        if (currentNode.Value.Keyword == "while" ||
                            currentNode.Value.Keyword == "for")
                        {
                            found = true;
                        }

                        currentNode = currentNode.Previous;
                    }

                    if(!found)
                    {
                        AddValidationException($"Invalid call to '{key}' inside '{_blockNode.Value.Keyword}' statement", lineStart, _token);
                    }
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
                        AddValidationException($"Expected a type cast. Received '{next.Value}'", _token);

                        //Go to end of terminator
                        _iterator.SkipUntil(TokenTypes.Terminator);
                    }
                }

                //Expression parsing includes assignment operators + methods
                KSExpression expression = ParseExpression();

                //Update object properties for autocomplete purposes
                if(expression.Items.Count >= 2)
                {
                    if(expression.Items[0] is ExpressionValue expressionValue && expression.Items[1] is ExpressionOperator expressionOp)
                    {
                        if(expressionOp.IsAssignment && expressionValue.Type == KSType.Any && expressionValue.Value is KSVariableName variableName)
                        {
                            //Grab value and add it to the declaration
                            UpdateDeclaration(variableName.Variable);
                        }
                    }
                }
            }
            else if(_token.Type == TokenTypes.GlobalObject) //Could probably throw this in there ^
            {
                ParseExpression();
            }
            else if(_token.Type == TokenTypes.KeyMethod)
            {
                string method = _token.Value;

                _iterator.Next();

                if (method == "remove")
                {
                    //Parse expression will handle index validation
                    KSExpression value = ParseExpression();

                    if (value.Type != KSType.Any)
                    {
                        if (value.Items.FirstOrDefault() is ExpressionValue expressionValue &&
                            expressionValue.Value is KSVariableName variableName)
                        {
                            KSType finalExpressionType = variableName.Type;

                            if (!variableName.Variable.Value.Type.IsArray)
                            {
                                AddValidationException($"Expected an array for 'remove'. Received: '{variableName.Variable.Value.Type}'. Variable '{variableName.Variable.Name}'", value.TokenLocation, value.EndTokenLocation);
                                _iterator.SkipUntil(TokenTypes.Terminator);
                            }
                            else if (finalExpressionType.ArrayDepth < 0) //Being indexed too far
                            {
                                AddValidationException($"Array indexed too far for 'remove'. Found '{variableName.Variable.Name}'", value.TokenLocation, value.EndTokenLocation);
                            }
                            else if (finalExpressionType.ArrayDepth == variableName.Variable.Value.Type.ArrayDepth) //Wasn't indexed at all
                            {
                                AddValidationException($"Expected an array property for 'remove'. Variable '{variableName.Variable.Name}'", value.TokenLocation, value.EndTokenLocation);
                            }
                        }
                        else
                        {
                            AddValidationException($"Expected an array type for 'remove'. Received '{value.Type}'", value.TokenLocation, value.EndTokenLocation);
                            _iterator.SkipUntil(TokenTypes.Terminator);
                        }
                    }
                }
                else if (method == "addTo")
                {
                    bool hasError = false;

                    KSExpression array = ParseExpression();

                    if (!array.Type.IsArray && array.Type != KSType.Any)
                    {
                        hasError = true;
                        AddValidationException($"Expected an array. Received '{array.Type}'", array.TokenLocation, array.EndTokenLocation);
                    }

                    array.Type.DecreaseDepth();
                    KSExpression item = ParseExpression();

                    if(!hasError && array.Type != item.Type && item.Type != null) //Null item.Type means it failed to parse the expression before the terminator
                    {
                        AddValidationException($"Expected type '{array.Type}'. Received '{item.Type}'", item.TokenLocation, item.EndTokenLocation);
                    }
                }
                else
                {
                    AddValidationException($"Unknown command '{method}'", _token.Prev);
                }
            }
            else
            {
                AddValidationException($"Unexpected value '{_token.Value}'", _token);

                _iterator.SkipUntil(TokenTypes.Terminator);
            }

            if(_token.Type != TokenTypes.Terminator)
            {
                AddValidationException($"Missing ';' on line {_token.Line}", _token.Prev);
            }
            else
            {
                _iterator.Next();
            }

            return returnValue;
        }

        /// <summary>
        /// Parses a block from a keyword (if, else if, else, while)
        /// </summary>
        private KSBlock ParseConditionalBlock(string key)
        {
            KSConditionalBlock block = new KSConditionalBlock
            {
                Key = key,
                TokenLocation = new TokenLocation(_token)
            };

            _iterator.Next();

            //else statement don't have conditions
            if (key != "else")
            {
                if (_token.Value != "(")
                {
                    AddValidationException($"Missing start of '{key}' statement condition '('", _token.Prev);

                    _iterator.SkipUntil(new HashSet<string> { "{" });
                }
                else
                {
                    _iterator.Next();

                    block.Condition = ParseExpression();

                    if (_token.Value != ")")
                    {
                        AddValidationException($"Missing end of '{key}' statement condition ')'", _token.Prev);

                        _iterator.SkipUntil(new HashSet<string> { "{" });
                    }
                    else
                    {
                        _iterator.Next();
                    }

                    if(block.Condition.Type != KSType.Bool)
                    {
                        AddValidationException($"if/else if statement conditions require type '{KSType.Bool}'. Received '{block.Condition.Type}'", block.Condition.TokenLocation, block.Condition.EndTokenLocation);
                    }
                }
            }

            KSBlock ksBlock = ParseBlock(key);

            block.Lines.AddRange(ksBlock.Lines);

            return block;
        }

        private KSBlock ParseLoopBlock(string key)
        {
            KSLoopBlock block = new KSLoopBlock
            {
                Key = key,
                TokenLocation = new TokenLocation(_token.Prev)
            };

            //PATCH: Dealing with if statement assignments in their (). A new level won't hurt anything
            AddNewScopeLevel(new KSBlock { Keyword = key });

            try
            {
                _iterator.Next();

                if (_token.Value != "(")
                {
                    AddValidationException($"Missing start of '{key}' statement condition '('", _token.Prev);

                    _iterator.SkipUntil(new HashSet<string> { "{" });
                }
                else
                {
                    _iterator.Next();

                    if (key == "while")
                    {
                        block.Condition = ParseExpression();
                    }
                    else if (key == "for")
                    {
                        if (_token.Type == TokenTypes.Type)
                        {
                            //Handles the terminator
                            block.Assignment = ParseVariableDeclaration();
                        }
                        else //Simple assignment
                        {
                            block.Assignment = ParseExpression();

                            if (_token.Type != TokenTypes.Terminator)
                            {
                                AddValidationException($"Missing ';' in '{key}' statement", _token);
                            }
                            else
                            {
                                _iterator.Next();
                            }
                        }

                        //Condition
                        block.Condition = ParseExpression();

                        if (_token.Type != TokenTypes.Terminator)
                        {
                            AddValidationException($"Missing ';' in '{key}' statement", _token);
                        }

                        if(block.Condition.Type != KSType.Bool)
                        {
                            AddValidationException($"Condition in '{key}' statement must return type '{KSType.Bool}'", block.Condition.TokenLocation, block.Condition.EndTokenLocation);
                        }

                        _iterator.Next();

                        //Increment (after loop)
                        block.Increment = ParseExpression();

                        if (!block.Increment.HasAssignment && !block.Increment.HasPostfix)
                        {
                            AddValidationException($"Missing assignment in '{key}' statement", block.Increment.TokenLocation, block.Increment.EndTokenLocation);
                        }
                    }

                    if (_token.Value != ")")
                    {
                        AddValidationException($"Missing end of '{key}' statement condition ')'", _token.Prev);
                    }
                    else
                    {
                        _iterator.Next();
                    }

                    if (block.Condition.Type != KSType.Bool)
                    {
                        AddValidationException($"if/else if statement conditions require type '{KSType.Bool}'. Received '{block.Condition.Type}'", block.Condition.TokenLocation, block.Condition.EndTokenLocation);
                    }
                }

                KSBlock ksBlock = ParseBlock(key);

                block.Lines.AddRange(ksBlock.Lines);
            }
            finally
            {
                RemoveScopeLevel(); //Making sure this level is removed
            }

            return block;
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
