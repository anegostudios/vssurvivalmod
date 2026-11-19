using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;

namespace Vintagestory.GameContent
{
    public class BlockGrindingWheel : BlockAxleAttachment
    {
        public MeshData GetFrameMesh(ICoreClientAPI capi, ITesselatorAPI tesselator, ItemStack? frameStack)
        {
            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(capi, "grindingWheelFrameMeshes", () => new Dictionary<string, MeshData>());
            string key = $"{Code}-{GetMaterialKey(frameStack)}";
            if (meshes.TryGetValue(key, out MeshData? mesh)) return mesh;

            string sourceForLogging = $"{Code} frame";
            Shape shape = API.Common.Shape.TryGet(capi, Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
            tesselator.TesselateShape(sourceForLogging, shape, out MeshData frameMesh, GetMaterialTextureSource(capi, shape, sourceForLogging, frameStack), new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ), quantityElements: Shape.QuantityElements, selectiveElements: Shape.SelectiveElements);
            return meshes[key] = frameMesh;
        }
    }
}
