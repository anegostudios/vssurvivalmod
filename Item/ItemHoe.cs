using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class ItemHoe : Item
    {
        public override bool OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;
            BlockPos pos = blockSel.Position;

            Block block = byEntity.World.BlockAccessor.GetBlock(pos);

            if (!block.Code.Path.StartsWith("soil")) return false;

            string fertility = block.LastCodePart(1);
            Block farmland = byEntity.World.GetBlock(new AssetLocation("farmland-dry-" + fertility));

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return false;


            if (block.Sounds != null) byEntity.World.PlaySoundAt(block.Sounds.Place, pos.X, pos.Y, pos.Z, byPlayer);

            byEntity.World.BlockAccessor.SetBlock(farmland.BlockId, pos);
            byEntity.World.BlockAccessor.MarkBlockDirty(pos);
            itemslot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot);

            if (byEntity.World is IServerWorldAccessor)
            {
                BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(pos);
                if (be is BlockEntityFarmland)
                {
                    ((BlockEntityFarmland)be).CreatedFromSoil(block);
                }
            }

            return true;
        }

    }
}
