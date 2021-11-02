﻿using KrunkScriptParser.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSAction : IKSValue
    {
        public string Name { get; set; }
        public KSType Type { get; set; } = KSType.Void;
        public bool Global { get; set; } //Global actions can accept any parameter type
        public List<KSParameter> Parameters { get; set; } = new List<KSParameter>();
        public KSBlock Block { get; set; }
        public bool HasAReturn { get; set; }
        public bool WasCalled { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public IEnumerable<KSStatement> GetInvalidReturns()
        {
            return Block.GetReturnStatements().Where(x => x.Type != Type);
        }
    }
}
