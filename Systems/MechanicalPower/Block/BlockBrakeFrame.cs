using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockBrakeFrame : BlockAxleAttachment
    {
        AssetLocation openedShapeLoc = null!;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            openedShapeLoc = AssetLocation.Create(Attributes["openedShape"].AsString(), Code.Domain).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
        }

        public override bool OnFrameInteract(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
        {
            return world.BlockAccessor.GetBlockEntity<BEBrake>(pos)?.OnInteract(byPlayer) == true;
        }

        public override bool CanInsertAxle(IWorldAccessor world, BlockPos pos, ref string failureCode)
        {
            if (world.BlockAccessor.GetBlockEntity<BEBrake>(pos)?.Engaged != true) return true;

            failureCode = "brakemustbeopen";
            return false;
        }

        public MeshData GetFrameMesh(ICoreClientAPI capi, ITesselatorAPI tesselator, ItemStack? frameStack, bool engaged)
        {
            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(capi, "brakeFrameMeshes", () => new Dictionary<string, MeshData>());
            string key = $"{Code}-{GetMaterialKey(frameStack)}-{engaged}";
            if (meshes.TryGetValue(key, out MeshData? mesh)) return mesh;

            string sourceForLogging = $"{Code} frame";
            Shape shape = API.Common.Shape.TryGet(capi, engaged ? Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json") : openedShapeLoc);
            tesselator.TesselateShape(sourceForLogging, shape, out mesh, GetMaterialTextureSource(capi, shape, sourceForLogging, frameStack), new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            return meshes[key] = mesh;
        }
    }
}
