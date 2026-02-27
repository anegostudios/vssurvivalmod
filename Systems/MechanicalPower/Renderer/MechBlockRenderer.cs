using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public abstract class MechBlockRenderer
    {
        protected ICoreClientAPI capi;

        protected MeshData updateMesh = new MeshData();

        protected int quantityBlocks = 0;

        protected float[] tmpMat = Mat4f.Create();
        protected double[] quat = Quaterniond.Create();
        protected float[] rotMat = Mat4f.Create();
        protected MechanicalPowerMod mechanicalPowerMod;

        protected Dictionary<BlockPos, IMechanicalPowerRenderable> renderedDevices = new Dictionary<BlockPos, IMechanicalPowerRenderable>();

        protected Vec3f tmp = new Vec3f();

        public MechBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod)
        {
            this.mechanicalPowerMod = mechanicalPowerMod;
            this.capi = capi;
        }
        
        public void AddDevice(IMechanicalPowerRenderable device)
        {
            renderedDevices[device.Position] = device;
            quantityBlocks = renderedDevices.Count;
        }

        public bool RemoveDevice(IMechanicalPowerRenderable device)
        {
            bool ok = renderedDevices.Remove(device.Position);
            quantityBlocks = renderedDevices.Count;
            return ok;
        }

        protected virtual void UpdateCustomFloatBuffer()
        {
            Vec3d pos = capi.World.Player.Entity.CameraPos;

            int i = 0;
            foreach (var dev in renderedDevices.Values)
            {
                //double precision int-double subtraction is needed here (even though the desired result is a float).  
                // It's needed to have enough significant figures in the result, as the integer size could be large e.g. 50000 but the difference should be small (can easily be less than 5)
                tmp.Set((float)(dev.Position.X - pos.X), (float)(dev.Position.InternalY - pos.Y), (float)(dev.Position.Z - pos.Z));  

                UpdateLightAndTransformMatrix(i, tmp, dev.AngleRad % GameMath.TWOPI, dev);
                i++;
            }
        }

        protected abstract void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotRad, IMechanicalPowerRenderable dev);

        protected virtual void UpdateLightAndTransformMatrix(float[] values, int index, Vec3f distToCamera, Vec4f lightRgba, float rotX, float rotY, float rotZ)
        {
            Mat4f.Identity(tmpMat);

            Mat4f.Translate(tmpMat, tmpMat, distToCamera.X + 0.5f, distToCamera.Y + 0.5f, distToCamera.Z + 0.5f);

            quat[0] = 0;
            quat[1] = 0;
            quat[2] = 0;
            quat[3] = 1;
            if (rotX != 0f) Quaterniond.RotateX(quat, quat, rotX);
            if (rotY != 0f) Quaterniond.RotateY(quat, quat, rotY);
            if (rotZ != 0f) Quaterniond.RotateZ(quat, quat, rotZ);

            Mat4f.MulQuat(tmpMat, quat);

            Mat4f.Translate(tmpMat, tmpMat, -0.5f, -0.5f, -0.5f);

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


        public virtual void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();
        }

        public virtual void Dispose()
        {

        }
    }
}

