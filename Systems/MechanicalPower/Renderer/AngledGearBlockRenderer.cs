using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class AngledGearsBlockRenderer : MechBlockRenderer
    {
        MultiTextureMeshRef gearboxCage;
        MultiTextureMeshRef gearboxPeg;

        CustomMeshDataPartFloat floatsPeg;
        CustomMeshDataPartFloat floatsCage;


        public AngledGearsBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            capi.Tesselator.TesselateShape(textureSoureBlock, API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/angledgearbox-cage.json"), out MeshData gearboxCageMesh, rot);
            capi.Tesselator.TesselateShape(textureSoureBlock, API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/angledgearbox-peg.json"), out MeshData gearboxPegMesh, rot);

            
            floatsPeg = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            floatsPeg.SetAllocationSize((16 + 4) * 10100);

            floatsCage = floatsPeg.Clone();

            this.gearboxPeg = uploadInstancedMesh(gearboxPegMesh, floatsPeg);
            this.gearboxCage = uploadInstancedMesh(gearboxCageMesh, floatsCage);
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
                renderInstancedMesh(prog, gearboxPeg, floatsPeg, quantityBlocks);
                renderInstancedMesh(prog, gearboxCage, floatsCage, quantityBlocks);
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
