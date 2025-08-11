using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockFarmland : Block
    {
        public CodeAndChance[] WeedNames;
        public int DelayGrowthBelowSunLight = 19;
        public float LossPerLevel = 0.1f;
        public float TotalWeedChance;

        public WeatherSystemBase wsys;
        public RoomRegistry roomreg;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes != null)
            {
                DelayGrowthBelowSunLight = Attributes["delayGrowthBelowSunLight"].AsInt(19);
                LossPerLevel = Attributes["lossPerLevel"].AsFloat(0.1f);

                if (WeedNames == null)
                {
                    WeedNames = Attributes["weedBlockCodes"].AsObject<CodeAndChance[]>();
                    for (int i = 0; WeedNames != null && i < WeedNames.Length; i++)
                    {
                        TotalWeedChance += WeedNames[i].Chance;
                    }
                }
            }

            wsys = api.ModLoader.GetModSystem<WeatherSystemBase>();
            roomreg = api.ModLoader.GetModSystem<RoomRegistry>();
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);

            if (byItemStack != null)
            {
                BlockEntityFarmland befarmland = world.BlockAccessor.GetBlockEntity(blockPos) as BlockEntityFarmland;
                befarmland?.OnCreatedFromSoil(byItemStack.Block);
            }
        }


        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            if ((block is BlockCrop || block is BlockDeadCrop) && blockFace == BlockFacing.UP) return true;

            if (blockFace.IsHorizontal) return false;
            return base.CanAttachBlockAt(world, block, pos, blockFace, attachmentArea);
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityFarmland befarmland = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFarmland;
            if (befarmland != null && befarmland.OnBlockInteract(byPlayer)) return true;

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityFarmland befarmland = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityFarmland;

            if (befarmland != null)
            {
                Block farmlandBlock = api.World.GetBlock(CodeWithVariant("state", befarmland.IsVisiblyMoist ? "moist" : "dry"));
                return new ItemStack(farmlandBlock).GetName();

            }
            return base.GetPlacedBlockName(world, pos);
        }


        public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
        {
            return facing == BlockFacing.UP ? 0 : 3;
        }


        public override bool SideIsSolid(BlockPos pos, int faceIndex)
        {
            // Although the block has solid sides for general purposes (it's actually almost solid, but the top row of voxels is removed) it is not fully solid, and therefore adjacent liquid blocks should have their sides rendered
            return faceIndex == BlockFacing.indexDOWN;
        }

    }
}
