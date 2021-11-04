using KrunkScriptParser.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models
{
    public enum Level
    {
        Error = 1,
        Warning,
        Info,
        Hint
    };

    public class ValidationException : Exception
    {
        public int LineNumber { get; private set; }
        public int ColumnNumber { get; private set; }
        public TokenLocation TokenStart { get; private set; }
        public Level Level { get; private set; }

        public ValidationException(string text, int lineNumber, int columnNumber, TokenLocation tokenStart, Level level = Level.Error) : base(text)
        {
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            Level = level;
            TokenStart = tokenStart;
        }

        public override string ToString()
        {
            return $"[{Level}] ({LineNumber}:{ColumnNumber}) {Message}";
        }
    }

    public class TokenLocation
    {
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
