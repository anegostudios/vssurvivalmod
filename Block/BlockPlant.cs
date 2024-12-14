using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockFern : BlockPlant, ICustomTreeFellingBehavior
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            waveFlagMinY = 0.25f;
            tallGrassColorMapping = true;
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, pos, chunkExtBlocks, extIndex3d);

            for (int i = 0; i < sourceMesh.FlagsCount; i++)
            {
                sourceMesh.Flags[i] &= VertexFlags.ClearNormalBitMask;
                sourceMesh.Flags[i] |= VertexFlags.PackNormal(0, 1, 0);
            }
        }
    }

    public class BlockTallGrass : BlockPlant
    {
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);

            if (byPlayer?.InventoryManager.ActiveTool == EnumTool.Knife && Variant["tallgrass"] != null && Variant["tallgrass"] != "eaten")
            {
                world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariant("tallgrass", "eaten")).Id, pos);
            }

        }
    }

    public class BlockPlant : Block, IDrawYAdjustable, IWithDrawnHeight
    {
        Block snowLayerBlock;
        Block tallGrassBlock;

        protected bool climateColorMapping = false;
        protected bool tallGrassColorMapping = false;

        int ExtraBend = 0;
        public int drawnHeight { get; set; }

        protected bool disappearOnSoilRemoved = false;

        public virtual bool skipPlantCheck { get; set; } = false;

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

            disappearOnSoilRemoved = Attributes?["disappearOnSoilRemoved"].AsBool(false) ?? false;

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

            // Iterate over each element face
            for (int vertexNum = 0; vertexNum < verticesCount; vertexNum++)
            {
                int flag = sourceMesh.Flags[vertexNum] & clearFlags;

                if (!off && sourceMesh.xyz[vertexNum * 3 + 1] > 0.5)
                {
                    flag |= allFlags | ExtraBend;
                }
                sourceMesh.Flags[vertexNum] = flag;
            }
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if(skipPlantCheck)
                return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);

            if (Variant.ContainsKey("side"))
            {
                return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            }

            if (CanPlantStay(world.BlockAccessor, blockSel.Position))
            {
                return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            }

            failureCode = "requirefertileground";

            return false;
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!skipPlantCheck && !CanPlantStay(world.BlockAccessor, pos))
            {
                if (world.BlockAccessor.GetBlock(pos.DownCopy()).Id == 0 && disappearOnSoilRemoved) world.BlockAccessor.SetBlock(0, pos);
                else world.BlockAccessor.BreakBlock(pos, null);
            }
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public virtual bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (Variant.ContainsKey("side"))
            {
                var facing = BlockFacing.FromCode(Variant["side"]);

                var npos = pos.AddCopy(facing);
                var block = blockAccessor.GetBlock(npos);
                return block.CanAttachBlockAt(blockAccessor, this, npos, facing.Opposite);
            }
            else
            {
                Block blockBelow = blockAccessor.GetBlockBelow(pos);
                if (blockBelow.Fertility <= 0) return false;
                return true;
            }
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            if (!CanPlantStay(blockAccessor, pos)) return false;
            var canPlace = true;
            var tmpPos = pos.Copy();
            for (int x = -1; x < 2; x++)
            {
                for (int z = -1; z < 2; z++)
                {
                    tmpPos.Set(pos.X + x, pos.Y, pos.Z + z);
                    var block = blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Solid);
                    if (block is BlockWaterLilyGiant)
                    {
                        canPlace = false;
                    }
                }
            }
            if (!canPlace) return false;
            return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand, attributes);
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
