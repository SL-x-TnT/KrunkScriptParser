using KrunkScriptParser.Models.Blocks;
using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Expressions
{
    public abstract class ExpressionItem : IKSEndToken
    {
        public const int MaxPriority = 11;
        public abstract bool HasType { get; }
        public KSType Type { get; set; }
        public virtual int Priority { get; set; }
        public TokenLocation TokenLocation { get; set; }
        public TokenLocation EndTokenLocation { get; set; }
    }
}
