using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockPlant : BlockRequireFertileGround, IDrawYAdjustable, IWithDrawnHeight
    {
        Block snowLayerBlock;
        Block tallGrassBlock;

        protected bool climateColorMapping = false;
        protected bool tallGrassColorMapping = false;

        int ExtraBend = 0;
        public int drawnHeight { get; set; }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Client)
            {
                snowLayerBlock = api.World.GetBlock(new AssetLocation("snowlayer-1"));
                tallGrassBlock = api.World.GetBlock(new AssetLocation("tallgrass-tall-free"));
                if (this.RandomDrawOffset > 0)
                {
                    JsonObject overrider = Attributes?["overrideRandomDrawOffset"];
                    if (overrider?.Exists == true) this.RandomDrawOffset = overrider.AsInt(1);
                }
            }           

            climateColorMapping = EntityClass == "Sapling";
            tallGrassColorMapping = Code.Path == "flower-lilyofthevalley-free";

            ExtraBend = (Attributes?["extraBend"].AsInt(0) ?? 0) << VertexFlags.WindDataBitsPos;
            drawnHeight = Attributes?["drawnHeight"]?.AsInt(48) ?? 48;
        }

        public float AdjustYPosition(BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            Block nblock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Down]];
            return nblock is BlockFarmland ? -0.0625f : 0f;
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (VertexFlags.WindMode == EnumWindBitMode.NormalWind)
            {
                bool waveOff = (lightRgbsByCorner[24] >> 24 & 0xff) < 159;  //corresponds with a sunlight level of less than 14

                setLeaveWaveFlags(sourceMesh, waveOff);
            }
        }


        void setLeaveWaveFlags(MeshData sourceMesh, bool off)
        {
            int allFlags = VertexFlags.All;
            int clearFlags = VertexFlags.WindBitsMask;
            int verticesCount = sourceMesh.VerticesCount;

            int windToApply = allFlags | ExtraBend;

            // Iterate over each element face
            var sourceMeshFlags = sourceMesh.Flags;
            var sourceMeshXyz = sourceMesh.xyz;
            for (int vertexNum = 0; vertexNum < verticesCount; vertexNum++)
            {
                int flag = sourceMeshFlags[vertexNum] & clearFlags;

                if (!off && sourceMeshXyz[vertexNum * 3 + 1] > 0.5)
                {
                    flag |= windToApply;
                }
                sourceMeshFlags[vertexNum] = flag;
            }
        }



        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            if (snowLevel > 0) return snowLayerBlock.GetRandomColor(capi, pos, facing, rndIndex);

            if (tallGrassColorMapping)
            {
                return tallGrassBlock.GetRandomColor(capi, pos, BlockFacing.UP, rndIndex);
            }

            int color = base.GetRandomColor(capi, pos, facing, rndIndex);
            if (climateColorMapping)
            {
                color = capi.World.ApplyColorMapOnRgba(ClimateColorMap, SeasonColorMap, color, pos.X, pos.Y, pos.Z);
            }


            return color;
        }


        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            if (EntityClass != null)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
                if (be is BlockEntitySapling bes) return bes.GetBlockName();
            }

            return base.GetPlacedBlockName(world, pos);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (EntityClass != null)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
                if (be is BlockEntitySapling bes) return bes.GetDrops();
            }

            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }


        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            if (snowLevel > 0)
            {
                return snowLayerBlock.GetColor(capi, pos);
            }
            if (tallGrassColorMapping)
            {
               return tallGrassBlock.GetColor(capi, pos);
            }

            return base.GetColor(capi, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            // Return an unshifted selection box if looking at or breaking a snowy block, or a block with only small random drawoffset (e.g. berry bush)
            if (this.snowLevel > 1 || this.RandomDrawOffset > 7)
            {
                return SelectionBoxes;
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public EnumTreeFellingBehavior GetTreeFellingBehavior(BlockPos pos, Vec3i fromDir, int spreadIndex)
        {
            return EnumTreeFellingBehavior.ChopSpreadVertical;
        }
    }
}
