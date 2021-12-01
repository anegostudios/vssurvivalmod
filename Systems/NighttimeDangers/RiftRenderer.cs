using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class Rift
    {
        public float Size = 1f;
        public Vec3d Position;
        public Vec3f JitterOffset = new Vec3f();
    }

    public class RiftRenderer : IRenderer
    {
        public double RenderOrder => 0.05;
        public int RenderRange => 100;

        MeshRef meshref;
        Matrixf matrixf;
        float counter;
        ICoreClientAPI capi;
        IShaderProgram prog;
        List<Rift> rifts;
        ModSystemRifts modsys;

        public RiftRenderer(ICoreClientAPI capi, List<Rift> rifts)
        {
            this.capi = capi;
            this.rifts = rifts;

            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterBlit, "rendertest");
            MeshData mesh = QuadMeshUtil.GetQuad();

            meshref = capi.Render.UploadMesh(mesh);
            matrixf = new Matrixf();

            capi.Event.ReloadShader += LoadShader;
            LoadShader();

            modsys = capi.ModLoader.GetModSystem<ModSystemRifts>();
        }



        public bool LoadShader()
        {
            prog = capi.Shader.NewShaderProgram();
            prog.VertexShader = capi.Shader.NewShader(EnumShaderType.VertexShader);
            prog.FragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);

            capi.Shader.RegisterFileShaderProgram("rift", prog);

            return prog.Compile();
        }


        float jitterAccum;


        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var plrPos = capi.World.Player.Entity.Pos;

            if (modsys.nearestRifts.Length > 0)
            {
                Rift rift = modsys.nearestRifts[0];
                     
                float dist = Math.Max(0, GameMath.Sqrt(plrPos.SquareDistanceTo(rift.Position.AddCopy(rift.JitterOffset))) - 2 - rift.Size / 2f);
                float f = Math.Max(0, 1 - dist / 3f);
                float jitter = capi.World.Rand.NextDouble() < 0.25 ? f * ((float)capi.World.Rand.NextDouble() - 0.5f) / 2f : 0;

                GlobalConstants.GuiGearRotJitter = jitter;

                capi.ModLoader.GetModSystem<SystemTemporalStability>().modGlitchStrength = f;

            }


            counter += deltaTime;
            jitterAccum += deltaTime * 2 * (float)capi.World.Rand.NextDouble();

            float jitterStep = 1 / 50f;

            if (capi.World.Rand.NextDouble() < 0.02)
            {
                counter += 30*(float)capi.World.Rand.NextDouble();
            }


            capi.Render.GLDepthMask(false);

            prog.Use();
            prog.BindTexture2D("primaryFb", capi.Render.FrameBuffers[(int)EnumFrameBuffer.Primary].ColorTextureIds[0], 0);
            prog.BindTexture2D("depthTex", capi.Render.FrameBuffers[(int)EnumFrameBuffer.Primary].DepthTextureId, 1);

            prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);


            int width = capi.Render.FrameWidth;
            int height = capi.Render.FrameHeight;
            prog.Uniform("counter", counter);
            prog.Uniform("counterSmooth", (float)GameMath.Sin(capi.InWorldEllapsedMilliseconds / 80000.0) * 200);
            prog.Uniform("invFrameSize", new Vec2f(1f / width, 1f / height));
            int riftIndex = 0;

            foreach (var rift in rifts)
            {
                riftIndex++;
                matrixf.Identity();

                if (jitterAccum > jitterStep)
                {
                    rift.JitterOffset.X = (float)(capi.World.Rand.NextDouble() - 0.5f) / 5f;
                    rift.JitterOffset.Y = (float)(capi.World.Rand.NextDouble() - 0.5f) / 5f;
                    rift.JitterOffset.Z = (float)(capi.World.Rand.NextDouble() - 0.5f) / 5f;
                }

                float dx = (float)(rift.Position.X - plrPos.X) + GameMath.Sin(capi.InWorldEllapsedMilliseconds / 28000f / 2f + riftIndex) * 3;
                float dy = (float)(rift.Position.Y - plrPos.Y) + GameMath.Sin(capi.InWorldEllapsedMilliseconds / 30000f / 2f + riftIndex) * 3;
                float dz = (float)(rift.Position.Z - plrPos.Z) + GameMath.Sin(capi.InWorldEllapsedMilliseconds / 31000f / 2f + riftIndex) * 3;

                //float atn = 1 - GameMath.Clamp(15 - GameMath.Sqrt(dx * dx + dy * dy + dz * dz), 0, 10) / 10f;
                float atn = 0;
                matrixf.Translate(dx + atn * rift.JitterOffset.X, dy + atn * rift.JitterOffset.Y, dz + atn * rift.JitterOffset.Z);
                matrixf.ReverseMul(capi.Render.CameraMatrixOriginf);


                matrixf.Values[0] = 1f;
                matrixf.Values[1] = 0f;
                matrixf.Values[2] = 0f;

                matrixf.Values[4] = 0f;
                matrixf.Values[5] = 1f;
                matrixf.Values[6] = 0f;

                matrixf.Values[8] = 0f;
                matrixf.Values[9] = 0f;
                matrixf.Values[10] = 1f;

                matrixf.Scale(rift.Size, rift.Size, rift.Size);
                //matrixf.RotateYDeg(riftIndex * 1251241 / 12412f);

                prog.UniformMatrix("modelViewMatrix", matrixf.Values);
                prog.Uniform("riftIndex", riftIndex);

                capi.Render.RenderMesh(meshref);

                /*if (dx * dx + dy * dy + dz * dz < 40 * 40)
                {
                    Vec3d ppos = rift.Position.AddCopy(rift.JitterOffset);
                    capi.World.SpawnParticles(0.1f, ColorUtil.ColorFromRgba(21 / 2, 70 / 2, 116 / 2, 128), ppos, ppos, new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 10, 0, 0.125f / 2 + (float)capi.World.Rand.NextDouble() * 0.25f);
                }*/
            }


            counter = GameMath.Mod(counter + deltaTime, GameMath.TWOPI * 100f);

            prog.Stop();

            capi.Render.GLDepthMask(true);


            if (jitterAccum > jitterStep)
            {
                jitterAccum = 0;
            }
        }

        public void Dispose()
        {
            meshref?.Dispose();
        }


    }
}
