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
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
