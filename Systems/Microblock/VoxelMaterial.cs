using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{

    public partial class BlockEntityMicroBlock
    {
        public readonly struct VoxelMaterial
        {
            public readonly int BlockId;
            public readonly bool ForDecal;
            public readonly TextureAtlasPosition[] Texture;// Texture per face
            public readonly TextureAtlasPosition[] TextureInside;
            public readonly TextureAtlasPosition TextureTopSoil;

            public readonly EnumChunkRenderPass RenderPass;
            public readonly int Flags;

            public readonly bool CullBetweenTransparents;
            public readonly byte ClimateMapIndex;
            public readonly bool Frostable;
            public readonly byte SeasonMapIndex;
            public readonly int TextureRotation;

            public VoxelMaterial(int blockId, TextureAtlasPosition[] texture, TextureAtlasPosition[] textureInside, TextureAtlasPosition textureTopSoil,
                EnumChunkRenderPass renderPass, int flags, byte climateMapIndex, byte seasonMapIndex, bool frostable, bool cullBetweenTransparents, int textureRotation, bool forDecal)
            {
                ClimateMapIndex = climateMapIndex;
                SeasonMapIndex = seasonMapIndex;
                BlockId = blockId;
                Texture = texture;
                TextureInside = textureInside;
                RenderPass = renderPass;
                Frostable = frostable;
                Flags = flags;
                TextureTopSoil = textureTopSoil;
                CullBetweenTransparents = cullBetweenTransparents;
                TextureRotation = textureRotation;
                ForDecal = forDecal;
            }

            public VoxelMaterial(int blockId, TextureAtlasPosition[] texture, TextureAtlasPosition[] textureInside, TextureAtlasPosition textureTopSoil,
                EnumChunkRenderPass renderPass, int flags, byte climateMapIndex, byte seasonMapIndex, bool frostable, bool cullBetweenTransparents) : this(blockId, texture, textureInside, textureTopSoil, renderPass, flags, climateMapIndex, seasonMapIndex, frostable, cullBetweenTransparents, 0, false)
            { }

            // Old constructor (pre- 1.21.2) without forDecal parameter, retained only for backwards compatibility for mods
            public VoxelMaterial(int blockId, TextureAtlasPosition[] texture, TextureAtlasPosition[] textureInside, TextureAtlasPosition textureTopSoil,
                EnumChunkRenderPass renderPass, int flags, byte climateMapIndex, byte seasonMapIndex, bool frostable, bool cullBetweenTransparents, int textureRotation) :
                this(blockId, texture, textureInside, textureTopSoil, renderPass, flags, climateMapIndex, seasonMapIndex, frostable, cullBetweenTransparents, textureRotation, false)
            { }

            public static VoxelMaterial FromBlock(ICoreClientAPI capi, Block block, BlockPos posForRnd = null, bool cullBetweenTransparents = false, int decorRotation = 0)
            {
                int altNum = 0;
                if (block.HasAlternates && posForRnd != null)
                {
                    int altcount = 0;
                    foreach (var pair in block.Textures)
                    {
                        var bct = pair.Value.Baked;
                        if (bct.BakedVariants != null)
                        {
                            altcount = Math.Max(altcount, bct.BakedVariants.Length);
                        }
                    }
                    if (altcount > 0)
                    {
                        altNum = ((block.RandomizeAxes == EnumRandomizeAxes.XYZ) ? GameMath.MurmurHash3Mod(posForRnd.X, posForRnd.Y, posForRnd.Z, altcount) : GameMath.MurmurHash3Mod(posForRnd.X, 0, posForRnd.Z, altcount));
                    }
                }

                var texture = new TextureAtlasPosition[6];
                var textureInside = new TextureAtlasPosition[6];
                TextureAtlasPosition fallbackTexture = null;

                var texSource = capi.Tesselator.GetTextureSource(block, altNum, returnNullWhenMissing: true);
                for (int i = 0; i < 6; i++)
                {
                    BlockFacing facing = BlockFacing.ALLFACES[i];

                    if (block.HasTiles && block.FastTextureVariants[i] is BakedCompositeTexture[] tiles && posForRnd != null)
                    {
                        int positionSelector = BakedCompositeTexture.GetTiledTexturesSelector(tiles, i, posForRnd.X, posForRnd.Y, posForRnd.Z);
                        int textureSubId = tiles[GameMath.Mod(positionSelector, tiles.Length)].TextureSubId;
                        texture[i] = textureSubId >= 0 ? capi.BlockTextureAtlas.Positions[textureSubId] : capi.BlockTextureAtlas.UnknownTexturePosition;
                    }
                    else
                    {
                        if ((texSource[facing.Code] == null || texSource["inside-" + facing.Code] == null) && fallbackTexture == null)
                        {
                            fallbackTexture = capi.BlockTextureAtlas.UnknownTexturePosition;
                            if (block.Textures.Count > 0) fallbackTexture = texSource[block.Textures.First().Key] ?? capi.BlockTextureAtlas.UnknownTexturePosition;
                        }

                        texture[i] = texSource[facing.Code] ?? fallbackTexture;
                    }

                    textureInside[i] = texSource["inside-" + facing.Code] ?? texture[i];
                }

                byte climateColorMapId = block.ClimateColorMapResolved == null ? (byte)0 : (byte)(block.ClimateColorMapResolved.RectIndex + 1);
                byte seasonColorMapId = block.SeasonColorMapResolved == null ? (byte)0 : (byte)(block.SeasonColorMapResolved.RectIndex + 1);

                TextureAtlasPosition grasscoverexPos = null;
                if (block.RenderPass == EnumChunkRenderPass.TopSoil)
                {
                    grasscoverexPos = capi.BlockTextureAtlas[block.Textures["specialSecondTexture"].Baked.BakedName];
                }

                return new VoxelMaterial(block.Id, texture, textureInside, grasscoverexPos, block.RenderPass, block.VertexFlags.All, climateColorMapId, seasonColorMapId, block.Frostable, cullBetweenTransparents, decorRotation, false);
            }

            public static VoxelMaterial FromTexSource(ICoreClientAPI capi, ITexPositionSource texSource, bool cullBetweenTransparents = false, bool forDecal = false)
            {
                var texture = new TextureAtlasPosition[6];
                var textureInside = new TextureAtlasPosition[6];
                for (int i = 0; i < 6; i++)
                {
                    BlockFacing facing = BlockFacing.ALLFACES[i];
                    texture[i] = texSource[facing.Code] ?? capi.BlockTextureAtlas.UnknownTexturePosition;
                    textureInside[i] = texSource["inside-" + facing.Code] ?? texSource[facing.Code] ?? capi.BlockTextureAtlas.UnknownTexturePosition;
                }
                return new VoxelMaterial(0, texture, textureInside, null, EnumChunkRenderPass.Opaque, 0, 0, 0, false, cullBetweenTransparents, 0, forDecal);
            }
        }



    }
}
