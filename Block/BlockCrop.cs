using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockCrop : Block, IDrawYAdjustable
    {
        private static readonly float defaultGrowthProbability = 0.8f;

        private float tickGrowthProbability;

        public int CurrentCropStage
        {
            get
            {
                int stage = 0;
                int.TryParse(LastCodePart(), out stage);
                return stage;
            }
        }


        //RoomRegistry roomreg;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Code.Path.Contains("sunflower"))
            {
                WaveFlagMinY = 0.1f;
            } else
            {
                WaveFlagMinY = 0.5f;
            }

            tickGrowthProbability = Attributes?["tickGrowthProbability"] != null ? Attributes["tickGrowthProbability"].AsFloat(defaultGrowthProbability) : defaultGrowthProbability;
            //roomreg = api.ModLoader.GetModSystem<RoomRegistry>();



            if (api.Side == EnumAppSide.Client)
            {
                if (this.RandomDrawOffset > 0)
                {
                    JsonObject overrider = Attributes?["overrideRandomDrawOffset"];
                    if (overrider?.Exists == true) this.RandomDrawOffset = overrider.AsInt(1);
                }
            }
        }


        public float AdjustYPosition(Block[] chunkExtBlocks, int extIndex3d)
        {
            Block nblock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Down]];
            return nblock is BlockFarmland ? -0.0625f : 0f;
        }


        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            /*if ((byte)(lightRgbsByCorner[24] >> 24) >= 159)
            {
                SetWindFlag(sourceMesh, (int)VertexFlags.WindMode);
            }
            else
            {
                ClearWindFlags(sourceMesh);
            }*/
        }

        public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos)
        {
            int sunLightLevel = world.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlySunLight);
            if (sunLightLevel >= 14)
            {
                SetWindFlag(decalMesh, (int)VertexFlags.WindMode);
            } else
            {
                ClearWindFlags(decalMesh);
            }
        }




        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            dsc.AppendLine(Lang.Get("Stage: {0}/{1}", CurrentCropStage, CropProps.GrowthStages)); 
        }


        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;

            if(offThreadRandom.NextDouble() < tickGrowthProbability && IsNotOnFarmland(world, pos))
            {
                extra = GetNextGrowthStageBlock(world, pos);
                return true;
            }
            return false;
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            Block block = extra as Block;
            world.BlockAccessor.ExchangeBlock(block.BlockId, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityFarmland befarmland = world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) as BlockEntityFarmland;
            if (befarmland != null && befarmland.OnBlockInteract(byPlayer)) return true;

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            Block block = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            if (block.Fertility == 0) return false;

            if (blockAccessor.GetBlock(pos).IsReplacableBy(this))
            {
                blockAccessor.SetBlock(BlockId, pos);
                return true;
            }

            return false;
        }

        public int CurrentStage()
        {
            int stage;
            int.TryParse(LastCodePart(), out stage);
            return stage;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            BlockEntityFarmland befarmland = world.BlockAccessor.GetBlockEntity(pos.DownCopy()) as BlockEntityFarmland;
            if (befarmland == null)
            {
                dropQuantityMultiplier *= byPlayer?.Entity.Stats.GetBlended("wildCropDropRate")?? 1;
            }

            ItemStack[] drops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

            
            if (befarmland != null)
            {
                drops = befarmland.GetDrops(drops);
            }

            return drops;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);

            BlockEntityFarmland befarmland = world.BlockAccessor.GetBlockEntity(pos.DownCopy()) as BlockEntityFarmland;
            befarmland?.OnCropBlockBroken();
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            Block block = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (block is BlockFarmland) return block.GetPlacedBlockInfo(world, pos.DownCopy(), forPlayer);

            return
                Lang.Get("Required Nutrient: {0}", CropProps.RequiredNutrient) + "\n" +
                Lang.Get("Growth Stage: {0} / {1}", CurrentStage(), CropProps.GrowthStages)
            ;
        }

        private bool IsNotOnFarmland(IWorldAccessor world, BlockPos pos)
        {
            Block onBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            return onBlock.FirstCodePart().Equals("farmland") == false;
        }

        private Block GetNextGrowthStageBlock(IWorldAccessor world, BlockPos pos)
        {
            int nextStage = CurrentStage() + 1;
            Block block = world.GetBlock(CodeWithParts(nextStage.ToString()));
            if (block == null)
            {
                nextStage = 1;
            }
            return world.GetBlock(CodeWithParts(nextStage.ToString()));
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-crop-breaktoharvest",
                    MouseButton = EnumMouseButton.Left,
                    ShouldApply = (wi, bs, es) => CropProps.GrowthStages == CurrentCropStage 
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
