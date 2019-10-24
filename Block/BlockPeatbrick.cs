using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockPeatbrick : Block
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            return false;
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            handHandling = EnumHandHandling.PreventDefault;

            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.Sneak) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                itemslot.MarkDirty();
                return;
            }

            BlockPeatPile block = byEntity.World.GetBlock(new AssetLocation("peatpile")) as BlockPeatPile;
            if (block == null) return;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityPeatPile)
            {
                BlockEntityPeatPile pile = (BlockEntityPeatPile)be;
                pile.OnPlayerInteract(byPlayer);
                return;
            }

            if (be is BlockEntityAnvil)
            {
                return;
            }

            BlockPos pos = blockSel.Position.AddCopy(blockSel.Face);
            if (byEntity.World.BlockAccessor.GetBlock(pos).Replaceable < 6000) return;

            be = byEntity.World.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityPeatPile)
            {
                BlockEntityPeatPile pile = (BlockEntityPeatPile)be;
                pile.OnPlayerInteract(byPlayer);
                return;
            }


            block.Construct(itemslot, byEntity.World, blockSel.Position.AddCopy(blockSel.Face), byPlayer);
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    HotKeyCode = "sneak",
                    ActionLangCode = "heldhelp-place",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }

    }
}
