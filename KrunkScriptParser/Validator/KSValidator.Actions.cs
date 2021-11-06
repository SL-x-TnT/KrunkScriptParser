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
        private KSAction ParseAction()
        {
            KSAction action = new KSAction
            {
                TokenLocation = new TokenLocation(_token)
            };

            if(_token.Value == "public")
            {
                //Need to verify it's a hook
                _iterator.Next();
                action.IsHook = true;
            }

            //Has a return type
            if (_token.Type == TokenTypes.Type)
            {
                action.Type = ParseType();
            }

            if(_token.Type != TokenTypes.Action)
            {
                AddValidationException($"Expected 'action'. Received '{_token.Value}'", _token);
            }
            else
            {
                _iterator.Next();
            }

            if(_token.Type != TokenTypes.Name)
            {
                AddValidationException($"Missing action name", _token);
            }
            else
            {
                action.Name = _token.Value;
                action.TokenLocation = new TokenLocation(_token);

                _iterator.Next();
            }

            AddDeclaration(action);

            if (_token.Value != "(")
            {
                AddValidationException($"Expected start of parameters '('. Received '{_token.Value}'", _token, willThrow: true);
            }
            else
            {
                _iterator.Next();
            }

            action.Parameters = ParseParameters();

            if(action.IsHook)
            {
                foreach(KSParameter parameter in action.Parameters)
                {
                    parameter.IsHookParameter = true;
                }
            }

            if(_token.Value != ")")
            {
                bool willThrow = _token.Value != "{";

                AddValidationException($"Expected end of parameters ')'. Received '{_token.Value}'", _token, willThrow: willThrow);
            }
            else
            {
                _iterator.Next();
            }

            action.Block = ParseBlock("action", action);

            if(action.Type != KSType.Void && !action.Block.Lines.Any(x => x is KSStatement statement && statement.IsReturn))
            {
                AddValidationException($"Action '{action.Name}' missing return statement", action.TokenLocation, action.Block.TokenLocation);
            }

            foreach(KSStatement statement in action.GetInvalidReturns())
            {
                AddValidationException($"Invalid return type '{statement.Type.FullType}'. Expected: '{action.Type.FullType}'", statement.TokenLocation, statement.EndTokenLocation);
            }
            
            return action;
        }

        /// <summary>
        /// Parses out the arguments sent to an action
        /// </summary>
        private List<KSExpression> ParseArguments()
        {
            List<KSExpression> values = new List<KSExpression>();

            _iterator.Next();

            while (_token.Value != ")" || _token.Value == ",")
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

        /// <summary>
        /// Parses out the parameter declared on an action
        /// </summary>
        private List<KSParameter> ParseParameters(bool isGlobalMethod = false)
        {
            List<KSParameter> parameters = new List<KSParameter>();

            while ((_token.Type == TokenTypes.Type || _token.Type == TokenTypes.Action) ||
                _token.Value == "." ||
                (isGlobalMethod && _token.Value == "any") || 
                _token.Type == TokenTypes.Modifier
                )
            {
                KSType type = null;

                //Skip modifier, if there's one
                if(_token.Type == TokenTypes.Modifier)
                {
                    if(_token.Value != "static")
                    {
                        AddValidationException($"Expected a type. Received modifier {_token.Value}", _token);
                    }

                    _iterator.Next();
                }

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
                            AddValidationException($"Spread notation '...' not supported", type.TokenLocation, _token.Prev);
                        }
                    }
                    else
                    {
                        AddValidationException($"Unexpected value '{multiProp}'", _token);
                    }
                }

                if (_token.Type != TokenTypes.Name)
                {
                    //Getting too tired to save invalid code
                    AddValidationException($"Expected parameter name. Received '{_token.Value}'", _token, willThrow: true);
                }

                parameters.Add(new KSParameter
                {
                    Name = _token.Value,
                    Type = type,
                    MultiProp = isMultiProp,
                    Optional = optional,
                    TokenLocation = new TokenLocation(_token)
                });

                _iterator.Next();

                //Got more
                if (_token.Value == ",")
                {
                    _iterator.Next();
                }
            }

            if (_token.Value != ")")
            {
                AddValidationException($"Expected ')'. Received '{_token.Value}'", _token.Prev);

                _iterator.SkipUntil(new HashSet<string> { ")", ";" });
            }

            return parameters;
        }

        /// <summary>
        /// Validates that the arguments provided for an action are accurate
        /// </summary>
        private void ValidateArguments(List<KSExpression> arguments, KSAction action)
        {
            int parameterIndex = 0;
            int expected = action.Parameters.Count(x => !x.Optional);
            bool isMulti = false;

            foreach (KSExpression argument in arguments)
            {
                if (parameterIndex >= action.Parameters.Count)
                {
                    AddValidationException($"Invalid argument count. Received {arguments.Count} and expected {action.Parameters.Count}", argument.TokenLocation, argument.EndTokenLocation);

                    return;
                }

                KSParameter parameter = action.Parameters[parameterIndex];

                if (parameter.Type.FullType != argument.Type.FullType && parameter.Type != KSType.Any)
                {
                    if(action.Global)
                    {
                        Level level = Level.Info;

                        //Upgrade to a warning as it could be mismatched arguments
                        if(argument.Type != KSType.Any)
                        {
                            level = Level.Warning;
                        }

                        AddValidationException($"Global method '{action.Name}' expected type '{parameter.Type.FullType}' for parameter '{parameter.Name}' (arg: {parameterIndex + 1}). Received '{argument.Type.FullType}'. Will still pass validation", argument.TokenLocation, argument.EndTokenLocation, level: level);
                    }
                    else if(!action.Global && argument.Type != KSType.Any) //Global actions can receive an "any" type without issues
                    {
                        AddValidationException($"Expected type '{parameter.Type.FullType}' for parameter '{parameter.Name}' (arg: {parameterIndex + 1}). Received '{argument.Type.FullType}'", argument.TokenLocation, argument.EndTokenLocation);
                    }
                }

                if (!parameter.MultiProp)
                {
                    parameterIndex++;
                }
                else
                {
                    isMulti = true;
                }
            }

            if (parameterIndex < expected && (!isMulti || parameterIndex + 1 < action.Parameters.Count))
            {
                TokenLocation tokenStart = arguments.FirstOrDefault()?.TokenLocation ?? new TokenLocation(_token.Prev);
                TokenLocation tokenEnd = arguments.LastOrDefault()?.EndTokenLocation ?? new TokenLocation(_token);

                AddValidationException($"Invalid argument count. Received {arguments.Count} and expected {expected}", tokenStart, tokenEnd);
            }
        }
    }
}
