using KrunkScriptParser.Helpers;
using KrunkScriptParser.Models;
using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KrunkScriptParser
{
    public class KrunkScriptValidator
    {
        public KrunkScriptValidator()
        {
        }

        public KSInfo Validate(string text)
        {
            TokenReader reader = new TokenReader(text);

            List<Token> tokens = new List<Token>();
            Token prevToken = null;

            Token currentToken;

            do
            {
                currentToken = reader.ReadToken();

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

            KSInfo info = new KSInfo();

            info.ParseTokens(tokens.FirstOrDefault());

            return info;
        }
    }
}
