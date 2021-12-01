using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public static class FoliageUtil
    {
        public static string[] FoliageStates = new string[] { "", "plain", "flowering", "fruiting", "ripe", "dead" };
    }

    public enum EnumFoliageState
    {
        DormantNoLeaves,
        Plain,
        Flowering,
        Fruiting,
        Ripe,
        Dead
    }
    public enum EnumTreePartType
    {
        Stem,
        Branch,
        Cutting,
        Leaves
    }

    public enum EnumFruitTreeState
    {
        /// <summary>
        /// Still a young tree, doesn't fruit
        /// </summary>
        Young,
        Flowering,
        Fruiting,
        Ripe,
        Empty,
        EnterDormancy,
        Dormant,
        DormantVernalized,
        Dead
    }
}
