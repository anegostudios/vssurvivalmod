using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockCrystal : Block
    {
        private Block[] _facingBlocks;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            _facingBlocks = new Block[6];
            for (int i = 0; i < 6; i++)
            {
                _facingBlocks[i] = api.World.GetBlock(CodeWithPart(BlockFacing.ALLFACES[i].Code, 2));
            }
        }

        public Block FacingCrystal(IBlockAccessor blockAccessor, BlockFacing facing)
        {
            return blockAccessor.GetBlock(CodeWithPart(facing.Code));
        }

        public override double ExplosionDropChance(IWorldAccessor world, BlockPos pos, EnumBlastType blastType)
        {
            return 0.2;
        }

        public override void OnBlockExploded(IWorldAccessor world, BlockPos pos, BlockPos explosionCenter, EnumBlastType blastType, string ignitedByPlayerUid)
        {
            if (world.Rand.NextDouble() < 0.25)
            {
                ItemStack stack = new ItemStack(api.World.GetBlock(CodeWithVariant("position", "up")));
                stack.StackSize = 1;
                world.SpawnItemEntity(stack, pos, null);
            }
            else
            {
                int startquantity = 3;
                if (Variant["variant"] == "cluster1" || Variant["variant"] == "cluster2") startquantity = 5;
                if (Variant["variant"] == "large1" || Variant["variant"] == "large2") startquantity = 7;

                int quantity = (int)(startquantity * Math.Min(1, world.Rand.NextDouble() * 0.31f + 0.7f));

                var type = Variant["type"] switch
                {
                    "milkyquartz" => "clearquartz",
                    "olivine" => "ore-olivine",
                    _ => Variant["type"]
                };

                ItemStack stack = new ItemStack(api.World.GetItem(new AssetLocation(type)));

                for (int k = 0; k < quantity; k++)
                {
                    ItemStack drop = stack.Clone();
                    drop.StackSize = 1;
                    world.SpawnItemEntity(drop, pos, null);
                }
            }

            // The explosion code uses the bulk block accessor for greater performance
            world.BulkBlockAccessor.SetBlock(0, pos);
        }

        public override double GetBlastResistance(IWorldAccessor world, BlockPos pos, Vec3f blastDirectionVector, EnumBlastType blastType)
        {
            return 0.5;
        }
    }
}
