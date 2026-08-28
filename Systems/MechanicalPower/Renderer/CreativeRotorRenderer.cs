using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class CreativeRotorRenderer : MechBlockRenderer
    {
        CustomMeshDataPartFloat matrixAndLightFloats1;
        CustomMeshDataPartFloat matrixAndLightFloats2;
        CustomMeshDataPartFloat matrixAndLightFloats3;
        CustomMeshDataPartFloat matrixAndLightFloats4;
        CustomMeshDataPartFloat matrixAndLightFloats5;

        List<MeshGroup> meshGroups1;   // axle
        List<MeshGroup> meshGroups2;   // contra-rotating parts
        List<MeshGroup> meshGroups3;   // spinbar
        List<MeshGroup> meshGroups4;   // spinball (same mesh, used for both ball instances)

        Vec3f axisCenter = new Vec3f(0.5f, 0.5f, 0.5f);

        public CreativeRotorRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            capi.Tesselator.TesselateShape(textureSoureBlock, API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/metal/mechanics/creativerotor-axle.json")), out MeshData blockMesh1, rot);
            capi.Tesselator.TesselateShape(textureSoureBlock, API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/metal/mechanics/creativerotor-contra.json")), out MeshData blockMesh2, rot);
            capi.Tesselator.TesselateShape(textureSoureBlock, API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/metal/mechanics/creativerotor-spinbar.json")), out MeshData blockMesh3, rot);
            capi.Tesselator.TesselateShape(textureSoureBlock, API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/metal/mechanics/creativerotor-spinball.json")), out MeshData blockMesh4, rot);

            int count = (16 + 4) * 2100;

            blockMesh1.CustomFloats = matrixAndLightFloats1 = CreateCustomFloats(count);
            blockMesh2.CustomFloats = matrixAndLightFloats2 = CreateCustomFloats(count);
            blockMesh3.CustomFloats = matrixAndLightFloats3 = CreateCustomFloats(count);
            blockMesh4.CustomFloats = matrixAndLightFloats4 = CreateCustomFloats(count);
            // Ball 5 shares the same mesh groups as ball 4 but uses an independent transform buffer
            matrixAndLightFloats5 = CreateCustomFloats(count);

            meshGroups1 = UploadMeshGrouped(blockMesh1, matrixAndLightFloats1);
            meshGroups2 = UploadMeshGrouped(blockMesh2, matrixAndLightFloats2);
            meshGroups3 = UploadMeshGrouped(blockMesh3, matrixAndLightFloats3);
            // meshGroups4 is uploaded once; rendered twice with different float buffers (ball 4 and ball 5)
            meshGroups4 = UploadMeshGrouped(blockMesh4, matrixAndLightFloats4);
        }

        private CustomMeshDataPartFloat CreateCustomFloats(int count)
        {
            var result = new CustomMeshDataPartFloat(count)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            result.SetAllocationSize(count);
            return result;
        }

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotation, IMechanicalPowerRenderable dev)
        {
            float rot1 = dev.AngleRad;
            float rot2 = GameMath.TWOPI - dev.AngleRad;
            float rot3 = rot1 * 2f;
            float axX = -Math.Abs(dev.AxisSign[0]);
            float axZ = -Math.Abs(dev.AxisSign[2]);

            // Axle
            UpdateLightAndTransformMatrix(matrixAndLightFloats1.Values, index, distToCamera, dev.LightRgba, rot1 * axX, rot1 * axZ, axisCenter, null);

            // Contra-rotating axle
            UpdateLightAndTransformMatrix(matrixAndLightFloats2.Values, index, distToCamera, dev.LightRgba, rot2 * axX, rot2 * axZ, axisCenter, null);

            // Spinbar
            UpdateLightAndTransformMatrix(matrixAndLightFloats3.Values, index, distToCamera, dev.LightRgba, rot3 * axX, rot3 * axZ, axisCenter, null);

            // Ball 4: position on spinbar (45° ahead), then apply its own spin
            TransformMatrix(distToCamera, (rot3 + GameMath.PI / 4) * axX, (rot3 + GameMath.PI / 4) * axZ, axisCenter);
            float b4rotX = axX == 0 ? rot1 * 2f : 0f;
            float b4rotZ = axZ == 0 ? -rot1 * 2f : 0f;
            float offX4 = dev.AxisSign[0] * 0.05f;
            float offZ4 = dev.AxisSign[2] * 0.05f;
            UpdateLightAndTransformMatrix(matrixAndLightFloats4.Values, index, distToCamera, dev.LightRgba, b4rotX, b4rotZ, new Vec3f(0.5f + offX4, 0.5f, 0.5f + offZ4), (float[])tmpMat.Clone());

            // Ball 5: opposite side of spinbar (225° ahead)
            TransformMatrix(distToCamera, (rot3 + GameMath.PI * 1.25f) * -Math.Abs(dev.AxisSign[0]), (rot3 + GameMath.PI * 1.25f) * -Math.Abs(dev.AxisSign[2]), axisCenter);
            float b5rotX = offX4 == 0 ? rot1 * 2f : 0f;
            float b5rotZ = offZ4 == 0 ? -rot1 * 2f : 0f;
            UpdateLightAndTransformMatrix(matrixAndLightFloats5.Values, index, distToCamera, dev.LightRgba, b5rotX, b5rotZ, new Vec3f(0.5f + offX4, 0.5f, 0.5f + offZ4), (float[])tmpMat.Clone());
        }

        /// <summary>
        /// Builds tmpMat representing the current world-space position of a sub-element
        /// before its own local rotation is applied. Pass the result as initialTransform to
        /// the overload of UpdateLightAndTransformMatrix that accepts an initial matrix.
        /// </summary>
        private void TransformMatrix(Vec3f distToCamera, float rotX, float rotZ, Vec3f axis)
        {
            Mat4f.Identity(tmpMat);
            Mat4f.Translate(tmpMat, tmpMat, distToCamera.X + axis.X, distToCamera.Y + axis.Y, distToCamera.Z + axis.Z);
            quat[0] = 0; quat[1] = 0; quat[2] = 0; quat[3] = 1;
            if (rotX != 0f) Quaterniond.RotateX(quat, quat, rotX);
            if (rotZ != 0f) Quaterniond.RotateZ(quat, quat, rotZ);
            Mat4f.MulQuat(tmpMat, quat);
            Mat4f.Translate(tmpMat, tmpMat, -axis.X, -axis.Y, -axis.Z);
        }

        /// <summary>
        /// When initialTransform is null: starts from Identity + camera translation.
        /// When initialTransform is provided: continues from that matrix (for compound-rotating sub-elements).
        /// </summary>
        protected void UpdateLightAndTransformMatrix(float[] values, int index, Vec3f distToCamera, Vec4f lightRgba, float rotX, float rotZ, Vec3f axis, float[] initialTransform)
        {
            if (initialTransform == null)
            {
                Mat4f.Identity(tmpMat);
                Mat4f.Translate(tmpMat, tmpMat, distToCamera.X + axis.X, distToCamera.Y + axis.Y, distToCamera.Z + axis.Z);
            }
            else
                Mat4f.Translate(tmpMat, tmpMat, axis.X, axis.Y, axis.Z);

            quat[0] = 0; quat[1] = 0; quat[2] = 0; quat[3] = 1;
            if (rotX != 0f) Quaterniond.RotateX(quat, quat, rotX);
            if (rotZ != 0f) Quaterniond.RotateZ(quat, quat, rotZ);

            Mat4f.MulQuat(tmpMat, quat);
            Mat4f.Translate(tmpMat, tmpMat, -axis.X, -axis.Y, -axis.Z);

            int j = index * 20;
            values[j] = lightRgba.R;
            values[++j] = lightRgba.G;
            values[++j] = lightRgba.B;
            values[++j] = lightRgba.A;
            for (int i = 0; i < 16; i++) values[++j] = tmpMat[i];
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            if (quantityBlocks > 0)
            {
                RenderGroups(prog, meshGroups1, matrixAndLightFloats1, quantityBlocks);
                RenderGroups(prog, meshGroups2, matrixAndLightFloats2, quantityBlocks);
                RenderGroups(prog, meshGroups3, matrixAndLightFloats3, quantityBlocks);

                // Both spinbar balls share the same mesh geometry (meshGroups4) but have
                // independent transform buffers, so we render the groups twice.
                RenderGroups(prog, meshGroups4, matrixAndLightFloats4, quantityBlocks);
                RenderGroups(prog, meshGroups4, matrixAndLightFloats5, quantityBlocks);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            DisposeGroups(meshGroups1);
            DisposeGroups(meshGroups2);
            DisposeGroups(meshGroups3);
            DisposeGroups(meshGroups4);
            // meshGroups5 does not exist: it reuses meshGroups4's MeshRefs, already disposed above
        }
    }
}
