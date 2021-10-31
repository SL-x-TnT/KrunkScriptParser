using KrunkScriptParser.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSAction : IKSValue
    {
        public string Text { get; set; }
        public KSType Type { get; set; }
        public bool Global { get; set; } //Global actions can accept any parameter type
        public List<KSParameter> Parameters { get; set; } = new List<KSParameter>();
    }
}
