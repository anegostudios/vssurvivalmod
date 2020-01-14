using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class AngledGearsBlockRenderer : MechBlockRenderer
    {
        MeshRef gearboxCage;
        MeshRef gearboxPeg;

        CustomMeshDataPartFloat floatsPeg;
        CustomMeshDataPartFloat floatsCage;


        public AngledGearsBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            MeshData gearboxCageMesh;
            MeshData gearboxPegMesh;
            capi.Tesselator.TesselateShape(textureSoureBlock, capi.Assets.TryGet("shapes/block/wood/mechanics/angledgearbox-cage.json").ToObject<Shape>(), out gearboxCageMesh, rot);
            capi.Tesselator.TesselateShape(textureSoureBlock, capi.Assets.TryGet("shapes/block/wood/mechanics/angledgearbox-peg.json").ToObject<Shape>(), out gearboxPegMesh, rot);

            gearboxPegMesh.Rgba2 = null;
            gearboxCageMesh.Rgba2 = null;

            // 16 floats matrix, 4 floats light rgbs
            gearboxPegMesh.CustomFloats = floatsPeg = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            gearboxPegMesh.CustomFloats.SetAllocationSize((16 + 4) * 10100);

            gearboxCageMesh.CustomFloats = floatsCage = floatsPeg.Clone();

            this.gearboxPeg = capi.Render.UploadMesh(gearboxPegMesh);
            this.gearboxCage = capi.Render.UploadMesh(gearboxCageMesh);
        }


        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float[] rotationRad, IMechanicalPowerNode dev)
        {
            float rotX = rotationRad[dev.AxisMapping[0]] * dev.AxisSign[0];
            float rotY = rotationRad[dev.AxisMapping[1]] * dev.AxisSign[1];
            float rotZ = rotationRad[dev.AxisMapping[2]] * dev.AxisSign[2];

            UpdateLightAndTransformMatrix(floatsPeg.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ);// - 0.08f);

            rotX = rotationRad[dev.AxisMapping[3]] * dev.AxisSign[3];
            rotY = rotationRad[dev.AxisMapping[4]] * dev.AxisSign[4];
            rotZ = rotationRad[dev.AxisMapping[5]] * dev.AxisSign[5];

            UpdateLightAndTransformMatrix(floatsCage.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ);
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            if (quantityBlocks > 0)
            {
                floatsPeg.Count = quantityBlocks * 20;
                floatsCage.Count = quantityBlocks * 20;

                updateMesh.CustomFloats = floatsPeg;
                capi.Render.UpdateMesh(gearboxPeg, updateMesh);

                updateMesh.CustomFloats = floatsCage;
                capi.Render.UpdateMesh(gearboxCage, updateMesh);

                capi.Render.RenderMeshInstanced(gearboxPeg, quantityBlocks);
                capi.Render.RenderMeshInstanced(gearboxCage, quantityBlocks);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            gearboxCage?.Dispose();
            gearboxPeg?.Dispose();
        }
    }
}
