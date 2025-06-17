﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemCandle: Item
    {

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.ShiftKey) return;

            IWorldAccessor world = byEntity.World;

            BlockPos offsetedPos = blockSel.Position.AddCopy(blockSel.Face);
            BlockPos belowPos = offsetedPos.DownCopy();

            Block targetedBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            Block nextblock;


            AssetLocation loc = new AssetLocation(this.Attributes["blockfirstcodepart"].AsString());
            string firstcodepart = loc.Path;

            IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                slot.MarkDirty();
                return;
            }

            if (targetedBlock.FirstCodePart() == firstcodepart)
            {
                int.TryParse(targetedBlock.LastCodePart(), out int stage);
                if (stage == 9) return;

                nextblock = world.GetBlock(targetedBlock.CodeWithPart("" + (stage + 1), 1));

                world.BlockAccessor.SetBlock(nextblock.BlockId, blockSel.Position);
            }
            else
            {
                if (world.BlockAccessor.GetBlock(belowPos) is BlockFence)
                {
                    nextblock = world.GetBlock(new AssetLocation("candle"));  
                }
                else
                {
                    nextblock = byEntity.World.GetBlock(loc.WithPathAppendix("-1"));
                    if (nextblock == null) return;
                }
                
                Block blockAtTargetPos = world.BlockAccessor.GetBlock(offsetedPos);
                if (!blockAtTargetPos.IsReplacableBy(nextblock)) return;
                if (!world.BlockAccessor.GetBlock(belowPos).CanAttachBlockAt(world.BlockAccessor, nextblock, belowPos, BlockFacing.UP, new Cuboidi(1, 14, 1, 14, 15, 14))) return;

                world.BlockAccessor.SetBlock(nextblock.BlockId, offsetedPos);
            }

            if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
            }
            slot.MarkDirty();

            if (nextblock.Sounds != null)
            {
                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
                world.PlaySoundAt(nextblock.Sounds.Place, blockSel.Position, -0.4, byPlayer);
            }

            handHandling = EnumHandHandling.PreventDefault;
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    HotKeyCode = "shift",
                    ActionLangCode = "heldhelp-place",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
