using KrunkScriptParser.Helpers;
using KrunkScriptParser.Models.Tokens;
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
        public TokenLocation TokenLocation { get; set; }

        public KSVariable()
        {
        }

        public bool TryReadObject(out KSObject ksObject)
        {
            ksObject = null;

            if(Value is KSObject tObj)
            {
                ksObject = tObj;

                return true;
            }

            if(Value is KSExpression ksExpression && ksExpression.TryReadObject(out ksObject))
            {
                return true;
            }

            return false;
        }

        public object Clone()
        {
            return new KSVariable
            {
                Type = new KSType(Type),
                Name = Name,
                Value = Value,
                WasCalled = WasCalled,
                TokenLocation = TokenLocation
            };
        }
    }
}
