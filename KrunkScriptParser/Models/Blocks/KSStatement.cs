using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSStatement : IKSValue, IKSEndToken
    {
        public string Statement { get; set; }
        public IKSValue Value { get; set; }
        public TokenLocation TokenLocation { get; set; }
        public TokenLocation EndTokenLocation
        {
            get
            {
                if(Value is IKSEndToken endToken)
                {
                    return endToken.EndTokenLocation;
                }

                return Value.TokenLocation;
            }
        }

        public KSType Type
        {
            get
            {
                return Value?.Type ?? KSType.Void;
            }
            set { }
        }

        public bool IsReturn => Statement == "return";
        public bool IsLoopStatment => Statement == "continue" || Statement == "break";
    }
}
