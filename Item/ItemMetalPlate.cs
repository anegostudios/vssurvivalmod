using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemMetalPlate : Item
    {
        public override void OnHeldInteractStart(IItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.Sneak) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            if (!byEntity.World.TryAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                itemslot.MarkDirty();
                return;
            }


            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityPlatePile)
            {
                BlockEntityPlatePile pile = (BlockEntityPlatePile)be;
                if (pile.OnPlayerInteract(byPlayer))
                {
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(blockSel.Face));
            if (be is BlockEntityPlatePile)
            {
                BlockEntityPlatePile pile = (BlockEntityPlatePile)be;
                if (pile.OnPlayerInteract(byPlayer))
                {
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            Block block = byEntity.World.GetBlock(new AssetLocation("platepile"));
            if (block == null) return;

            if (((BlockPlatePile)block).Construct(itemslot, byEntity.World, blockSel.Position.AddCopy(blockSel.Face), byPlayer))
            {
                handling = EnumHandHandling.PreventDefault;
            }
        }




        public string GetMetalType()
        {
            return LastCodePart();
        }
    }
}
