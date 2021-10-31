using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptParser.Models.Blocks
{
    public class KSType : IBlock
    {
        public string Name { get; set; }
        public bool IsArray { get; set; }
        public int ArrayDepth { get; set; }

        public string FullType
        {
            get
            {
                if (string.IsNullOrEmpty(_fullType))
                {
                    _fullType = $"{Name}";

                    if (IsArray)
                    {
                        for (int i = 0; i < ArrayDepth; i++)
                        {
                            _fullType += "[]";
                        }
                    }
                }

                return _fullType;
            }
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

        public static readonly KSType String = new KSType { Name = "str" }; 
        public static readonly KSType Bool = new KSType { Name = "bool" }; 
        public static readonly KSType Number = new KSType { Name = "num" }; 
        public static readonly KSType Object = new KSType { Name = "obj" }; 

        //Internal types
        public static readonly KSType Void = new KSType { Name = "void" }; 
        public static readonly KSType Any = new KSType { Name = "any" };
        public static readonly KSType Action = new KSType { Name = "action" };
    }
}
