using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// 1. Use instanced rendering to issue one draw call for all fruit with the same mesh
    /// 2. Each fruit has 1 shape. Different positions and orientations are made with transformation matrixes
    /// 3. Each frame, upload a transformation matrix
    /// </summary>
    public class FruitRendererSystem : IRenderer
    {
        ICoreClientAPI capi;
        IShaderProgram prog;

        Dictionary<AssetLocation, FruitRenderer> renderers = new Dictionary<AssetLocation, FruitRenderer>();



        public FruitRendererSystem(ICoreClientAPI capi)
        {
            this.capi = capi;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "fruit");

            // This shader is created by the essentials mod in Core.cs
            prog = capi.Shader.GetProgramByName("instanced");
        }


        /// <summary>
        /// Add a fruit to render, with its germination date in gametime  (allows for it to be grown, transitioned etc)
        /// </summary>
        public void AddFruit(Item fruit, Vec3d position, FruitData data)
        {
            if (fruit.Shape == null) return;

            if (!renderers.TryGetValue(fruit.Code, out FruitRenderer renderer))
            {
                renderer = new FruitRenderer(capi, fruit);
                renderers.Add(fruit.Code, renderer);
            }

            renderer.AddFruit(position, data);
        }



        public void RemoveFruit(Item fruit, Vec3d position)
        {
            renderers.TryGetValue(fruit.Code, out FruitRenderer renderer);
            if (renderer != null)
            {
                renderer.RemoveFruit(position);
            }
        }


        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (prog.Disposed) prog = capi.Shader.GetProgramByName("instanced");

            capi.Render.GlDisableCullFace();

            if (stage == EnumRenderStage.Opaque)
            {
                capi.Render.GlToggleBlend(false); // Seems to break SSAO without
                prog.Use();
                prog.BindTexture2D("tex", capi.ItemTextureAtlas.Positions[0].atlasTextureId, 0);
                prog.Uniform("rgbaFogIn", capi.Render.FogColor);
                prog.Uniform("rgbaAmbientIn", capi.Render.AmbientColor);
                prog.Uniform("fogMinIn", capi.Render.FogMin);
                prog.Uniform("fogDensityIn", capi.Render.FogDensity);
                prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
                prog.UniformMatrix("modelViewMatrix", capi.Render.CameraMatrixOriginf);

                foreach (var val in renderers)
                {
                    val.Value.OnRenderFrame(deltaTime, prog);
                }

                prog.Stop();
            }

            capi.Render.GlEnableCullFace();
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
            get { return 80; }
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            foreach (var val in renderers)
            {
                val.Value.Dispose();
            }
        }

    }

    public class FruitRenderer
    {
        Dictionary<Vec3d, FruitData> positions = new Dictionary<Vec3d, FruitData>();
        bool onGround = false;
        protected ICoreClientAPI capi;
        protected MeshData itemMesh;
        protected MeshRef meshref;
        CustomMeshDataPartFloat matrixAndLightFloats;

        protected Vec3f tmp = new Vec3f();
        protected float[] tmpMat = Mat4f.Create();
        protected double[] quat = Quaterniond.Create();
        protected float[] qf = new float[4];
        protected float[] rotMat = Mat4f.Create();

        static Vec3f noRotation = new Vec3f(0, 0, 0);
        Vec3f v = new Vec3f();

        static int nextID = 0;
        private int id = 0;

        public FruitRenderer(ICoreClientAPI capi, Item item)
        {
            this.capi = capi;
            this.id = nextID++;

            CompositeShape shapeLoc = item.Shape;
            if (item.Attributes != null && item.Attributes["onGround"].AsBool(false)) onGround = true;
            AssetLocation loc = shapeLoc.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            Shape shape = API.Common.Shape.TryGet(capi, loc);
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            capi.Tesselator.TesselateShape(item, shape, out itemMesh, rot, null, shapeLoc.SelectiveElements);

            //itemMesh.SetVertexFlags(0x8000800);

            itemMesh.CustomFloats = matrixAndLightFloats = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            itemMesh.CustomFloats.SetAllocationSize((16 + 4) * 10100);

            meshref = capi.Render.UploadMesh(itemMesh);
        }

        internal void Dispose()
        {
            meshref?.Dispose();
        }


        internal void AddFruit(Vec3d position, FruitData data)
        {
            positions[position] = data;
        }

        internal void RemoveFruit(Vec3d position)
        {
            positions.Remove(position);
        }

        internal void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            if (positions.Count > 0)
            {
                matrixAndLightFloats.Count = positions.Count * 20;
                itemMesh.CustomFloats = matrixAndLightFloats;
                capi.Render.UpdateMesh(meshref, itemMesh);
                capi.Render.RenderMeshInstanced(meshref, positions.Count);
            }
        }

        protected virtual void UpdateCustomFloatBuffer()
        {
            Vec3d camera = capi.World.Player.Entity.CameraPos;
            float windSpeed = API.Config.GlobalConstants.CurrentWindSpeedClient.X;
            float windWaveIntensity = 1.0f;
            float div = 30 * 3.5f;  //weakwave

            DefaultShaderUniforms shUniforms = capi.Render.ShaderUniforms;

            float wwaveHighFreq = shUniforms.WindWaveCounterHighFreq;
            float counter = shUniforms.WindWaveCounter;

            int i = 0;
            foreach (var fruit in positions)
            {
                Vec3d pos = fruit.Key;
                Vec3f rot = fruit.Value.rotation;
                double posX = pos.X;
                double posY = pos.Y;
                double posZ = pos.Z;
                float rotY = rot.Y;
                float rotX = rot.X;
                float rotZ = rot.Z;

                if (onGround)
                {
                    BlockPos blockPos = fruit.Value.behavior.Blockentity.Pos;
                    posY = blockPos.Y - 0.0625;
                    posX += 1.1 * (posX - blockPos.X - 0.5);  //fruit on ground positioned further out from the plant center
                    posZ += 1.1 * (posZ - blockPos.Z - 0.5);
                    rot = noRotation;
                    rotY = (float)((posX + posZ) * 40 % 90);  //some random rotation
                }
                else
                {
                    // Apply windwave

                    // Precisely replicate the effects of vertexwarp.vsh ... except where noted in comments below!
                    double x = posX;
                    double y = posY;
                    double z = posZ;
                    float heightBend = 0.7f * (0.5f + (float)y - (int)y);
                    double strength = windWaveIntensity * (1 + windSpeed) * (0.5 + (posY - fruit.Value.behavior.Blockentity.Pos.Y)) / 2.0;  // reduce the strength for lower fruit

                    v.Set((float)x % 4096f / 10, (float)z % 4096f / 10, counter % 1024f / 4);
                    float bendNoise = windSpeed * 0.2f + 1.4f * gnoise(v);

                    float bend = windSpeed * (0.8f + bendNoise) * heightBend * windWaveIntensity;
                    bend = Math.Min(4, bend) * 0.2857143f / 2.8f;    //no idea why this reduction by a factor of approximately 10 is needed, but it looks right

                    x += wwaveHighFreq;
                    y += wwaveHighFreq;
                    z += wwaveHighFreq;
                    strength *= 0.25f;  // reduced strength because it looks right (fruits are less wobbly and closer to the center of the plant, compared with the foliage texture vertex tips)
                    double dx = strength * (Math.Sin(x * 10) / 120 + (2 * Math.Sin(x / 2) + Math.Sin(x + y) + Math.Sin(0.5 + 4 * x + 2 * y) + Math.Sin(1 + 6 * x + 3 * y) / 3) / div);
                    double dz = strength * ((2 * Math.Sin(z / 4) + Math.Sin(z + 3 * y) + Math.Sin(0.5 + 4 * z + 2 * y) + Math.Sin(1 + 6 * z + y) / 3) / div);
                    posX += dx;
                    posY += strength * (Math.Sin(5 * y) / 15 + Math.Cos(10 * x) / 10 + Math.Sin(3 * z) / 2 + Math.Cos(x * 2) / 2.2) / div;
                    posZ += dz;

                    // Also apply a small wind effect to the rotation, otherwise the fruits look 'stiff' because they remain upright
                    rotX += (float)(dz * 6 + bend / 2);
                    rotZ += (float)(dx * 6 + bend / 2);
                    posX += bend;
                }

                //double precision subtraction is needed here (even though the desired result is a float).  
                // It's needed to have enough significant figures in the result, as the integer size could be large e.g. 50000 but the difference should be small (can easily be less than 5)
                tmp.Set((float)(posX - camera.X), (float)(posY - camera.Y), (float)(posZ - camera.Z));

                UpdateLightAndTransformMatrix(matrixAndLightFloats.Values, i, tmp, fruit.Value.behavior.LightRgba, rotX, rotY, rotZ);
                i++;
            }
        }

        //const mat3 m = mat3(127.1, 311.7, 74.7,
        //                    269.5, 183.3, 246.1,
        //                    113.5, 271.9, 124.6);

        /// <summary>
        /// Ghash and DotProduct in a single method, so that it can return a float.  Does not alter p or q!
        /// </summary>
        private float ghashDot(Vec3f p, Vec3f q, float oX, float oY, float oZ)
        {
            float qX = q.X - oX;
            float qY = q.Y - oY;
            float qZ = q.Z - oZ;
            oX += p.X;
            oY += p.Y;
            oZ += p.Z;
            float pX = 127.1f * oX + 311.7f * oY + 74.7f * oZ;
            float pY = 269.5f * oX + 183.3f * oY + 246.1f * oZ;
            float pZ = 113.5f * oX + 271.9f * oY + 124.6f * oZ;

            return (float)(qX * (-1.0 + 2.0 * fract(GameMath.Mod(((pX * 0.025f) + 8.0f) * pX, 289.0f) / 41.0))
                         + qY * (-1.0 + 2.0 * fract(GameMath.Mod(((pY * 0.025f) + 8.0f) * pY, 289.0f) / 41.0))
                         + qZ * (-1.0 + 2.0 * fract(GameMath.Mod(((pZ * 0.025f) + 8.0f) * pZ, 289.0f) / 41.0)));
        }

        private double fract(double v)
        {
            return v - Math.Floor(v);
        }

        Vec3f i = new Vec3f();
        Vec3f f = new Vec3f();

        private float gnoise(Vec3f p)
        {
            int ix = (int)p.X;
            int iy = (int)p.Y;
            int iz = (int)p.Z;
            i.Set(ix, iy, iz);
            f.Set(p.X - ix, p.Y - iy, p.Z - iz);

            float ux = f.X * f.X * (3.0f - 2.0f * f.X);
            float uy = f.Y * f.Y * (3.0f - 2.0f * f.Y);
            float uz = f.Z * f.Z * (3.0f - 2.0f * f.Z);

            float ab1 = ghashDot(i, f, 0, 0, 0);
            float ab2 = ghashDot(i, f, 0, 0, 1);
            float at1 = ghashDot(i, f, 0, 1, 0);
            float at2 = ghashDot(i, f, 0, 1, 1);
            float bb1 = ghashDot(i, f, 1, 0, 0);
            float bb2 = ghashDot(i, f, 1, 0, 1);
            float bt1 = ghashDot(i, f, 1, 1, 0);
            float bt2 = ghashDot(i, f, 1, 1, 1);

            float rg1 = mix(mix(ab1, bb1, ux), mix(at1, bt1, ux), uy);
            float rg2 = mix(mix(ab2, bb2, ux), mix(at2, bt2, ux), uy);

            return 1.2f * mix(rg1, rg2, uz);
        }

        private float mix(float x, float y, float a)
        {
            return x * (1f - a) + y * a;
        }

        protected virtual void UpdateLightAndTransformMatrix(float[] values, int index, Vec3f distToCamera, Vec4f lightRgba, float rotX, float rotY, float rotZ)
        {
            // This commented-out code could allow individual fruits to change size as they grow, in future: But ... Saraty asked not to implement because the pixel density will change

            //float scale = 1f;
            //Mat4f.Identity_Scaled(tmpMat, scale);
            //Mat4f.Translate(tmpMat, tmpMat, distToCamera.X / scale, distToCamera.Y / scale, distToCamera.Z / scale);

            Mat4f.Identity(tmpMat);
            Mat4f.Translate(tmpMat, tmpMat, distToCamera.X, distToCamera.Y, distToCamera.Z);

            quat[0] = 0;
            quat[1] = 0;
            quat[2] = 0;
            quat[3] = 1;
            if (rotX != 0f) Quaterniond.RotateX(quat, quat, rotX);
            if (rotY != 0f) Quaterniond.RotateY(quat, quat, rotY);
            if (rotZ != 0f) Quaterniond.RotateZ(quat, quat, rotZ);

            for (int i = 0; i < quat.Length; i++) qf[i] = (float)quat[i];
            Mat4f.Mul(tmpMat, tmpMat, Mat4f.FromQuat(rotMat, qf));

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

    }
}

