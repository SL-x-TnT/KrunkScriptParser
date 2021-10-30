using KrunkScriptParser.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSAction
    {
        public string Text { get; set; }
        public KSType ReturnType { get; set; }
        public KSParameter[] Parameters { get; set; }
        public bool IsAction { get; set; }
        public int TotalOptionalParams { get; set; }
    }
}
