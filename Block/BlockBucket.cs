using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBucket : Block
    {
        public bool IsFull()
        {
            return LastCodePart() == "filled";
        }

        public override bool OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return true;
            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);

            Block targetedBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (targetedBlock.HasBehavior(typeof(BlockBehaviorLiquidContainer), true))
            {
                if (!byEntity.World.TestPlayerAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
                {
                    return false;
                }

                BlockBehaviorLiquidContainer bh = targetedBlock.GetBehavior(typeof(BlockBehaviorLiquidContainer), true) as BlockBehaviorLiquidContainer;

                if (bh.OnInteractWithBucket(itemslot, byEntity, blockSel)) return true;
            }

            BlockPos pos = blockSel.Position;


            if (!byEntity.World.TestPlayerAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return false;
            }

            IBlockAccessor blockAcc = byEntity.World.BlockAccessor;

            if (IsFull())
            {
                BlockPos secondPos = blockSel.Position.AddCopy(blockSel.Face);

                Block emptyBucketBlock = byEntity.World.GetBlock(CodeWithParts("empty"));
                Block waterBlock = byEntity.World.GetBlock(new AssetLocation("water-7"));

                if (blockAcc.GetBlock(pos).IsWater())
                {
                    blockAcc.SetBlock(waterBlock.BlockId, pos);
                    blockAcc.MarkBlockDirty(pos);
                } else
                {
                    blockAcc.SetBlock(waterBlock.BlockId, secondPos);
                    blockAcc.MarkBlockDirty(secondPos);
                }


                itemslot.Itemstack = new ItemStack(emptyBucketBlock);
                itemslot.MarkDirty();

                byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/water"), pos.X, pos.Y, pos.Z, byPlayer);
            } else
            {
                if (blockAcc.GetBlock(pos).IsWater())
                {
                    Block fullBucketBlock = byEntity.World.GetBlock(CodeWithParts("filled"));
                    itemslot.Itemstack = new ItemStack(fullBucketBlock);
                    itemslot.MarkDirty();
                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/water"), pos.X, pos.Y, pos.Z, byPlayer);
                }
            }

            // Prevent placing on normal use
            return true;
        }


    }
}
