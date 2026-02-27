using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

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

        MeshRef gearMeshref;
        Matrixf matrixf;
        float counter;
        ICoreClientAPI capi;
        IShaderProgram prog;

        List<MachineGear> mgears = new List<MachineGear>();

        AnimationUtil tripodAnim;
        Vec3d tripodPos = new Vec3d();
        double tripodAccum;
        LoadedTexture rustTexture;

        EntityBehaviorTemporalStabilityAffected bh;

        public GearRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;

            if (!capi.Assets.Exists("shapes/block/machine/machinegear2.json"))
            {
                capi.Logger.Warning("shapes/block/machine/machinegear2.json file missing. GearRenderer will be disabled.");
                return;
            }

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "machinegearrenderer");
            
            matrixf = new Matrixf();

            capi.Event.ReloadShader += LoadShader;
            LoadShader();
        }

        public void Init()
        {
            var shape = Shape.TryGet(capi, "shapes/block/machine/machinegear2.json");
            var block = capi.World.GetBlock(new AssetLocation("platepile"));
            if (block == null || shape == null) return;

            capi.Tesselator.TesselateShape(block, shape, out var mesh);
            gearMeshref = capi.Render.UploadMesh(mesh);
            genGears();


            rustTexture = new LoadedTexture(capi);
            var loc = new AssetLocation("textures/block/metal/tarnished/rust.png");
            capi.Render.GetOrLoadTexture(loc, ref rustTexture);

            shape = Shape.TryGet(capi, "shapes/entity/lore/supermech/thunderlord.json");

            tripodAnim = new AnimationUtil(capi, tripodPos);
            tripodAnim.InitializeShapeAndAnimator("tripod", shape, capi.Tesselator.GetTextureSource(block), null, out var _);
            tripodAnim.StartAnimation(new AnimationMetaData() { Animation = "walk", Code = "walk", BlendMode = EnumAnimationBlendMode.Average, AnimationSpeed = 0.1f });
            tripodAnim.renderer.ScaleX = 30;
            tripodAnim.renderer.ScaleY = 30;
            tripodAnim.renderer.ScaleZ = 30;
            tripodAnim.renderer.FogAffectedness = 0.15f;
            tripodAnim.renderer.LightAffected = false;
            tripodAnim.renderer.StabilityAffected = false;
            tripodAnim.renderer.ShouldRender = true;


            bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
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


        float raiseyRelGears;
        float raiseyRelTripod;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (tripodAnim == null) return;
            if (capi.IsGamePaused) deltaTime = 0;

            float targetRaiseyRel = 0;
            if (bh != null) targetRaiseyRel = GameMath.Clamp((float)bh.GlichEffectStrength * 5f - 3f, 0, 1);
            raiseyRelGears += (targetRaiseyRel - raiseyRelGears) * deltaTime;

            capi.Render.GlToggleBlend(true);

            if (raiseyRelGears >= 0.01f)
            {
                renderGears(deltaTime);
            }

            if (!capi.IsGamePaused)
            {
                tripodAnim.renderer.ShouldRender = raiseyRelTripod > 0.01f;
                updateSuperMechState(deltaTime, stage);
            }

            capi.Render.GlToggleBlend(false);
        }

        private void updateSuperMechState(float deltaTime, EnumRenderStage stage)
        {
            float targetRaiseyRel = 0;
            if (bh != null) targetRaiseyRel = GameMath.Clamp((float)bh.GlichEffectStrength * 5f - 1.75f, 0, 1);
            raiseyRelTripod += (targetRaiseyRel - raiseyRelTripod) * deltaTime/3f;

            var plrPos = capi.World.Player.Entity.Pos;

            tripodAccum += deltaTime / 50.0 * (0.33f + raiseyRelTripod) * 1.2f;
            tripodAccum = tripodAccum % 500000d;

            float d = (1 - raiseyRelTripod) * 900;

            tripodPos.X = plrPos.X + Math.Sin(tripodAccum) * (300d + d);
            tripodPos.Y = capi.World.SeaLevel;
            tripodPos.Z = plrPos.Z + Math.Cos(tripodAccum) * (300d + d);
            tripodAnim.renderer.rotationDeg.Y = (float)((tripodAccum % GameMath.TWOPI) + GameMath.PI) * GameMath.RAD2DEG;
            tripodAnim.renderer.renderColor.Set(0.5f, 0.5f, 0.5f, Math.Min(1, raiseyRelTripod*2));
            tripodAnim.renderer.FogAffectedness = 1f - GameMath.Clamp(raiseyRelGears * 2.2f - 0.5f, 0, 0.9f);

            tripodAnim.OnRenderFrame(deltaTime, stage);
        }

        private void renderGears(float deltaTime)
        {
            prog.Use();
            prog.Uniform("rgbaFogIn", capi.Render.FogColor);
            prog.Uniform("fogMinIn", capi.Render.FogMin);
            prog.Uniform("fogDensityIn", capi.Render.FogDensity);
            prog.Uniform("rgbaAmbientIn", capi.Render.AmbientColor);
            prog.Uniform("rgbaLightIn", new Vec4f(1, 1, 1, 1));
            prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
            prog.Uniform("counter", counter);

            int riftIndex = 0;
            var plrPos = capi.World.Player.Entity.Pos;

            foreach (var gear in mgears)
            {
                riftIndex++;
                matrixf.Identity();

                gear.Position.Y = Math.Max(gear.Position.Y, capi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos((int)(gear.Position.X + plrPos.X), 0, (int)(gear.Position.Z + plrPos.Z))));

                float dx = (float)(gear.Position.X);
                float dy = (float)(gear.Position.Y - plrPos.Y - (1 - raiseyRelGears) * gear.Size * 1.5f);
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

                capi.Render.RenderMesh(gearMeshref);
            }

            counter = GameMath.Mod(counter + deltaTime, GameMath.TWOPI * 100f);

            prog.Stop();
        }

        public void Dispose()
        {
            gearMeshref?.Dispose();
        }

    }
}

