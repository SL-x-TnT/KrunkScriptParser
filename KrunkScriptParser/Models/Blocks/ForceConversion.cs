using KrunkScriptParser.Models.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    class ForceConversion : ExpressionItem
    {
        public bool IsConvert { get; set; }
        public KSType ReturnType { get; set; }
        public bool ValidLeftHand { get; set; }
        public bool IsTypeCast { get; set; }
        public bool IsMinus { get; set; }

        public override int Priority => ExpressionItem.MaxPriority - 1;
        public override bool HasType => true;

        public ForceConversion(KSType type, bool isConvert, KSType returnType = null, bool validLeftHand = false, bool isTypeCast = false, bool isMinus = false)
        {
            Type = type;
            IsConvert = isConvert;
            ReturnType = returnType ?? type;
            ValidLeftHand = validLeftHand;
            IsTypeCast = isTypeCast;
            IsMinus = isMinus;
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
            else if(IsConvert)
            {
                //Converts don't work on arrays
                return !otherType.IsArray && otherType == KSType.Any || otherType == KSType.Bool || otherType == KSType.Number || otherType == KSType.String || otherType == KSType.Object;
            }

            if(IsMinus && otherType == KSType.Any)
            {
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
