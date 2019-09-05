using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public abstract class MechBlockRenderer
    {
        protected ICoreClientAPI capi;

        protected MeshData updateMesh = new MeshData();

        protected int quantityBlocks = 0;

        protected float[] tmpMat = Mat4f.Create();
        protected double[] quat = Quaterniond.Create();
        protected float[] qf = new float[4];
        protected float[] rotMat = Mat4f.Create();
        protected MechanicalPowerMod mechanicalPowerMod;

        protected List<IMechanicalPowerNode> renderedDevices = new List<IMechanicalPowerNode>();

        protected Vec3f tmp = new Vec3f();
        protected float[] testRot = new float[3];


        public MechBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod)
        {
            this.mechanicalPowerMod = mechanicalPowerMod;
            this.capi = capi;
        }
        

        public void AddDevice(IMechanicalPowerNode device)
        {
            quantityBlocks++;
            renderedDevices.Add(device);
        }

        public void RemoveDevice(IMechanicalPowerNode device)
        {
            quantityBlocks--;
            renderedDevices.Remove(device);
        }




        protected void UpdateCustomFloatBuffer()
        {
            Vec3d pos = capi.World.Player.Entity.CameraPos;
            

            for (int i = 0; i < renderedDevices.Count; i++)
            {
                IMechanicalPowerNode dev = renderedDevices[i];

                tmp.Set((float)(dev.Position.X - pos.X), (float)(dev.Position.Y - pos.Y), (float)(dev.Position.Z - pos.Z));

                testRot[2] = dev.Angle; // -(capi.World.ElapsedMilliseconds / 900f) % GameMath.TWOPI;

                UpdateLightAndTransformMatrix(i, tmp, testRot, dev);
            }
        }

        protected abstract void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float[] rot, IMechanicalPowerNode dev);


        protected virtual void UpdateLightAndTransformMatrix(float[] values, int index, Vec3f distToCamera, Vec4f lightRgba, float rotX, float rotY, float rotZ)
        {
            Mat4f.Identity(tmpMat);

            Mat4f.Translate(tmpMat, tmpMat, distToCamera.X, distToCamera.Y, distToCamera.Z);

            Mat4f.Translate(tmpMat, tmpMat, 0.5f, 0.5f, 0.5f);

            quat[0] = 0;
            quat[1] = 0;
            quat[2] = 0;
            quat[3] = 1;
            Quaterniond.RotateX(quat, quat, rotX);
            Quaterniond.RotateY(quat, quat, rotY);
            Quaterniond.RotateZ(quat, quat, rotZ);

            for (int i = 0; i < quat.Length; i++) qf[i] = (float)quat[i];
            Mat4f.Mul(tmpMat, tmpMat, Mat4f.FromQuat(rotMat, qf));

            Mat4f.Translate(tmpMat, tmpMat, -0.5f, -0.5f, -0.5f);

            values[index * 20] = lightRgba.R;
            values[index * 20 + 1] = lightRgba.G;
            values[index * 20 + 2] = lightRgba.B;
            values[index * 20 + 3] = lightRgba.A;

            for (int i = 0; i < 16; i++)
            {
                values[index * 20 + i + 4] = tmpMat[i];
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

