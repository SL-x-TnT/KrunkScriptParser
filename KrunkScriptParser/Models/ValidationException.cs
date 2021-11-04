using KrunkScriptParser.Helpers;
using KrunkScriptParser.Models.Tokens;
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
        public int LineStart { get; private set; }
        public int LineEnd { get; private set; }

        public int ColumnStart { get; private set; }
        public int ColumnEnd { get; private set; }

        public Level Level { get; private set; }

        public ValidationException(string text, TokenLocation tokenStart, TokenLocation tokenEnd, Level level = Level.Error) : base(text)
        {
            LineStart = tokenStart.Line;
            LineEnd = tokenEnd.Line;

            ColumnStart = tokenStart.Column;
            ColumnEnd = tokenEnd.ColumnEnd;

            Level = level;
        }

        public override string ToString()
        {
            return $"[{Level}] ({LineStart}:{ColumnStart}) {Message}";
        }
    }
}
