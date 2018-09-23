using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemDryGrass : Item
    {
        public override void OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.Sneak) return;

            IWorldAccessor world = byEntity.World;
            Block firepitBlock = world.GetBlock(new AssetLocation("firepit-construct1"));
            if (firepitBlock == null) return;

            IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            if (!byEntity.World.TestPlayerAccessBlock(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            BlockPos onPos = blockSel.DidOffset ? blockSel.Position : blockSel.Position.AddCopy(blockSel.Face);

            Block block = world.BlockAccessor.GetBlock(onPos.DownCopy());
            Block atBlock = world.BlockAccessor.GetBlock(onPos);

            if (!block.CanAttachBlockAt(byEntity.World.BlockAccessor, firepitBlock, onPos.DownCopy(), BlockFacing.UP)) return;
            if (!firepitBlock.IsSuitablePosition(world, onPos)) return;

            world.BlockAccessor.SetBlock(firepitBlock.BlockId, onPos);

            if (firepitBlock.Sounds != null) world.PlaySoundAt(firepitBlock.Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);

            itemslot.Itemstack.StackSize--;
            handHandling = EnumHandHandling.PreventDefaultAction;
        }
    }
}
