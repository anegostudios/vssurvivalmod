using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockCrystal : Block
    {
        Block[] FacingBlocks;
        Random rand;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            rand = new Random(api.World.Seed + 131);

            FacingBlocks = new Block[6];
            for (int i = 0; i < 6; i++)
            {
                FacingBlocks[i] = api.World.GetBlock(CodeWithPart(BlockFacing.ALLFACES[i].Code, 2));
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

        public override void OnBlockExploded(IWorldAccessor world, BlockPos pos, BlockPos explosionCenter, EnumBlastType blastType)
        {
            if (world.Rand.NextDouble() < 0.25)
            {
                ItemStack stack = new ItemStack(api.World.GetBlock(CodeWithVariant("position", "up")));
                stack.StackSize = 1;
                world.SpawnItemEntity(stack, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
            } else
            {
                int startquantity = 3;
                if (Variant["variant"] == "cluster1" || Variant["variant"] == "cluster2") startquantity = 5;
                if (Variant["variant"] == "large1" || Variant["variant"] == "large2") startquantity = 7;

                int quantity = (int)(startquantity * Math.Min(1, world.Rand.NextDouble() * 0.31f + 0.7f));

                string type = Variant["type"];
                if (Variant["type"] == "milkyquartz") type = "clearquartz";

                ItemStack stack = new ItemStack(api.World.GetItem(new AssetLocation(type)));
                
                for (int k = 0; k < quantity; k++)
                {
                    ItemStack drop = stack.Clone();
                    drop.StackSize = 1;
                    world.SpawnItemEntity(drop, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                }
            }
        }

        public override double GetBlastResistance(IWorldAccessor world, BlockPos pos, Vec3f blastDirectionVector, EnumBlastType blastType)
        {
            return 0.5;
        }

    }
}
