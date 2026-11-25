using System;
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
        MultiTextureMeshRef blockMeshRef1;
        MultiTextureMeshRef blockMeshRef2;
        MultiTextureMeshRef blockMeshRef3;
        MultiTextureMeshRef blockMeshRef4;
        Vec3f axisCenter = new Vec3f(0.5f, 0.5f, 0.5f);

        public CreativeRotorRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {

            AssetLocation loc = new AssetLocation("shapes/block/metal/mechanics/creativerotor-axle.json");

            Shape shape = API.Common.Shape.TryGet(capi, loc);
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            capi.Tesselator.TesselateShape(textureSoureBlock, shape, out MeshData blockMesh1, rot);

            rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);
            Shape ovshape = API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/metal/mechanics/creativerotor-contra.json"));
            capi.Tesselator.TesselateShape(textureSoureBlock, ovshape, out MeshData blockMesh2, rot);
            Shape ovshape2 = API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/metal/mechanics/creativerotor-spinbar.json"));
            capi.Tesselator.TesselateShape(textureSoureBlock, ovshape2, out MeshData blockMesh3, rot);
            Shape ovshape3 = API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/metal/mechanics/creativerotor-spinball.json"));
            capi.Tesselator.TesselateShape(textureSoureBlock, ovshape3, out MeshData blockMesh4, rot);

            //blockMesh1.Rgba2 = null;
            //blockMesh2.Rgba2 = null;
            //blockMesh3.Rgba2 = null;
            //blockMesh4.Rgba2 = null;

            int count = (16 + 4) * 2100;
            matrixAndLightFloats1 = createCustomFloats(count);
            matrixAndLightFloats2 = createCustomFloats(count);
            matrixAndLightFloats3 = createCustomFloats(count);
            matrixAndLightFloats4 = createCustomFloats(count);
            matrixAndLightFloats5 = createCustomFloats(count);

            this.blockMeshRef1 = uploadInstancedMesh(blockMesh1, matrixAndLightFloats1);
            this.blockMeshRef2 = uploadInstancedMesh(blockMesh2, matrixAndLightFloats2);
            this.blockMeshRef3 = uploadInstancedMesh(blockMesh3, matrixAndLightFloats3);
            this.blockMeshRef4 = uploadInstancedMesh(blockMesh4, matrixAndLightFloats4);
        }

        private CustomMeshDataPartFloat createCustomFloats(int count)
        {
            CustomMeshDataPartFloat result = new CustomMeshDataPartFloat(count)
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

            //axle
            float rotX = rot1 * axX;
            float rotZ = rot1 * axZ;
            UpdateLightAndTransformMatrix(matrixAndLightFloats1.Values, index, distToCamera, dev.LightRgba, rotX, rotZ, axisCenter, null);

            //contra-rotating axle parts
            rotX = rot2 * axX;
            rotZ = rot2 * axZ;
            UpdateLightAndTransformMatrix(matrixAndLightFloats2.Values, index, distToCamera, dev.LightRgba, rotX, rotZ, axisCenter, null);

            //the spin bar
            rotX = rot3 * axX;
            rotZ = rot3 * axZ;
            UpdateLightAndTransformMatrix(matrixAndLightFloats3.Values, index, distToCamera, dev.LightRgba, rotX, rotZ, axisCenter, null);

            //position the ball on the spin bar (45 degrees ahead of the spinbar)
            rotX = (rot3 + GameMath.PI / 4) * axX;
            rotZ = (rot3 + GameMath.PI / 4) * axZ;
            TransformMatrix(distToCamera, rotX, rotZ, axisCenter);

            rotX = axX == 0 ? rot1 * 2f : 0f;
            rotZ = axZ == 0 ? -rot1 * 2f : 0f;
            axX = dev.AxisSign[0] * 0.05f;
            axZ = dev.AxisSign[2] * 0.05f;
            UpdateLightAndTransformMatrix(matrixAndLightFloats4.Values, index, distToCamera, dev.LightRgba, rotX, rotZ, new Vec3f(0.5f + axX, 0.5f, 0.5f + axZ), (float[])tmpMat.Clone());

            //position the other ball on the spin bar (225 degrees ahead of the spinbar i.e. opposite side from the first one)
            rotX = (rot3 + GameMath.PI * 1.25f) * -Math.Abs(dev.AxisSign[0]);
            rotZ = (rot3 + GameMath.PI * 1.25f) * -Math.Abs(dev.AxisSign[2]);
            TransformMatrix(distToCamera, rotX, rotZ, axisCenter);

            rotX = axX == 0 ? rot1 * 2f : 0f;
            rotZ = axZ == 0 ? -rot1 * 2f : 0f;
            UpdateLightAndTransformMatrix(matrixAndLightFloats5.Values, index, distToCamera, dev.LightRgba, rotX, rotZ, new Vec3f(0.5f + axX, 0.5f, 0.5f + axZ), (float[])tmpMat.Clone());
        }

        /// <summary>
        /// Set up tmpMat - expected to be used with a later call to UpdateLightAndTransformMatrix passing in tmpMat as an initial matrix
        /// </summary>
        private void TransformMatrix(Vec3f distToCamera, float rotX, float rotZ, Vec3f axis)
        {
            Mat4f.Identity(tmpMat);
            Mat4f.Translate(tmpMat, tmpMat, distToCamera.X + axis.X, distToCamera.Y + axis.Y, distToCamera.Z + axis.Z);
            quat[0] = 0;
            quat[1] = 0;
            quat[2] = 0;
            quat[3] = 1;
            if (rotX != 0f) Quaterniond.RotateX(quat, quat, rotX);
            if (rotZ != 0f) Quaterniond.RotateZ(quat, quat, rotZ);
            Mat4f.MulQuat(tmpMat, quat);
            Mat4f.Translate(tmpMat, tmpMat, -axis.X, -axis.Y, -axis.Z);
        }

        /// <summary>
        /// The initialTransform parameter is either null, to start with the Identity matrix and apply the camera transform, or for movable+rotating sub-elements can pass in an existing matrix which represents the current positioning of the sub-element (prior to the sub-element's own rotation)
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

            quat[0] = 0;
            quat[1] = 0;
            quat[2] = 0;
            quat[3] = 1;
            if (rotX != 0f) Quaterniond.RotateX(quat, quat, rotX);
            if (rotZ != 0f) Quaterniond.RotateZ(quat, quat, rotZ);

            Mat4f.MulQuat(tmpMat, quat);

            Mat4f.Translate(tmpMat, tmpMat, -axis.X, -axis.Y, -axis.Z);

            int j = index * 20;
            values[j] = lightRgba.R;
            values[++j] = lightRgba.G;
            values[++j] = lightRgba.B;
            values[++j] = lightRgba.A;

            for (int i = 0; i < 16; i++)
            {
                values[++j] = tmpMat[i];
            }
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            if (quantityBlocks > 0)
            {
                renderInstancedMesh(prog, blockMeshRef1, matrixAndLightFloats1, quantityBlocks);
                renderInstancedMesh(prog, blockMeshRef2, matrixAndLightFloats2, quantityBlocks);
                renderInstancedMesh(prog, blockMeshRef3, matrixAndLightFloats3, quantityBlocks);

                //Sub elements 4 and 5 are the two spinbar balls, each has the exact same mesh (blockMeshRef4) but a different transform
                renderInstancedMesh(prog, blockMeshRef4, matrixAndLightFloats4, quantityBlocks);
                renderInstancedMesh(prog, blockMeshRef4, matrixAndLightFloats5, quantityBlocks);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            blockMeshRef1?.Dispose();
            blockMeshRef2?.Dispose();
            blockMeshRef3?.Dispose();
            blockMeshRef4?.Dispose();
        }
    }
}
