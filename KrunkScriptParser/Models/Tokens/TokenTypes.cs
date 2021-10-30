﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Tokens
{
    [Flags]
    public enum TokenTypes
    { 
        Unknown = 0,
        Comment = 1,
        Type = 1 << 1,
        String = 1 << 2,        
        Number = 1 << 3,
        Action = 1 << 4,
        Terminator = 1 << 5,
        Operator = 1 << 6,
        NewLine = 1 << 7,
        Name = 1 << 8,
        Punctuation = 1 << 9,
        Assign = 1 << 10,
        Keyword = 1 << 11,
        KeyMethod = 1 << 12,
        Modifier = 1 << 13,
        Bool = 1 << 14
    };
}
