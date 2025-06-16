using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class AngledCageGearRenderer : MechBlockRenderer
    {
        CustomMeshDataPartFloat matrixAndLightFloats;
        MeshRef blockMeshRef;

        public AngledCageGearRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {

            AssetLocation loc = shapeLoc.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            Shape shape = API.Common.Shape.TryGet(capi, loc);
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            capi.Tesselator.TesselateShape(textureSoureBlock, shape, out MeshData blockMesh, rot);

            if (shapeLoc.Overlays != null)
            {
                for (int i = 0; i < shapeLoc.Overlays.Length; i++)
                {
                    CompositeShape ovShapeCmp = shapeLoc.Overlays[i];
                    rot = new Vec3f(ovShapeCmp.rotateX, ovShapeCmp.rotateY, ovShapeCmp.rotateZ);
                    
                    Shape ovshape = API.Common.Shape.TryGet(capi, ovShapeCmp.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                    capi.Tesselator.TesselateShape(textureSoureBlock, ovshape, out MeshData overlayMesh, rot);
                    blockMesh.AddMeshData(overlayMesh);
                }
            }

            //blockMesh.Rgba2 = null;

            // 16 floats matrix, 4 floats light rgbs
            blockMesh.CustomFloats = matrixAndLightFloats = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            blockMesh.CustomFloats.SetAllocationSize((16 + 4) * 10100);

            this.blockMeshRef = capi.Render.UploadMesh(blockMesh);
        }

        
        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotation, IMechanicalPowerRenderable dev)
        {
            BEBehaviorMPAngledGears gear = dev as BEBehaviorMPAngledGears;
            rotation = gear.LargeGearAngleRad(rotation);

            float rotX = rotation * dev.AxisSign[0];
            float rotY = rotation * dev.AxisSign[1];
            float rotZ = rotation * dev.AxisSign[2];

            UpdateLightAndTransformMatrix(matrixAndLightFloats.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ);
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            if (quantityBlocks > 0)
            {
                matrixAndLightFloats.Count = quantityBlocks * 20;
                updateMesh.CustomFloats = matrixAndLightFloats;
                capi.Render.UpdateMesh(blockMeshRef, updateMesh);
                capi.Render.RenderMeshInstanced(blockMeshRef, quantityBlocks);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            blockMeshRef?.Dispose();
        }
    }
}
