using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class TransmissionBlockRenderer : MechBlockRenderer
    {
        CustomMeshDataPartFloat matrixAndLightFloats1;
        CustomMeshDataPartFloat matrixAndLightFloats2;
        List<MeshGroup> meshGroups1;
        List<MeshGroup> meshGroups2;

        public TransmissionBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            AssetLocation loc = shapeLoc.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape shape = API.Common.Shape.TryGet(capi, loc);
            capi.Tesselator.TesselateShape(textureSoureBlock, shape, out MeshData blockMesh1, rot);

            Shape ovshape = API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/wood/mechanics/transmission-rightgear.json"));
            capi.Tesselator.TesselateShape(textureSoureBlock, ovshape, out MeshData blockMesh2, rot);

            // 16 floats matrix, 4 floats light rgba
            blockMesh1.CustomFloats = matrixAndLightFloats1 = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            blockMesh1.CustomFloats.SetAllocationSize((16 + 4) * 10100);

            blockMesh2.CustomFloats = matrixAndLightFloats2 = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            blockMesh2.CustomFloats.SetAllocationSize((16 + 4) * 10100);

            meshGroups1 = UploadMeshGrouped(blockMesh1, matrixAndLightFloats1);
            meshGroups2 = UploadMeshGrouped(blockMesh2, matrixAndLightFloats2);
        }

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotation, IMechanicalPowerRenderable dev)
        {
            BEBehaviorMPTransmission trans = dev as BEBehaviorMPTransmission;
            if (trans == null) return;

            float rot1 = trans.RotationNeighbour(1, true);
            float rot2 = trans.RotationNeighbour(0, true);

            float rotX = rot1 * dev.AxisSign[0];
            float rotY = rot1 * dev.AxisSign[1];
            float rotZ = rot1 * dev.AxisSign[2];
            UpdateLightAndTransformMatrix(matrixAndLightFloats1.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ);

            rotX = rot2 * dev.AxisSign[0];
            rotY = rot2 * dev.AxisSign[1];
            rotZ = rot2 * dev.AxisSign[2];
            UpdateLightAndTransformMatrix(matrixAndLightFloats2.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ);
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            if (quantityBlocks > 0)
            {
                RenderGroups(prog, meshGroups1, matrixAndLightFloats1, quantityBlocks);
                RenderGroups(prog, meshGroups2, matrixAndLightFloats2, quantityBlocks);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            DisposeGroups(meshGroups1);
            DisposeGroups(meshGroups2);
        }
    }
}
