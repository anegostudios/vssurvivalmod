
#nullable disable
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
