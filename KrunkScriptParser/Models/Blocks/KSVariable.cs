using KrunkScriptParser.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSVariable : IKSValue
    {
        public KSType Type { get; set; }

        public string Name { get;  set; }
        public IKSValue Value { get; set; }

        public KSVariable()
        {
        }
    }
}
