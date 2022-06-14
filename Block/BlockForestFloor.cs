using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
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

        
        CompositeTexture grassTex;


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

                var soilBlock = api.World.GetBlock(new AssetLocation("soil-low-normal"));
                if (soilBlock.Textures == null || !soilBlock.Textures.TryGetValue("specialSecondTexture", out grassTex))
                {
                    grassTex = soilBlock.Textures?.First().Value;
                }

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


        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;

            if (offThreadRandom.NextDouble() > growthChanceOnTick) return false;

            if (world.BlockAccessor.GetRainMapHeightAt(pos) > pos.Y + 1)
            {
                return false;
            }

            return extra != null;
        }

        protected bool isSmotheringBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetLiquidBlock(pos);
            if (block is BlockLakeIce || block.LiquidLevel > 1) return true;
            block = world.BlockAccessor.GetBlock(pos);
            return block.SideSolid[BlockFacing.DOWN.Index] && block.SideOpaque[BlockFacing.DOWN.Index] || block is BlockLava;
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
            float grassLevel = Variant["grass"].ToInt() / 7f;
            
            if (grassLevel == 0) return base.GetColorWithoutTint(capi, pos);
         
            int? textureSubId = grassTex?.Baked.TextureSubId;
            if (textureSubId == null)
            {
                return ColorUtil.WhiteArgb;
            }

            int grassColor = capi.BlockTextureAtlas.GetAverageColor((int)textureSubId);

            if (ClimateColorMapResolved != null)
            {
                grassColor = capi.World.ApplyColorMapOnRgba(ClimateColorMapResolved, SeasonColorMapResolved, grassColor, pos.X, pos.Y, pos.Z, false);
            }

            int soilColor = capi.BlockTextureAtlas.GetAverageColor((int)Textures["up"].Baked.TextureSubId);

            return ColorUtil.ColorOverlay(soilColor, grassColor, grassLevel);
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
