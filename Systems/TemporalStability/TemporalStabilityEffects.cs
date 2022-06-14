using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class GameEffectsPacket
    {
        public bool RainAndFogActive;
        public bool GlitchPresent;
        public bool SlomoActive;
    }


    public class TemporalStabilityEffects : ModSystem, IRenderer
    {
        bool rainAndFogActive;
        bool slowmoModeActive;

        bool glitchPresent;
        float glitchActive = 0;

        float warp;
        float secondsPassedRainFogMode;
        float secondsPassedSlowMoMode;

        float secondsPassedSlowGlitchMode;

        ICoreAPI api;
        ICoreClientAPI capi;

        public double RenderOrder => 1;
        public int RenderRange => 9999;

        SimpleParticleProperties blackAirParticles;

        IServerNetworkChannel serverChannel;
        IClientNetworkChannel clientChannel;

        AmbientModifier rainfogAmbient;


        GearRenderer renderer;


        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;

            this.capi = api;
            this.api = api;

            clientChannel =
                api.Network.RegisterChannel("gameeffects")
               .RegisterMessageType(typeof(GameEffectsPacket))
               .SetMessageHandler<GameEffectsPacket>(OnGameEffectToggle)
            ;

            blackAirParticles = new SimpleParticleProperties()
            {
                Color = ColorUtil.ToRgba(150, 50, 25, 15),
                ParticleModel = EnumParticleModel.Quad,
                MinSize = 0.1f,
                MaxSize = 1f,
                GravityEffect = 0,
                LifeLength = 1.2f,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true,
                MinVelocity = new Vec3f(-5f, 10f, -3f),
                MinQuantity = 1,
                AddQuantity = 0,
            };
            blackAirParticles.AddVelocity = new Vec3f(0f, 30f, 0);
            blackAirParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -8);

            api.Event.RegisterRenderer(this, EnumRenderStage.Before, "gameeffects");



            renderer = new GearRenderer(capi);

            api.RegisterCommand("geartest", "", "", onCmdGearTest);

            api.Event.LevelFinalize += Event_LevelFinalize;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            api.Event.RegisterGameTickListener(OnServerTick, 20);

            serverChannel =
               api.Network.RegisterChannel("gameeffects")
               .RegisterMessageType(typeof(GameEffectsPacket))
            ;
        }

        private void OnGameEffectToggle(GameEffectsPacket msg)
        {
            if (this.rainAndFogActive != msg.RainAndFogActive) ResetRainFog();
            this.rainAndFogActive = msg.RainAndFogActive;
            if (this.slowmoModeActive != msg.SlomoActive) ResetSlomo();
            this.slowmoModeActive = msg.SlomoActive;
            if (this.glitchPresent != msg.GlitchPresent) ResetGlitch();
            this.glitchPresent = msg.GlitchPresent;
        }



        void ResetRainFog()
        {
            if (capi != null)
            {
                float b = 0.5f;
                capi.Ambient.CurrentModifiers["brownrainandfog"] = rainfogAmbient = new AmbientModifier()
                {
                    AmbientColor = new WeightedFloatArray(new float[] { b * 132 / 255f, b * 115 / 255f, b * 112f / 255f, 1 }, 0),
                    FogColor = new WeightedFloatArray(new float[] { b * 132 / 255f, b * 115 / 255f, b * 112f / 255f, 1 }, 0),
                    FogDensity = new WeightedFloat(0.035f, 0),
                }.EnsurePopulated();
            }

            secondsPassedRainFogMode = 0;
        }


        void ResetSlomo()
        {
            GlobalConstants.OverallSpeedMultiplier = 1f;
            secondsPassedSlowMoMode = 0;
            api.World.Calendar.RemoveTimeSpeedModifier("slomo");
        }

        private void ResetGlitch()
        {
            warp = 0;
            if (capi != null)
            {
                capi.Render.ShaderUniforms.GlobalWorldWarp = 0;
                capi.Ambient.CurrentModifiers.Remove("glitch");
            }

            secondsPassedSlowGlitchMode = 0;
        }


        void UpdateClients()
        {
            serverChannel.BroadcastPacket(new GameEffectsPacket()
            {
                SlomoActive = slowmoModeActive,
                RainAndFogActive = rainAndFogActive,
                GlitchPresent = glitchPresent
            });
        }


        private void OnServerTick(float dt)
        {
            if (glitchPresent)
            {
                warp = GameMath.Clamp(warp + dt * 40, 0, 30);
                // secondsPassedGlitchMode = GameMath.Clamp(secondsPassedGlitchMode - dt * 30, -60, 60);
            }

            if (slowmoModeActive)
            {
                secondsPassedSlowMoMode += dt / 3;
                GlobalConstants.OverallSpeedMultiplier = 1 - GameMath.SmoothStep(Math.Min(1, secondsPassedSlowMoMode));
                api.World.Calendar.SetTimeSpeedModifier("slomo", -60 * (1 - GlobalConstants.OverallSpeedMultiplier));
            }
        }



        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (slowmoModeActive)
            {
                secondsPassedSlowMoMode += deltaTime / 3;
                GlobalConstants.OverallSpeedMultiplier = 1 - GameMath.SmoothStep(Math.Min(1, secondsPassedSlowMoMode));

                capi.World.Calendar.SetTimeSpeedModifier("slomo", -60 * (1 - GlobalConstants.OverallSpeedMultiplier));
            }

            if (rainAndFogActive)
            {
                secondsPassedRainFogMode += deltaTime;

                float strength = Math.Min(1, secondsPassedRainFogMode / 3);

                rainfogAmbient.AmbientColor.Weight = strength;
                rainfogAmbient.FogColor.Weight = strength;
                rainfogAmbient.FogDensity.Weight = strength;

                float tries = 40 * strength;
                while (tries-- > 0)
                {
                    float offX = (float)capi.World.Rand.NextDouble() * 64 - 32;
                    float offY = (float)capi.World.Rand.NextDouble() * 64 - 32;
                    float offZ = (float)capi.World.Rand.NextDouble() * 64 - 32;

                    Vec3d position = capi.World.Player.Entity.Pos.XYZ.OffsetCopy(offX, offY, offZ);
                    BlockPos pos = new BlockPos((int)position.X, (int)position.Y, (int)position.Z);

                    if (!capi.World.BlockAccessor.IsValidPos(pos)) continue;

                    blackAirParticles.WithTerrainCollision = false;
                    blackAirParticles.MinPos = position;
                    blackAirParticles.SelfPropelled = true;

                    capi.World.SpawnParticles(blackAirParticles);
                }

            }

            if (glitchPresent)
            {
                warp = 0;
                secondsPassedSlowGlitchMode += deltaTime;

                bool wasActive = glitchActive > 0;

                if (capi.World.Rand.NextDouble() < 0.0001 + secondsPassedSlowGlitchMode / 10000.0 && glitchActive <= 0)
                {
                    glitchActive = 0.1f + (float)capi.World.Rand.NextDouble() / 3.5f;
                    capi.World.AddCameraShake(glitchActive * 1.2f);
                }

                if (glitchActive > 0)
                {
                    capi.Ambient.CurrentModifiers["glitch"] = new AmbientModifier()
                    {
                        AmbientColor = new WeightedFloatArray(new float[] { 0.458f, 0.223f, 0.129f, 1 }, 1),
                        FogColor = new WeightedFloatArray(new float[] { 0.458f * 0.5f, 0.223f * 0.5f, 0.129f * 0.5f, 1 }, 1),
                        FogDensity = new WeightedFloat(0.04f, 1),

                    }.EnsurePopulated();

                    glitchActive -= deltaTime;
                    warp = 100;

                }
                else
                {
                    capi.Ambient.CurrentModifiers.Remove("glitch");
                    if (wasActive) capi.World.ReduceCameraShake(0.2f);
                }

                capi.Render.ShaderUniforms.GlobalWorldWarp = warp;
            }

        }





        private void Event_LevelFinalize()
        {
            renderer.Init();
            capi.Logger.VerboseDebug("Done init huge gears");
        }

        private void onCmdGearTest(int groupId, CmdArgs args)
        {
            renderer.Init();
        }








    }
}
