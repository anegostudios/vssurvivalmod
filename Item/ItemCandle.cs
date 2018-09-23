using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemCandle: Item
    {

        public override void OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.Sneak) return;

            IWorldAccessor world = byEntity.World;

            BlockPos offsetedPos = blockSel.Position.AddCopy(blockSel.Face);
            BlockPos belowPos = offsetedPos.DownCopy();

            Block targetedBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            Block nextblock;


            AssetLocation loc = new AssetLocation(this.Attributes["blockfirstcodepart"].AsString());
            string firstcodepart = loc.Path;

            IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            if (!byEntity.World.TestPlayerAccessBlock(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                slot.MarkDirty();
                return;
            }

            if (targetedBlock.FirstCodePart() == firstcodepart)
            {
                int stage = 1;
                int.TryParse(targetedBlock.LastCodePart(), out stage);
                if (stage == 9) return;

                nextblock = world.GetBlock(targetedBlock.CodeWithPart("" + (stage + 1), 1));

                world.BlockAccessor.SetBlock(nextblock.BlockId, blockSel.Position);
            }
            else
            {
                nextblock = byEntity.World.GetBlock(loc.WithPathAppendix("-1"));
                if (nextblock == null) return;

                Block blockAtTargetPos = world.BlockAccessor.GetBlock(offsetedPos);
                if (!blockAtTargetPos.IsReplacableBy(nextblock)) return;
                if (!world.BlockAccessor.GetBlock(belowPos).SideSolid[BlockFacing.UP.Index]) return;

                world.BlockAccessor.SetBlock(nextblock.BlockId, offsetedPos);
            }

            slot.TakeOut(1);
            slot.MarkDirty();

            if (nextblock.Sounds != null)
            {
                IPlayer byPlayer = null;
                if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
                world.PlaySoundAt(nextblock.Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
            }

            handHandling = EnumHandHandling.PreventDefault;
        }

    }
}
