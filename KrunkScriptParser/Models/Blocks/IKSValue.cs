using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public interface IKSValue
    {
        public KSType Type { get; set; }
        public TokenLocation TokenLocation { get; set; }
    }

    public interface IKSEndToken
    {
        public TokenLocation EndTokenLocation { get; }
    }
}
