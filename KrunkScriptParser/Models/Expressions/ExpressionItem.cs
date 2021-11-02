using KrunkScriptParser.Models.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Expressions
{
    public abstract class ExpressionItem
    {
        public const int MaxPriority = 10;
        public abstract bool HasType { get; }
        public KSType Type { get; set; }
        public virtual int Priority { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
