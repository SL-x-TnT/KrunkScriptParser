﻿using KrunkScriptParser.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models
{
    public enum Level { Info, Warning, Error };

    public class ValidationException : Exception
    {
        public int LineNumber { get; private set; }
        public int ColumnNumber { get; private set; }
        public bool CanContinue { get; private set; }

        public ValidationException(string text, int lineNumber, int columnNumber, bool canContinue = false, Level level = Level.Error) : base(text)
        {
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            CanContinue = canContinue;
        }
    }
}
