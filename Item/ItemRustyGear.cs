using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemRustyGear : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (blockSel == null || !byEntity.Controls.ShiftKey)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            handling = EnumHandHandling.PreventDefault;

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (block is BlockLooseGears)
            {
                int q = 5;
                if (int.TryParse(block.LastCodePart(), out q) && q < 5)
                {
                    Block moregearsblock = byEntity.World.GetBlock(block.CodeWithPart((q + 1) + "", 1));
                    byEntity.World.BlockAccessor.SetBlock(moregearsblock.BlockId, blockSel.Position);
                    byEntity.World.PlaySoundAt(block.Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    slot.TakeOut(1);
                }

                
                return;
            }


            BlockPos placePos = blockSel.Position.AddCopy(blockSel.Face);
            if (!byEntity.World.Claims.TryAccess((byEntity as EntityPlayer)?.Player, placePos, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            block = byEntity.World.BlockAccessor.GetBlock(placePos);
            Block gearBlock = byEntity.World.GetBlock(new AssetLocation("loosegears-1"));
            placePos.Y--;
            if (block.IsReplacableBy(gearBlock) && byEntity.World.BlockAccessor.GetMostSolidBlock(placePos.X, placePos.Y, placePos.Z).CanAttachBlockAt(byEntity.World.BlockAccessor, gearBlock, placePos, BlockFacing.UP))
            {
                placePos.Y++;
                byEntity.World.BlockAccessor.SetBlock(gearBlock.BlockId, placePos);
                slot.TakeOut(1);
                byEntity.World.PlaySoundAt(block.Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
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
