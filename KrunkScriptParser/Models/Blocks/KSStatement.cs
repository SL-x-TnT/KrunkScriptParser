using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSStatement : IKSValue
    {
        public string Statement { get; set; }
        public IKSValue Value { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

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
