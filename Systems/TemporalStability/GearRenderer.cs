using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class MachineGear
    {
        public Vec3d Position = new Vec3d();
        public Vec3d Rot = new Vec3d();
        public float Velocity;
        public float Size;
    }

    public class GearRenderer : IRenderer
    {
        public double RenderOrder => 1;
        public int RenderRange => 100;

        MeshRef meshref;
        Matrixf matrixf;
        float counter;
        ICoreClientAPI capi;
        IShaderProgram prog;

        List<MachineGear> mgears = new List<MachineGear>();

        public GearRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "machinegearrenderer");
            
            matrixf = new Matrixf();

            capi.Event.ReloadShader += LoadShader;
            LoadShader();
        }

        public void Init()
        {
            var shape = API.Common.Shape.TryGet(capi, "shapes/block/machine/machinegear2.json");
            var block = capi.World.GetBlock(new AssetLocation("platepile"));
            capi.Tesselator.TesselateShape(block, shape, out var mesh);
            meshref = capi.Render.UploadMesh(mesh);

            genGears();
        }

        void genGears()
        {
            var rnd = capi.World.Rand;

            mgears.Clear();
            double angle = rnd.NextDouble() * GameMath.TWOPI;

            int cnt = 6;
            float angleStep = GameMath.TWOPI / cnt;

            for (int i = 0; i < cnt; i++)
            {
                double dist = 150 + rnd.NextDouble() * 300;
                dist *= 2*2.5;

                angle += angleStep + rnd.NextDouble() * angleStep * 0.1 - angleStep * 0.05;

                var size = 20 + (float)rnd.NextDouble() * 30;
                size *= 6*2.5f;

                var mg = new MachineGear()
                {
                    Position = new Vec3d(/*plrPos.X +*/ GameMath.Sin(angle) * dist, size / 2f, /*plrPos.Z +*/ GameMath.Cos(angle) * dist),
                    Rot = new Vec3d(0, rnd.NextDouble() * GameMath.TWOPI, rnd.NextDouble() - 1/2.0),
                    Velocity = (float)rnd.NextDouble() * 0.2f,
                    Size = size,
                };

                mgears.Add(mg);
            }
        }



        public bool LoadShader()
        {
            prog = capi.Shader.NewShaderProgram();
            prog.VertexShader = capi.Shader.NewShader(EnumShaderType.VertexShader);
            prog.FragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);

            capi.Shader.RegisterFileShaderProgram("machinegear", prog);

            return prog.Compile();
        }


        float raiseyRel;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var plrPos = capi.World.Player.Entity.Pos;
            var bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();

            float targetRaiseyRel = 0; 
            if (bh !=null) targetRaiseyRel = GameMath.Clamp((float)bh.GlichEffectStrength * 5f - 3f, 0, 1);
            
            raiseyRel += (targetRaiseyRel - raiseyRel) * deltaTime;

            if (raiseyRel <= 0.01f) return;

            capi.Render.GlToggleBlend(true);

            prog.Use();
            prog.Uniform("rgbaFogIn", capi.Render.FogColor);
            prog.Uniform("fogMinIn", capi.Render.FogMin);
            prog.Uniform("fogDensityIn", capi.Render.FogDensity);
            prog.Uniform("rgbaAmbientIn", capi.Render.AmbientColor);
            prog.Uniform("rgbaLightIn", new Vec4f(1,1,1,1));
            prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);            
            prog.Uniform("counter", counter);

            

            int riftIndex = 0;

            foreach (var gear in mgears)
            {
                riftIndex++;
                matrixf.Identity();

                gear.Position.Y = Math.Max(gear.Position.Y, capi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos((int)(gear.Position.X + plrPos.X), 0, (int)(gear.Position.Z + plrPos.Z))));

                float dx = (float)(gear.Position.X);
                float dy = (float)(gear.Position.Y - plrPos.Y - (1 - raiseyRel) * gear.Size * 1.5f);
                float dz = (float)(gear.Position.Z);

                float dist = GameMath.Sqrt(dx * dx + dz * dz);

                matrixf.Mul(capi.Render.CameraMatrixOriginf);

                matrixf.Translate(dx, dy, dz);
                matrixf.RotateY((float)gear.Rot.Y);
                matrixf.RotateX((float)gear.Rot.Z + GameMath.PIHALF);


                float size = gear.Size;
                matrixf.Scale(size, size, size);

                matrixf.Translate(0.5f, 0.5f, 0.5f);
                matrixf.RotateY(counter * (float)gear.Velocity);
                matrixf.Translate(-0.5f, -0.5f, -0.5f);

                prog.Uniform("alpha", 1.0f);

                prog.UniformMatrix("modelViewMatrix", matrixf.Values);
                prog.Uniform("worldPos", new Vec4f(dx, dy, dz, 0));
                prog.Uniform("riftIndex", riftIndex);

                capi.Render.RenderMesh(meshref);
            }


            counter = GameMath.Mod(counter + deltaTime, GameMath.TWOPI * 100f);

            prog.Stop();

            capi.Render.GlToggleBlend(false);
        }


        public void Dispose()
        {
            meshref?.Dispose();
        }

    }
}

