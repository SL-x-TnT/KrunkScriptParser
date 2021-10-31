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
        /// <summary>
        /// Parses out the arguments sent to an action
        /// </summary>
        private List<IKSValue> ParseArguments()
        {
            List<IKSValue> values = new List<IKSValue>();

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
                (isGlobalMethod && _token.Value == "any")
                )
            {
                KSType type = null;

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
                            AddValidationException(new ValidationException($"Spread notation '...' not supported", _token.Line, _token.Column));
                        }
                    }
                    else
                    {
                        AddValidationException(new ValidationException($"Unexpected value '{multiProp}'", _token.Line, _token.Column));
                    }
                }

                if (_token.Type != TokenTypes.Name)
                {
                    //Getting too tired to save invalid code
                    throw new ValidationException($"Expected parameter name. Received '{_token.Value}'", _token.Line, _token.Column);
                }

                parameters.Add(new KSParameter
                {
                    Name = _token.Value,
                    Type = type,
                    MultiProp = isMultiProp,
                    Optional = optional
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
                AddValidationException(new ValidationException($"Expected ')'. Received '{_token.Value}'", _token.Line, _token.Column));

                _iterator.SkipUntil(new HashSet<string> { ")", ";" });
            }

            return parameters;
        }

        /// <summary>
        /// Validates that the arguments provided for an action are accurate
        /// </summary>
        private void ValidateArguments(List<IKSValue> arguments, KSAction action)
        {
            int parameterIndex = 0;
            int expected = action.Parameters.Count(x => !x.Optional);

            foreach (KSExpression argument in arguments)
            {
                if (parameterIndex >= action.Parameters.Count)
                {
                    AddValidationException(new ValidationException($"Invalid argument count. Received {arguments.Count} and expected {action.Parameters.Count}", _token.Line, _token.Column));

                    return;
                }

                KSParameter parameter = action.Parameters[parameterIndex];

                if (parameter.Type.FullType != argument.CurrentType.FullType && parameter.Type != KSType.Any)
                {
                    Level level = Level.Error;

                    if (action.Global)
                    {
                        level = Level.Warning;
                    }

                    AddValidationException(new ValidationException($"Expected type '{parameter.Type.FullType}' for parameter '{parameter.Type.FullType}'. Received '{argument.CurrentType.FullType}'", _token.Line, _token.Column, level: level));
                }

                if (!parameter.MultiProp)
                {
                    parameterIndex++;
                }
            }

            if (parameterIndex < expected)
            {
                AddValidationException(new ValidationException($"Invalid argument count. Received {arguments.Count} and expected {expected}", _token.Line, _token.Column));
            }
        }
    }
}
