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
        MultiTextureMeshRef blockMeshRef1;
        MultiTextureMeshRef blockMeshRef2;

        public TransmissionBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {

            AssetLocation loc = shapeLoc.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            Shape shape = API.Common.Shape.TryGet(capi, loc);
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            capi.Tesselator.TesselateShape(textureSoureBlock, shape, out MeshData blockMesh1, rot);

            CompositeShape ovShapeCmp = new CompositeShape() { Base = new AssetLocation("shapes/block/wood/mechanics/transmission-rightgear.json") };
            //rot = new Vec3f(ovShapeCmp.rotateX, ovShapeCmp.rotateY, ovShapeCmp.rotateZ);
            rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);
            Shape ovshape = API.Common.Shape.TryGet(capi, ovShapeCmp.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
            capi.Tesselator.TesselateShape(textureSoureBlock, ovshape, out MeshData blockMesh2, rot);

            //blockMesh1.Rgba2 = null;
            //blockMesh2.Rgba2 = null;

            matrixAndLightFloats1 = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            matrixAndLightFloats1.SetAllocationSize((16 + 4) * 10100);
            matrixAndLightFloats2 = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            matrixAndLightFloats2.SetAllocationSize((16 + 4) * 10100);

            this.blockMeshRef1 = uploadInstancedMesh(blockMesh1, matrixAndLightFloats1);
            this.blockMeshRef2 = uploadInstancedMesh(blockMesh2, matrixAndLightFloats2);
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
                renderInstancedMesh(prog, blockMeshRef1, matrixAndLightFloats1, quantityBlocks);
                renderInstancedMesh(prog, blockMeshRef2, matrixAndLightFloats2, quantityBlocks);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            blockMeshRef1?.Dispose();
            blockMeshRef2?.Dispose();
        }
    }
}
