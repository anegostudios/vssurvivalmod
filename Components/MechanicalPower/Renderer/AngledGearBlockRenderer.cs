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


        public AngledGearsBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block block) : base(capi, mechanicalPowerMod, block)
        {
            Vec3f rot = new Vec3f(block.Shape.rotateX, block.Shape.rotateY, block.Shape.rotateZ);

            MeshData gearboxCageMesh;
            MeshData gearboxPegMesh;
            capi.Tesselator.TesselateShape(block, capi.Assets.TryGet("shapes/block/wood/angledgearbox-cage.json").ToObject<Shape>(), out gearboxCageMesh, rot);
            capi.Tesselator.TesselateShape(block, capi.Assets.TryGet("shapes/block/wood/angledgearbox-peg.json").ToObject<Shape>(), out gearboxPegMesh, rot);

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


        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float[] rotation, IMechanicalPowerNode dev)
        {
            float rotX = rotation[dev.AxisMapping[0]] * dev.AxisSign[0];
            float rotY = rotation[dev.AxisMapping[1]] * dev.AxisSign[1];
            float rotZ = rotation[dev.AxisMapping[2]] * dev.AxisSign[2];

            UpdateLightAndTransformMatrix(floatsPeg.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ - 0.08f);

            rotX = rotation[dev.AxisMapping[3]] * dev.AxisSign[3];
            rotY = rotation[dev.AxisMapping[4]] * dev.AxisSign[4];
            rotZ = rotation[dev.AxisMapping[5]] * dev.AxisSign[5];

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
    }
}
