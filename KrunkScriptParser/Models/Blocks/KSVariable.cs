using KrunkScriptParser.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSVariable : IKSValue, ICloneable
    {
        public KSType Type { get; set; }
        public string Name { get;  set; }
        public IKSValue Value { get; set; }
        public bool WasCalled { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public KSVariable()
        {
        }

        public object Clone()
        {
            return new KSVariable
            {
                Type = new KSType(Type),
                Name = Name,
                Value = Value,
                WasCalled = WasCalled,
                Line = Line,
                Column = Column
            };
        }
    }
}
