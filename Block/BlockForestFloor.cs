using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.GameContent
{

    /// <summary>
    /// Handles eventual long-term transition to standard soil via server ticks.
    /// </summary>
    public class BlockForestFloor : Block   //WithGrassOverlay
    {
        protected string[] growthStages = new string[] { "0", "1", "2", "3", "4", "5", "6", "7" };

        protected int growthLightLevel;
        protected int chunksize;

        protected float growthChanceOnTick = 0.16f;

        static public int MaxStage { get; set; }

        int mapColorTextureSubId;



        public int CurrentLevel()
        {
            return MaxStage - (Code.Path[Code.Path.Length - 1] - '0');
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            chunksize = api.World.BlockAccessor.ChunkSize;

            if (api is ICoreClientAPI)
            {
                Block fullCoverBlock = api.World.GetBlock(this.CodeWithParts("7"));
                mapColorTextureSubId = fullCoverBlock.Textures["specialSecondTexture"].Baked.TextureSubId;
            }
        }

        // A bit clunky / hard-coded still, find a better way to do this
        internal static int[] InitialiseForestBlocks(IWorldAccessor world)
        {
            MaxStage = 8;
            int[] result = new int[MaxStage];

            for (int i = 0; i < MaxStage; i++)
            {
                result[i] = world.GetBlock(new AssetLocation("forestfloor-" + i)).Id;
            }
            return result;
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            base.OnServerGameTick(world, pos, extra);

            //GrassTick tick = extra as GrassTick;
            //world.BlockAccessor.SetBlock(tick.Grass.BlockId, pos);
            //if (tick.TallGrass != null && world.BlockAccessor.GetBlock(pos.UpCopy()).BlockId == 0)
            //{
            //    world.BlockAccessor.SetBlock(tick.TallGrass.BlockId, pos.UpCopy());
            //}
        }

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;

            if (offThreadRandom.NextDouble() > growthChanceOnTick) return false;

            if (world.BlockAccessor.GetRainMapHeightAt(pos) > pos.Y + 1)
            {
                return false;
            }

            //bool isGrowing = false;

            //Block grass;
            //BlockPos upPos = pos.UpCopy();
            
            //bool lowLightLevel = world.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.MaxLight) < growthLightLevel;
            //if (lowLightLevel || isSmotheringBlock(world, upPos))
            //{
            //    grass = tryGetBlockForDying(world);
            //}
            //else
            //{
            //    isGrowing = true;
            //    grass = tryGetBlockForGrowing(world, pos);
            //}

            //if (grass != null)
            //{
            //    extra = new GrassTick()
            //    {
            //        Grass = grass,
            //        TallGrass = isGrowing ? getTallGrassBlock(world, upPos, offThreadRandom) : null
            //    };
            //}
            return extra != null;
        }

        protected bool isSmotheringBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(pos);
            return block.SideSolid[BlockFacing.DOWN.Index] && block.SideOpaque[BlockFacing.DOWN.Index];
        }

        protected Block tryGetBlockForGrowing(IWorldAccessor world, BlockPos pos)
        {
            return null;
        }

        protected Block tryGetBlockForDying(IWorldAccessor world)
        {
            return null;
        }


        protected int getClimateSuitedGrowthStage(IWorldAccessor world, BlockPos pos, ClimateCondition climate)
        {
            return CurrentLevel();
        }


        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            return base.GetColor(capi, pos);
        }


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            if (facing == BlockFacing.UP)
            {
                return capi.World.ApplyColorMapOnRgba(ClimateColorMap, SeasonColorMap, capi.BlockTextureAtlas.GetRandomColor(mapColorTextureSubId, rndIndex), pos.X, pos.Y, pos.Z);
            }
            return base.GetRandomColor(capi, pos, facing, rndIndex);
        }
    }
}
