using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class ClutchBlockRenderer : MechBlockRenderer
    {
        readonly MaterialMeshPool armPool;
        readonly MaterialMeshPool drumPool;
        readonly float[] lightAndTransform = new float[InstanceDataSize];
        readonly Vec2f materialUvOffset = new();
        readonly MechanicalMaterialProperties materialProperties;

        public ClutchBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            materialProperties = (textureSoureBlock as BlockClutch)?.MaterialProperties;
            Shape armShape = API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/wood/mechanics/clutch-arm.json"));
            Vec3f rotation = new(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);
            armPool = createMaterialMeshPool(textureSoureBlock, armShape, rotation, materialProperties: materialProperties);
            try
            {
                Shape drumShape = API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/wood/mechanics/clutch-drum.json"));
                drumPool = createMaterialMeshPool(textureSoureBlock, drumShape, rotation, materialProperties: materialProperties);
            }
            catch
            {
                disposeMaterialMeshPool(armPool);
                throw;
            }
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
            UpdateLightAndTransformMatrix(lightAndTransform, 0, distToCamera, dev.LightRgba, rotX, rotY, rotZ, clutch.hinge, null);
            updateMaterialInstances(armPool, clutch, index);

            Vec3f axis = new Vec3f(0.5f - dev.AxisSign[2] * 0.125f, 0.625f, 0.5f + dev.AxisSign[0] * 0.125f);
            rotX = rot2 * dev.AxisSign[0];
            rotY = rot2 * dev.AxisSign[1];
            rotZ = rot2 * dev.AxisSign[2];
            UpdateLightAndTransformMatrix(lightAndTransform, 0, distToCamera, dev.LightRgba, rotX, rotY, rotZ, axis, tmpMat);
            updateMaterialInstances(drumPool, clutch, index);
        }

        void updateMaterialInstances(MaterialMeshPool pool, BEClutch clutch, int index)
        {
            foreach (var piece in pool.Pieces)
            {
                materialUvOffset.X = materialUvOffset.Y = 0;
                if (piece.MaterialTextureCode != null) materialProperties?.GetMaterialUvOffset(capi, clutch.PlacedStack, piece.MaterialTextureCode, materialUvOffset);
                setMaterialInstance(piece.Floats.Values, index, lightAndTransform, materialUvOffset);
            }
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
            //if (xtraTransform != null) Mat4f.Mul(tmpMat, tmpMat, xtraTransform);

            int j = index * 20;
            values[j] = lightRgba.R;
            values[++j] = lightRgba.G;
            values[++j] = lightRgba.B;
            values[++j] = lightRgba.A;

            for (int i = 0; i < 16; i++)
            {
                values[++j] = tmpMat[i];
            }
            return tmpMat;
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            if (quantityBlocks > 0)
            {
                armPool.Count = drumPool.Count = quantityBlocks;
                renderMaterialMeshPool(prog, armPool);
                renderMaterialMeshPool(prog, drumPool);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            disposeMaterialMeshPool(armPool);
            disposeMaterialMeshPool(drumPool);
        }
    }
}
