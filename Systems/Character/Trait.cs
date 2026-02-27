using System.Collections.Generic;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumTraitType
    {
        Positive,
        Mixed,
        Negative
    }

    public class Trait
    {
        public string Code;
        public EnumTraitType Type;
        public Dictionary<string, double> Attributes;

        /// <summary>
        /// Attribute code to the desired blend-type.
        /// </summary>
        public Dictionary<string, EnumStatBlendType> AttributeBlendTypes;
    }

    public class CharacterClass
    {
        public bool Enabled = true;
        public string Code;
        public string[] Traits;
        public JsonItemStack[] Gear;
    }
}
