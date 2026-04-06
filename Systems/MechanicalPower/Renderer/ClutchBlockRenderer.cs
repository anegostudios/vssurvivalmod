using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class ClutchBlockRenderer : MechBlockRenderer
    {
        CustomMeshDataPartFloat matrixAndLightFloats1;
        CustomMeshDataPartFloat matrixAndLightFloats2;
        List<MeshGroup> meshGroups1;
        List<MeshGroup> meshGroups2;

        public ClutchBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            Shape shape = API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/wood/mechanics/clutch-arm.json"));
            Shape ovshape = API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/wood/mechanics/clutch-drum.json"));

            capi.Tesselator.TesselateShape(textureSoureBlock, shape, out MeshData blockMesh1, rot);
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
            BEClutch clutch = dev as BEClutch;
            if (clutch == null) return;

            float rot1 = clutch.AngleRad;
            float rot2 = clutch.RotationNeighbour();

            float rotX = rot1 * dev.AxisSign[0];
            float rotY = rot1 * dev.AxisSign[1];
            float rotZ = rot1 * dev.AxisSign[2];
            UpdateLightAndTransformMatrix(matrixAndLightFloats1.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ, clutch.hinge, null);

            Vec3f axis = new Vec3f(0.5f - dev.AxisSign[2] * 0.125f, 0.625f, 0.5f + dev.AxisSign[0] * 0.125f);
            rotX = rot2 * dev.AxisSign[0];
            rotY = rot2 * dev.AxisSign[1];
            rotZ = rot2 * dev.AxisSign[2];
            UpdateLightAndTransformMatrix(matrixAndLightFloats2.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ, axis, (float[])tmpMat.Clone());
        }

        protected float[] UpdateLightAndTransformMatrix(float[] values, int index, Vec3f distToCamera, Vec4f lightRgba, float rotX, float rotY, float rotZ, Vec3f axis, float[] xtraTransform)
        {
            if (xtraTransform == null)
            {
                Mat4f.Identity(tmpMat);
                Mat4f.Translate(tmpMat, tmpMat, distToCamera.X + axis.X, distToCamera.Y + axis.Y, distToCamera.Z + axis.Z);
            }
            else
                Mat4f.Translate(tmpMat, tmpMat, axis.X, axis.Y, axis.Z);

            quat[0] = 0;
            quat[1] = 0;
            quat[2] = 0;
            quat[3] = 1;
            if (rotX != 0f) Quaterniond.RotateX(quat, quat, rotX);
            if (rotY != 0f) Quaterniond.RotateY(quat, quat, rotY);
            if (rotZ != 0f) Quaterniond.RotateZ(quat, quat, rotZ);

            Mat4f.MulQuat(tmpMat, quat);

            Mat4f.Translate(tmpMat, tmpMat, -axis.X, -axis.Y, -axis.Z);

            int j = index * 20;
            values[j] = lightRgba.R;
            values[++j] = lightRgba.G;
            values[++j] = lightRgba.B;
            values[++j] = lightRgba.A;

            for (int i = 0; i < 16; i++)
                values[++j] = tmpMat[i];

            return tmpMat;
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
