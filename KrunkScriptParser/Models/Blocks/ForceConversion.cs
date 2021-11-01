using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    class ForceConversion : IExpressionItem
    {
        public bool IsConvert { get; set; }
        public KSType ReturnType { get; set; }

        public override int Priority => IExpressionItem.MaxPriority - 1;
        public override bool HasType => true;

        public ForceConversion(KSType type, bool isConvert, KSType returnType = null)
        {
            Type = type;
            IsConvert = isConvert;
            ReturnType = returnType ?? type;
        }

        public bool IsValid(KSType otherType)
        {
            if (Type == KSType.LengthOf)
            {
                if(otherType.IsArray || otherType == KSType.String || otherType == KSType.Any)
                {
                    return true;
                }

                return false;
            }
            else if (Type == KSType.NotEmpty)
            {
                if(otherType == KSType.Any || otherType == KSType.Object)
                {
                    return true;
                }

                return false;
            }

            if (!IsConvert && otherType != KSType.Any) //Was a cast, verify there's no type changes
            {
                if (otherType != Type)
                {
                    return false;
                }

                return true;
            }

            return true;
        }
    }
}
