
using Vintagestory.GameContent;

namespace Vintagestory.GameContent
{
    public class ConstantPerishRateContainer : InWorldContainer
    {
        public float PerishRate = 0;

        public ConstantPerishRateContainer(InventorySupplierDelegate inventorySupplier, string treeAttrKey) : base(inventorySupplier, treeAttrKey)
        {
        }

        public override float GetPerishRate()
        {
            return PerishRate;
        }
    }
}
