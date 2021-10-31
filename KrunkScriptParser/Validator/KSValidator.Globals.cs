using KrunkScriptParser.Helpers;
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
                string text = File.ReadAllText("globalObjects.krnk");

                ParseGlobalObjects(text);
            }
            catch (ValidationException ex)
            {
                AddValidationException(ex);
                AddValidationException($"Failed to parse 'globalObjects.krnk' file. Additional errors may occur", level: Level.Warning);
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
                        Text = name
                    });

                    _iterator.Next(false);
                }
                else //Property
                {
                    _krunkerGlobalVariables.TryAdd(name, new KSVariable
                    {
                        Name = name,
                        Type = returnType
                    });
                }
            }
        }
    }
}
