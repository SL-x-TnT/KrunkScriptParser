using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class BlockTypes
    {
        private static readonly HashSet<string> Types = new HashSet<string>
        {
            "obj", "num", "str", "obj[]", "num[]", "str[]"
        };

        private static readonly HashSet<string> Actions = new HashSet<string>
        {
            "public", "action"
        };

        public static bool IsType(string text)
        {
            return Types.Contains(text);
        }

        public static bool IsAction(string text)
        {
            return Actions.Contains(text);
        }
    }
}
