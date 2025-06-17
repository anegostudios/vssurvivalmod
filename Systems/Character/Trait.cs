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
    }

    public class CharacterClass
    {
        public string Code;
        public string[] Traits;
        public JsonItemStack[] Gear;
    }

}
