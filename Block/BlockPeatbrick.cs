using Vintagestory.API.Client;
using Vintagestory.API.Common;
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

            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.ShiftKey)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) 
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }
            

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                itemslot.MarkDirty();
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            BlockPeatPile block = byEntity.World.GetBlock(new AssetLocation("peatpile")) as BlockPeatPile;
            if (block == null)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityPeatPile)
            {
                BlockEntityPeatPile pile = (BlockEntityPeatPile)be;
                pile.OnPlayerInteract(byPlayer);
                return;
            }
            if (be is BlockEntityFirepit)
            {
                handHandling = EnumHandHandling.NotHandled;
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


            if (!block.Construct(itemslot, byEntity.World, blockSel.Position.AddCopy(blockSel.Face), byPlayer))
            {
                handHandling = EnumHandHandling.NotHandled;
            }
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    HotKeyCode = "shift",
                    ActionLangCode = "heldhelp-place",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }

    }
}
