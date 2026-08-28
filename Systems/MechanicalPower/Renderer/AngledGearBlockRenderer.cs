using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class AngledGearsBlockRenderer : MechBlockRenderer
    {
        List<MeshGroup> pegGroups;
        List<MeshGroup> cageGroups;

        CustomMeshDataPartFloat floatsPeg;
        CustomMeshDataPartFloat floatsCage;

        public AngledGearsBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            capi.Tesselator.TesselateShape(textureSoureBlock, API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/angledgearbox-cage.json"), out MeshData gearboxCageMesh, rot);
            capi.Tesselator.TesselateShape(textureSoureBlock, API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/angledgearbox-peg.json"), out MeshData gearboxPegMesh, rot);

            // 16 floats matrix, 4 floats light rgba
            // Cage and peg rotate independently so they need separate float buffers.
            gearboxPegMesh.CustomFloats = floatsPeg = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            gearboxPegMesh.CustomFloats.SetAllocationSize((16 + 4) * 10100);

            // Clone so cage transform data is independent from peg transform data
            gearboxCageMesh.CustomFloats = floatsCage = floatsPeg.Clone();

            pegGroups = UploadMeshGrouped(gearboxPegMesh, floatsPeg);
            cageGroups = UploadMeshGrouped(gearboxCageMesh, floatsCage);
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
            UpdateLightAndTransformMatrix(floatsPeg.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ);

            if (dev.AxisSign.Length < 4)
            {
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
                RenderGroups(prog, pegGroups, floatsPeg, quantityBlocks);
                RenderGroups(prog, cageGroups, floatsCage, quantityBlocks);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            DisposeGroups(cageGroups);
            DisposeGroups(pegGroups);
        }
    }
}
