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

    public delegate void CalcMat(int index, Vec3f distToPlayer, Vec3f rotation, Vec4f lightRgba);


    /// <summary>
    /// 1. Use instanced rendering to issue one draw call for all mech.power blocks of one type. (i.e. 1 draw call for all axles)
    /// 2. Each block type has only 1 default shape per orientation. Different orientations are made with transformation matrixes
    /// 3. Each frame, upload a transformation matrix for the full block
    /// </summary>
    public class MechNetworkRenderer : IRenderer
    {
        ICoreClientAPI capi;

        MeshRef gearboxCage;
        MeshRef gearboxPeg;
        MeshRef axle;
        MeshRef windmillRotor;

        MeshData updateMesh = new MeshData();

        CustomMeshDataPartFloat floatsPeg;
        CustomMeshDataPartFloat floatsCage;
        CustomMeshDataPartFloat floatsAxle;
        CustomMeshDataPartFloat floatsWindmillRotor;

        int quantityGearboxes = 0;
        int quantityAxles = 0;
        int quantityRotors = 0;

        float[] tmpMat = Mat4f.Create();
        double[] quat = Quaternion.Create();
        float[] qf = new float[4];
        float[] rotMat = Mat4f.Create();
        private MechanicalPowerMod mechanicalPowerMod;

        List<IMechanicalPowerDeviceVS> renderedDevices = new List<IMechanicalPowerDeviceVS>();


        CalcMat[] calcs = new CalcMat[3];
        Vec3f tmp = new Vec3f();
        IShaderProgram prog;

        public MechNetworkRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod)
        {
            calcs[0] = UpdateGearbox;
            calcs[1] = UpdateAxle;
            calcs[2] = UpdateWindmillRotor;

            this.mechanicalPowerMod = mechanicalPowerMod;
            this.capi = capi;

            Block block = capi.World.GetBlock(new AssetLocation("gearbox-wood"));
            if (block == null) return;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
            capi.Event.RegisterReloadShaders(LoadShader);
            LoadShader();

            MeshData gearboxCageMesh;
            MeshData gearboxPegMesh;
            MeshData axleMesh;
            MeshData windmillRotorMesh;

            capi.Tesselator.TesselateShape(block, capi.Assets.TryGet("shapes/block/wood/angledgearbox-cage.json").ToObject<Shape>(), out gearboxCageMesh);
            capi.Tesselator.TesselateShape(block, capi.Assets.TryGet("shapes/block/wood/angledgearbox-peg.json").ToObject<Shape>(), out gearboxPegMesh);
            capi.Tesselator.TesselateShape(block, capi.Assets.TryGet("shapes/block/wood/axle.json").ToObject<Shape>(), out axleMesh);
            capi.Tesselator.TesselateShape(block, capi.Assets.TryGet("shapes/block/wood/windmillrotor.json").ToObject<Shape>(), out windmillRotorMesh);

            gearboxPegMesh.Rgba2 = null;
            gearboxCageMesh.Rgba2 = null;
            axleMesh.Rgba2 = null;
            windmillRotorMesh.Rgba2 = null;


            // 16 floats matrix, 4 floats light rgbs
            gearboxPegMesh.CustomFloats = floatsPeg = new CustomMeshDataPartFloat((16+4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4*16,
                StaticDraw = false,
            };
            gearboxPegMesh.CustomFloats.SetAllocationSize((16+4) * 10100);

            gearboxCageMesh.CustomFloats = floatsCage = floatsPeg.Clone();
            axleMesh.CustomFloats = floatsAxle = floatsPeg.Clone();
            windmillRotorMesh.CustomFloats = floatsWindmillRotor = floatsPeg.Clone();

            

            this.gearboxPeg = capi.Render.UploadMesh(gearboxPegMesh);
            this.gearboxCage = capi.Render.UploadMesh(gearboxCageMesh);
            this.axle = capi.Render.UploadMesh(axleMesh);
            this.windmillRotor = capi.Render.UploadMesh(windmillRotorMesh);
        }

        public bool LoadShader()
        {
            prog = capi.Shader.NewShaderProgram();

            prog.VertexShader = capi.Shader.NewShader(EnumShaderType.VertexShader);
            prog.FragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);

            capi.Shader.RegisterFileShaderProgram("mechpower", prog);

            prog.PrepareUniformLocations("rgbaFogIn", "rgbaAmbientIn", "fogMinIn", "fogDensityIn", "projectionMatrix", "modelViewMatrix", "tex");
            return prog.Compile();
        }


        public void AddDevice(IMechanicalPowerDeviceVS device)
        {
            if (device.Type == MechDeviceType.Gearbox) quantityGearboxes++;
            if (device.Type == MechDeviceType.Axle) quantityAxles++;
            if (device.Type == MechDeviceType.Windmillrotor) quantityRotors++;

            renderedDevices.Add(device);
        }

        public void RemoveDevice(IMechanicalPowerDeviceVS device)
        {
            if (device.Type == MechDeviceType.Gearbox) quantityGearboxes--;
            if (device.Type == MechDeviceType.Axle) quantityAxles--;
            if (device.Type == MechDeviceType.Windmillrotor) quantityRotors--;

            renderedDevices.Remove(device);
        }


        Vec3f testRot = new Vec3f();


        private void UpdateCustomFloatBuffer()
        {
            Vec3d pos = capi.World.Player.Entity.CameraPos;
            testRot.Z = -(capi.World.ElapsedMilliseconds / 900f) % GameMath.TWOPI;

            for (int i = 0; i < renderedDevices.Count; i++)
            {
                IMechanicalPowerDeviceVS dev = renderedDevices[i];

                tmp.Set((float)(dev.Position.X - pos.X), (float)(dev.Position.Y - pos.Y), (float)(dev.Position.Z - pos.Z));

                calcs[(int)dev.Type](i, tmp, testRot, dev.LightRgba);
            }
        }

        void UpdateGearbox(int index, Vec3f distToCamera, Vec3f rotation, Vec4f lightRgba)
        {
            Update(floatsPeg.Values, index, distToCamera, lightRgba, rotation.X, rotation.Y, rotation.Z - 0.08f);
            Update(floatsCage.Values, index, distToCamera, lightRgba, rotation.Z, rotation.Y, rotation.X);
        }
        
        void UpdateAxle(int index, Vec3f distToCamera, Vec3f rotation, Vec4f lightRgba)
        {
            Update(floatsAxle.Values, index, distToCamera, lightRgba, rotation.X, rotation.Y, rotation.Z);
        }

        void UpdateWindmillRotor(int index, Vec3f distToCamera, Vec3f rotation, Vec4f lightRgba)
        {
            Update(floatsWindmillRotor.Values, index, distToCamera, lightRgba, rotation.X, rotation.Y, rotation.Z);
        }

        void Update(float[] values, int index, Vec3f distToCamera, Vec4f lightRgba, float rotX, float rotY, float rotZ)
        {
            Mat4f.Identity(tmpMat);

            Mat4f.Translate(tmpMat, tmpMat, distToCamera.X, distToCamera.Y, distToCamera.Z);

            Mat4f.Translate(tmpMat, tmpMat, 0.5f, 0.5f, 0.5f);

            quat[0] = 0;
            quat[1] = 0;
            quat[2] = 0;
            quat[3] = 1;
            Quaternion.RotateX(quat, quat, rotX);
            Quaternion.RotateY(quat, quat, rotY);
            Quaternion.RotateZ(quat, quat, rotZ);

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


        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            UpdateCustomFloatBuffer();

            prog.Use();
            prog.BindTexture2D("tex", capi.BlockTextureAtlas.Positions[0].atlasTextureId, 0);
            prog.Uniform("rgbaFogIn", capi.Render.FogColor);
            prog.Uniform("rgbaAmbientIn", capi.Render.AmbientColor);
            prog.Uniform("fogMinIn", capi.Render.FogMin);
            prog.Uniform("fogDensityIn", capi.Render.FogDensity);
            prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
            prog.UniformMatrix("modelViewMatrix", capi.Render.CameraMatrixOriginf);

            if (quantityGearboxes > 0)
            {
                floatsPeg.Count = quantityGearboxes * 20;
                floatsCage.Count = quantityGearboxes * 20;

                updateMesh.CustomFloats = floatsPeg;
                capi.Render.UpdateMesh(gearboxPeg, updateMesh);

                updateMesh.CustomFloats = floatsCage;
                capi.Render.UpdateMesh(gearboxCage, updateMesh);

                capi.Render.RenderMeshInstanced(gearboxPeg, quantityGearboxes);
                capi.Render.RenderMeshInstanced(gearboxCage, quantityGearboxes);
            }

            prog.Stop();

            /*capi.Render.RenderMeshInstanced(gearboxCage, quantityGearboxes);
            capi.Render.RenderMeshInstanced(axle, quantityAxles);
            capi.Render.RenderMeshInstanced(windmillRotor, quantityRotors);*/
        }

        public double RenderOrder
        {
            get
            {
                return 0.5;
            }
        }

        public int RenderRange
        {
            get { return 100; }
        }

        public void Dispose()
        {
            gearboxCage?.Dispose();
            gearboxPeg?.Dispose();
            axle?.Dispose();
            windmillRotor?.Dispose();
        }

    }
}

