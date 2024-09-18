using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSlabSnowRemove : Block, ITexPositionSource
    {
        MeshData groundSnowLessMesh;
        MeshData groundSnowedMesh;

        bool testGroundSnowRemoval;
        bool testGroundSnowAdd;

        BlockFacing rot;
        ICoreClientAPI capi;
        AssetLocation snowLoc = new AssetLocation("block/liquid/snow/normal1");   // Hardcoded yuk

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode] => capi.BlockTextureAtlas[snowLoc] ?? capi.BlockTextureAtlas.UnknownTexturePosition;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            capi = api as ICoreClientAPI;

            rot = BlockFacing.FromCode(Variant["rot"]);

            testGroundSnowRemoval =
                Variant["cover"] == "snow" &&
                (Variant["rot"] == "north" || Variant["rot"] == "east" || Variant["rot"] == "south" || Variant["rot"] == "west")
            ;

            testGroundSnowAdd =
                Variant["cover"] == "free" &&
                (Variant["rot"] == "north" || Variant["rot"] == "east" || Variant["rot"] == "south" || Variant["rot"] == "west")
            ;
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (testGroundSnowRemoval)
            {
                if (!SolidBlockBelow(chunkExtBlocks, extIndex3d, pos))
                {
                    if (groundSnowLessMesh == null)
                    {
                        groundSnowLessMesh = sourceMesh.Clone();
                        groundSnowLessMesh.RemoveVertices(24);
                        groundSnowLessMesh.XyzFacesCount -= 6;
                    }

                    sourceMesh = groundSnowLessMesh;
                }
            }

            if (testGroundSnowAdd && chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[rot.Opposite.Index]].BlockMaterial == EnumBlockMaterial.Snow && SolidBlockBelow(chunkExtBlocks, extIndex3d, pos))
            {
                if (groundSnowedMesh == null)
                {
                    Shape shape = API.Common.Shape.TryGet(api, "shapes/block/basic/slab/snow-" + Variant["rot"] + ".json");
                    (api as ICoreClientAPI).Tesselator.TesselateShape("slab snow cover", shape, out groundSnowedMesh, this);

                    // No idea why this is needed
                    for (int i = 0; i < groundSnowedMesh.RenderPassCount; i++)
                    {
                        groundSnowedMesh.RenderPassesAndExtraBits[i] = 0;
                    }

                    groundSnowedMesh.AddMeshData(sourceMesh);
                }

                sourceMesh = groundSnowedMesh;
            }
        }

        private bool SolidBlockBelow(Block[] chunkExtBlocks, int extIndex3d, BlockPos pos)
        {
            Block nblock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Down]];
            if (nblock.SideSolid[BlockFacing.UP.Index]) return true;
            nblock = api.World.BlockAccessor.GetBlock(pos.DownCopy(), BlockLayersAccess.Fluid);
            return nblock.SideSolid[BlockFacing.UP.Index];
        }

        public override float GetAmbientSoundStrength(IWorldAccessor world, BlockPos pos)
        {
            var conds = capi.World.Player.Entity.selfClimateCond;
            if (FirstCodePart() == "glassslab" && conds != null && conds.Rainfall > 0.1f && conds.Temperature > 3f && (world.BlockAccessor.GetRainMapHeightAt(pos) <= pos.Y || world.BlockAccessor.GetDistanceToRainFall(pos, 3, 1) <= 2))
            {
                return conds.Rainfall;
            }

            return 0;
        }
    }
}
