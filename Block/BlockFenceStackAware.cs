using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockFenceStackAware : BlockFence
    {
        ICoreClientAPI capi;

        Dictionary<string, MeshData> continousFenceMeches;
        string cntCode;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            capi = api as ICoreClientAPI;


            if (capi != null)
            {
                continousFenceMeches = ObjectCacheUtil.GetOrCreate(capi, Code.Domain + ":" + FirstCodePart() + "-continousFenceMeches", () => new Dictionary<string, MeshData>());

                cntCode = Code.ToShortString();
            }
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            Block upBlock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Up]];
            if (upBlock is BlockFence)
            {
                int var = (GameMath.MurmurHash3Mod(pos.X, pos.Y, pos.Z, 8) + 1);

                MeshData mesh;
                if (!continousFenceMeches.TryGetValue(cntCode + var, out mesh))
                {
                    AssetLocation loc = Shape.Base.Clone();
                    loc.Path = loc.Path.Replace("-top", "");
                    loc.WithPathAppendixOnce(".json");
                    loc.WithPathPrefixOnce("shapes/");
                    Shape shape = API.Common.Shape.TryGet(capi, loc);

                    CompositeTexture ct = Textures["wall"];
                    int prevSubid = ct.Baked.TextureSubId;
                    ct.Baked.TextureSubId = ct.Baked.BakedVariants[GameMath.MurmurHash3Mod(pos.X, pos.Y, pos.Z, ct.Alternates.Length)].TextureSubId;
                    
                    capi.Tesselator.TesselateShape(this, shape, out mesh, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ), Shape.QuantityElements, Shape.SelectiveElements);

                    ct.Baked.TextureSubId = prevSubid;

                    continousFenceMeches[cntCode] = mesh;
                }

                sourceMesh = mesh;
            }


            // Todo: make this work
            /*            int nBlockId = chunkExtIds[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Up]];
                        Block upblock = api.World.Blocks[nBlockId];

                        if (upblock.snowLevel >= 1 && snowLayerBlock != null)
                        {
                            sourceMesh = sourceMesh.Clone();
                            sourceMesh.AddMeshData(capi.TesselatorManager.GetDefaultBlockMesh(snowLayerBlock));
                            return;
                        }*/

            return;  // no windwave for solid fences!

            //base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, pos, chunkExtIds, chunkLightExt, extIndex3d);
        }
    }
}
