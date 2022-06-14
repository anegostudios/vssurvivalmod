using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class RiftRenderer : IRenderer
    {
        public double RenderOrder => 0.05;
        public int RenderRange => 100;

        MeshRef meshref;
        Matrixf matrixf;
        float counter;
        ICoreClientAPI capi;
        IShaderProgram prog;
        public Dictionary<int, Rift> rifts;
        ModSystemRifts modsys;


        public RiftRenderer(ICoreClientAPI capi, Dictionary<int, Rift> rifts)
        {
            this.capi = capi;
            this.rifts = rifts;

            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterBlit, "riftrenderer");
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


        int cnt = 0;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var plrPos = capi.World.Player.Entity.Pos;
            var bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
            if (bh != null)
            {
                bh.stabilityOffset = 0;
            }
            
            if (modsys.nearestRifts.Length > 0)
            {
                Rift rift = modsys.nearestRifts[0];
                
                float dist = Math.Max(0, GameMath.Sqrt(plrPos.SquareDistanceTo(rift.Position)) - 1 - rift.Size / 2f);
                float f = Math.Max(0, 1 - dist / 3f);
                float jitter = capi.World.Rand.NextDouble() < 0.25 ? f * ((float)capi.World.Rand.NextDouble() - 0.5f) / 1f : 0;

                GlobalConstants.GuiGearRotJitter = jitter;

                capi.ModLoader.GetModSystem<SystemTemporalStability>().modGlitchStrength = Math.Min(1, f * 1.3f);

                if (bh != null)
                {
                    bh.stabilityOffset = -Math.Pow(Math.Max(0, 1 - dist / 3), 2) * 20;
                }
            } else
            {
                capi.ModLoader.GetModSystem<SystemTemporalStability>().modGlitchStrength = 0;
            }

            counter += deltaTime;
            if (capi.World.Rand.NextDouble() < 0.012)
            {
                counter += 20*(float)capi.World.Rand.NextDouble();
            }

            capi.Render.GLDepthMask(false);

            prog.Use();
            prog.Uniform("rgbaFogIn", capi.Render.FogColor);
            prog.Uniform("fogMinIn", capi.Render.FogMin);
            prog.Uniform("fogDensityIn", capi.Render.FogDensity);
            prog.Uniform("rgbaAmbientIn", capi.Render.AmbientColor);
            prog.Uniform("rgbaLightIn", new Vec4f(1,1,1,1));


            prog.BindTexture2D("primaryFb", capi.Render.FrameBuffers[(int)EnumFrameBuffer.Primary].ColorTextureIds[0], 0);
            prog.BindTexture2D("depthTex", capi.Render.FrameBuffers[(int)EnumFrameBuffer.Primary].DepthTextureId, 1);
            prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);


            int width = capi.Render.FrameWidth;
            int height = capi.Render.FrameHeight;
            prog.Uniform("counter", counter);
            float bf = 200 + (float)GameMath.Sin(capi.InWorldEllapsedMilliseconds / 24000.0) * 100;
            
            prog.Uniform("counterSmooth", bf);
            prog.Uniform("invFrameSize", new Vec2f(1f / width, 1f / height));
            int riftIndex = 0;

            cnt = (cnt + 1) % 3;

            foreach (var rift in rifts.Values)
            {
                if (cnt == 0)
                {
                    rift.Visible = capi.World.BlockAccessor.GetChunkAtBlockPos((int)rift.Position.X, (int)rift.Position.Y, (int)rift.Position.Z) != null;
                }

                riftIndex++;
                matrixf.Identity();

                float dx = (float)(rift.Position.X - plrPos.X);
                float dy = (float)(rift.Position.Y - plrPos.Y);
                float dz = (float)(rift.Position.Z - plrPos.Z);

                matrixf.Translate(dx, dy, dz);
                matrixf.ReverseMul(capi.Render.CameraMatrixOriginf);

                matrixf.Values[0] = 1f;
                matrixf.Values[1] = 0f;
                matrixf.Values[2] = 0f;

                //matrixf.Values[4] = 0f;
                //matrixf.Values[5] = 1f;
                //matrixf.Values[6] = 0f;

                matrixf.Values[8] = 0f;
                matrixf.Values[9] = 0f;
                matrixf.Values[10] = 1f;

                float size = rift.GetNowSize(capi);
                matrixf.Scale(size, size, size);

                prog.UniformMatrix("modelViewMatrix", matrixf.Values);
                prog.Uniform("worldPos", new Vec4f(dx, dy, dz, 0));
                prog.Uniform("riftIndex", riftIndex);

                capi.Render.RenderMesh(meshref);

                if (dx * dx + dy * dy + dz * dz < 40 * 40)
                {
                    Vec3d ppos = rift.Position;
                    capi.World.SpawnParticles(0.1f, ColorUtil.ColorFromRgba(21 / 2, 70 / 2, 116 / 2, 128), ppos, ppos, new Vec3f(-0.125f, -0.125f, -0.125f), new Vec3f(0.125f, 0.125f, 0.125f), 5, 0, (0.125f / 2 + (float)capi.World.Rand.NextDouble() * 0.25f) / 2);
                }
            }


            counter = GameMath.Mod(counter + deltaTime, GameMath.TWOPI * 100f);

            prog.Stop();

            capi.Render.GLDepthMask(true);
        }

        public void Dispose()
        {
            meshref?.Dispose();
        }


    }
}
