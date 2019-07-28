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
    public class GenericMechBlockRenderer : MechBlockRenderer
    {
        Block block;

        CustomMeshDataPartFloat matrixAndLightFloats;
        MeshRef blockMeshRef;

        

        public GenericMechBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block block) : base(capi, mechanicalPowerMod, block)
        {
            this.block = block;
            MeshData blockMesh;

            Shape shape = capi.Assets.TryGet(block.Shape.Base.Clone().WithPathPrefix("shapes/") + ".json").ToObject<Shape>();
            Vec3f rot = new Vec3f(block.Shape.rotateX, block.Shape.rotateY, block.Shape.rotateZ);
            capi.Tesselator.TesselateShape(block, shape, out blockMesh, rot);

            blockMesh.Rgba2 = null;

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

        
        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float[] rotation, IMechanicalPowerNode dev)
        {
            float rotX = rotation[dev.AxisMapping[0]] * dev.AxisSign[0];
            float rotY = rotation[dev.AxisMapping[1]] * dev.AxisSign[1];
            float rotZ = rotation[dev.AxisMapping[2]] * dev.AxisSign[2];

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
