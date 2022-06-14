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
            capi.Tesselator.TesselateShape(textureSoureBlock, API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/angledgearbox-cage.json"), out gearboxCageMesh, rot);
            capi.Tesselator.TesselateShape(textureSoureBlock, API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/angledgearbox-peg.json"), out gearboxPegMesh, rot);

            
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

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotationRad, IMechanicalPowerRenderable dev)
        {
            BEBehaviorMPAngledGears gear = dev as BEBehaviorMPAngledGears;
            if (gear != null)
            {
                BlockFacing inTurn = gear.GetPropagationDirection();
                if (inTurn == gear.axis1 || inTurn == gear.axis2)
                {
                    rotationRad = -rotationRad;
                }
            }
            float rotX = rotationRad * dev.AxisSign[0];
            float rotY = rotationRad * dev.AxisSign[1];
            float rotZ = rotationRad * dev.AxisSign[2];
            UpdateLightAndTransformMatrix(floatsPeg.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ);// - 0.08f);

            if (dev.AxisSign.Length < 4)
            {
                //System.Diagnostics.Debug.WriteLine("3 length AxisSign");
                return;
            }
            rotX = rotationRad * dev.AxisSign[3];
            rotY = rotationRad * dev.AxisSign[4];
            rotZ = rotationRad * dev.AxisSign[5];
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
