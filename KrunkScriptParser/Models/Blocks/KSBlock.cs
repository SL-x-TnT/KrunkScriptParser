using KrunkScriptParser.Models.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSBlock : IKSValue
    {
        public string Keyword { get; set; }
        public List<IKSValue> Lines { get; set; } = new List<IKSValue>();
        public TokenLocation TokenLocation { get; set; }
        public TokenLocation TokenLocationEnd => Lines.LastOrDefault()?.TokenLocation;

        //Used for auto completions
        internal Dictionary<string, IKSValue> Declarations { get; set; } = new Dictionary<string, IKSValue>();
        internal KSBlock ParentBlock { get; set; }

        public KSType Type { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IEnumerable<KSStatement> GetReturnStatements()
        {
            foreach(IKSValue value in Lines)
            {
                if(value is KSStatement statement && statement.IsReturn)
                {
                    yield return statement;
                }
                else if (value is KSBlock block)
                {
                    foreach(KSStatement innerStatement in block.GetReturnStatements())
                    {
                        yield return innerStatement;
                    }
                }
            }
        }
    }
}
