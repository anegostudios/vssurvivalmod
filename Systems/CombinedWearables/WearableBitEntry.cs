using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;


namespace Vintagestory.GameContent
{
    /// <summary>
    /// Struct contrining information about wearable bit inside combined wearable
    /// </summary>
    public struct WearableBitEntry
    {
        public ItemStack Stack;
        public string SlotCode;
        public string LayerCode;
        public float HitChance;
        public int LayerIndex;

        public WearableBitEntry(ITreeAttribute slotTree, IWorldAccessor world)
        {
            Stack = slotTree.GetItemstack("stack");
            SlotCode = slotTree.GetString("code");
            LayerCode = slotTree.GetString("layer");
            LayerIndex = slotTree.GetInt("layerIndex");
            HitChance = slotTree.GetFloat("hitchance");

            Stack.ResolveBlockOrItem(world);
        }

        public readonly void Store(ITreeAttribute slotTree)
        {
            slotTree.SetItemstack("stack", Stack);
            slotTree.SetString("code", SlotCode);
            slotTree.SetString("layer", LayerCode);
            slotTree.SetInt("layerIndex", LayerIndex);
            slotTree.SetFloat("hitchance", HitChance);
        }
    }
}
