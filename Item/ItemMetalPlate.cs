using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemMetalPlate : Item
    {
        public override bool OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.Sneak) return false;

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return false;


            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityPlatePile)
            {
                BlockEntityPlatePile pile = (BlockEntityPlatePile)be;
                if (pile.OnPlayerInteract(byPlayer)) return true;
            }

            be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(blockSel.Face));
            if (be is BlockEntityPlatePile)
            {
                BlockEntityPlatePile pile = (BlockEntityPlatePile)be;
                if (pile.OnPlayerInteract(byPlayer)) return true;
            }

            Block block = byEntity.World.GetBlock(new AssetLocation("platepile"));
            if (block == null) return false;

            return ((BlockPlatePile)block).Construct(itemslot, byEntity.World, blockSel.Position.AddCopy(blockSel.Face), byPlayer);
        }




        public string GetMetalType()
        {
            return LastCodePart();
        }
    }
}
