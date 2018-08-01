using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemIngot : Item
    {
        public override bool OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.Sneak) return false;

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return false;

            if (!byEntity.World.TestPlayerAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                itemslot.MarkDirty();
                return false;
            }

            BlockIngotPile block = byEntity.World.GetBlock(new AssetLocation("ingotpile")) as BlockIngotPile;
            if (block == null) return false;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityIngotPile)
            {
                BlockEntityIngotPile pile = (BlockEntityIngotPile)be;
                return pile.OnPlayerInteract(byPlayer);
            }

            BlockPos pos = blockSel.Position.AddCopy(blockSel.Face);
            if (byEntity.World.BlockAccessor.GetBlock(pos).Replaceable < 6000) return false;

            be = byEntity.World.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityIngotPile)
            {
                BlockEntityIngotPile pile = (BlockEntityIngotPile)be;
                return pile.OnPlayerInteract(byPlayer);
            }


            return block.Construct(itemslot, byEntity.World, blockSel.Position.AddCopy(blockSel.Face), byPlayer);
        }


        

        public string GetMetalType()
        {
            return LastCodePart();
        }
    }
}
