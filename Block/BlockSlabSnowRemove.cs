using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

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
        AssetLocation snowLoc = new AssetLocation("block/snow/normal1");

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode] => capi.BlockTextureAtlas[snowLoc];

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

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, int[] chunkExtIds, ushort[] chunkLightExt, int extIndex3d)
        {
            if (testGroundSnowRemoval)
            {
                int nBlockId = chunkExtIds[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Down]];
                Block nblock = api.World.Blocks[nBlockId];

                if (!nblock.SideSolid[BlockFacing.UP.Index])
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

            if (testGroundSnowAdd && api.World.Blocks[chunkExtIds[extIndex3d + TileSideEnum.MoveIndex[rot.Opposite.Index]]].BlockMaterial == EnumBlockMaterial.Snow && api.World.Blocks[chunkExtIds[extIndex3d + TileSideEnum.MoveIndex[5]]].SideSolid[BlockFacing.UP.Index] == true)
            {
                if (groundSnowedMesh == null)
                {
                    Shape shape = api.Assets.Get("shapes/block/basic/slab/snow-" + Variant["rot"] + ".json").ToObject<Shape>();
                    (api as ICoreClientAPI).Tesselator.TesselateShape("slab snow cover", shape, out groundSnowedMesh, this);

                    // No idea why this is needed
                    for (int i = 0; i < groundSnowedMesh.RenderPassCount; i++)
                    {
                        groundSnowedMesh.RenderPasses[i] = 0;
                    }

                    groundSnowedMesh.AddMeshData(sourceMesh);
                }

                sourceMesh = groundSnowedMesh;
            }
        }
    }
}
