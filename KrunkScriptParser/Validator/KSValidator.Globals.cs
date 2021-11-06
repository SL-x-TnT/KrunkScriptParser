using KrunkScriptParser.Helpers;
using KrunkScriptParser.Models;
using KrunkScriptParser.Models.Blocks;
using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Validator
{
    public partial class KSValidator
    {
        /// <summary>
        /// Initializes global methods/objects from file
        /// </summary>
        private void InitializeGlobals()
        {
            if (_krunkerGlobalVariables.Count > 0)
            {
                return;
            }

            //Read file + parse file
            try
            {
                string text = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "globalObjects.krnk"));

                ParseGlobalObjects(text);
            }
            catch (ValidationException ex)
            {
                AddValidationException(ex);
                AddValidationException($"Failed to parse 'globalObjects.krnk' file. Additional errors may occur", _token, level: Level.Warning);
            }
        }

        /// <summary>
        /// Parses global methods/objects file text
        /// </summary>
        private void ParseGlobalObjects(string text)
        {
            TokenReader reader = new TokenReader(text);
            _iterator = new TokenIterator(reader.ReadAllTokens());

            while (_iterator.PeekNext() != null)
            {
                KSType returnType = KSType.Void;

                if (_token.Type == TokenTypes.Type)
                {
                    returnType = ParseType();
                }

                string name = String.Empty;
                bool globalFinished = false;

                while ((_token.Type != TokenTypes.Type && _token.Value != "("))
                {
                    if (!String.IsNullOrEmpty(name) && _token.Type == TokenTypes.GlobalObject && globalFinished)
                    {
                        break;
                    }

                    name += _token.Value;

                    _iterator.Next();

                    if (_token.Type == TokenTypes.Name)
                    {
                        globalFinished = true;
                    }
                }

                //Action
                if (_token.Value == "(")
                {
                    _iterator.Next();

                    List<KSParameter> parameters = ParseParameters(true);

                    _krunkerGlobalVariables.TryAdd(name, new KSAction
                    {
                        Type = returnType,
                        Parameters = parameters,
                        Name = name,
                        Global = true
                    });

                    _iterator.Next(false);
                }
                else //Property
                {
                    KSVariable variable = new KSVariable
                    {
                        Name = name,
                        Type = returnType
                    };

                    UpdateGlobalDeclaration(variable);
                    _krunkerGlobalVariables.TryAdd(name, variable);
                }
            }
        }

        //Too lazy to get the other method working with globals
        private void UpdateGlobalDeclaration(IKSValue value)
        {
            if(value is KSVariable variable)
            {
                string[] parts = variable.Name.Split('.');

                KSObject ksObject = new KSObject();

                //First value
                if(!_defaultDeclarations.TryGetValue(parts[0], out IKSValue declaredValue))
                {
                    declaredValue = new KSVariable
                    {
                        Type = KSType.Object,
                        Value = ksObject,
                        Name = parts[0]
                    };

                    _defaultDeclarations.TryAdd(parts[0], declaredValue);
                }

                ksObject = ((KSVariable)declaredValue).Value as KSObject;
                //Remaining
                for (int i = 1; i < parts.Length - 1; i++)
                {
                    if (!ksObject.Properties.TryGetValue(parts[i], out IKSValue v))
                    {
                        KSObject newObj = new KSObject
                        {
                            Type = KSType.Object
                        };

                        ksObject.Properties.TryAdd(parts[i], newObj);
                        ksObject = newObj;
                    }
                    else
                    {
                        ksObject = v as KSObject;
                    }

                }

                ksObject.Properties.TryAdd(parts[parts.Length - 1], new KSVariable
                {
                    Type = value.Type
                });
            }
            else if (value is KSAction action)
            {

            }
        }


        private List<AutoCompleteSuggestion> GlobalSuggestions(string[] parts)
        {
            return new List<AutoCompleteSuggestion>();
        }

    }
}
