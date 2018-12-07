using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemIngot : Item
    {
        public override void OnHeldInteractStart(IItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling)
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

            BlockIngotPile block = byEntity.World.GetBlock(new AssetLocation("ingotpile")) as BlockIngotPile;
            if (block == null) return;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityIngotPile)
            {
                BlockEntityIngotPile pile = (BlockEntityIngotPile)be;
                if (pile.OnPlayerInteract(byPlayer))
                {
                    handHandling = EnumHandHandling.PreventDefault;
                }
                return;
            }

            if (be is BlockEntityAnvil)
            {
                return;
            }

            BlockPos pos = blockSel.Position.AddCopy(blockSel.Face);
            if (byEntity.World.BlockAccessor.GetBlock(pos).Replaceable < 6000) return;

            be = byEntity.World.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityIngotPile)
            {
                BlockEntityIngotPile pile = (BlockEntityIngotPile)be;
                if (pile.OnPlayerInteract(byPlayer))
                {
                    handHandling = EnumHandHandling.PreventDefault;
                }
                return;
            }


            if (block.Construct(itemslot, byEntity.World, blockSel.Position.AddCopy(blockSel.Face), byPlayer))
            {
                handHandling = EnumHandHandling.PreventDefault;
            }
            
        }


        

        public string GetMetalType()
        {
            return LastCodePart();
        }
    }
}
