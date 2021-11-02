using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSType
    {
        public string Name { get; set; }
        public bool IsArray => ArrayDepth > 0;
        public int ArrayDepth { get; private set; }

        public string FullType
        {
            get
            {
                if (string.IsNullOrEmpty(_fullType))
                {
                    _fullType = $"{Name}";

                    for (int i = 0; i < ArrayDepth; i++)
                    {
                        _fullType += "[]";
                    }
                }

                return _fullType;
            }
        }

        public KSType()
        {
        }

        public KSType(KSType type)
        {
            if (type == null)
            {
                Name = KSType.Any.Name;
            }
            else
            {
                Name = type.Name;
                ArrayDepth = type.ArrayDepth;
            }
        }

        public void IncreaseDepth()
        {
            ArrayDepth++;

            _fullType = string.Empty;
        }

        public void DecreaseDepth()
        {
            ArrayDepth--;

            _fullType = string.Empty;
        }

        public override string ToString()
        {
            return FullType;
        }

        public static bool operator ==(KSType type1, KSType type2)
        {
            return type1?.FullType == type2?.FullType;
        }

        public static bool operator !=(KSType type1, KSType type2)
        {
            return type1?.FullType != type2?.FullType;
        }


        public override bool Equals(object obj)
        {
            if (obj is KSType type)
            {
                return type.FullType == FullType;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return FullType.GetHashCode();
        }

        private string _fullType = string.Empty;



        internal static readonly KSType String = new KSType { Name = "str" };
        internal static readonly KSType Bool = new KSType { Name = "bool" };
        internal static readonly KSType Number = new KSType { Name = "num" };
        internal static readonly KSType Object = new KSType { Name = "obj" };

        //Internal types
        internal static readonly KSType Void = new KSType { Name = "void" };
        internal static readonly KSType Any = new KSType { Name = "any" };
        internal static readonly KSType Action = new KSType { Name = "action" };
        internal static readonly KSType LengthOf = new KSType { Name = "lengthOf" };
        internal static readonly KSType NotEmpty = new KSType { Name = "notEmpty" };
        internal static readonly KSType Unknown = new KSType { Name = "unknown" };
    }
}
