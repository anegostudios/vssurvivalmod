using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityButterfly : EntityAgent
    {
        //double sitHeight = 1;
        public double windMotion;
        int cnt = 0;

        static EntityButterfly() {
            AiTaskRegistry.Register<AiTaskButterflyWander>("butterflywander");
            AiTaskRegistry.Register<AiTaskButterflyRest>("butterflyrest");
            AiTaskRegistry.Register<AiTaskButterflyChase>("butterflychase");
            AiTaskRegistry.Register<AiTaskButterflyFlee>("butterflyflee");
            AiTaskRegistry.Register<AiTaskButterflyFeedOnFlowers>("butterflyfeedonflowers");
        }

        public override bool IsInteractable => false;

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            
            if (api.Side == EnumAppSide.Client)
            {
                WatchedAttributes.RegisterModifiedListener("windWaveIntensity", () =>
                {
                    (Properties.Client.Renderer as EntityShapeRenderer).WindWaveIntensity = WatchedAttributes.GetDouble("windWaveIntensity");
                });
            }


            ClimateCondition conds = api.World.BlockAccessor.GetClimateAt(Pos.AsBlockPos, EnumGetClimateMode.NowValues);
            if (conds.Temperature < 0)
            {
                Die(EnumDespawnReason.Removed);
            }
        }


        float flapPauseDt = 0;

        public override void OnGameTick(float dt)
        {
            if (World.Side == EnumAppSide.Server)
            {
                base.OnGameTick(dt);
                return;
            }

            if (!AnimManager.ActiveAnimationsByAnimCode.ContainsKey("feed") && !AnimManager.ActiveAnimationsByAnimCode.ContainsKey("rest"))
            {
                if (ServerPos.Y < Pos.Y - 0.05 && !Collided)
                {
                    SetAnimation("glide", 1);
                }

                if ((ServerPos.Y > Pos.Y - 0.02 || Collided) && !FeetInLiquid)
                {
                    SetAnimation("fly", 2.5f);
                }

                if (FeetInLiquid)
                {
                    (Properties.Client.Renderer as EntityShapeRenderer).AddRenderFlags |= 1 << 12; 
                } else
                {
                    (Properties.Client.Renderer as EntityShapeRenderer).AddRenderFlags &= ~(1 << 12);
                }
                
                if (FeetInLiquid && flapPauseDt <= 0 && Api.World.Rand.NextDouble() < 0.07)
                {
                    flapPauseDt = 2 + 6 * (float)Api.World.Rand.NextDouble();
                    StopAnimation("fly");
                }

                if (flapPauseDt > 0)
                {
                    flapPauseDt -= dt;

                    if (flapPauseDt <= 0)
                    {
                        SetAnimation("fly", 2.5f);
                    }
                } else
                {
                    if (FeetInLiquid)
                    {
                        EntityPos herepos = Pos;
                        double width = CollisionBox.XSize * 0.75f;

                        SplashParticleProps.BasePos.Set(herepos.X - width / 2, herepos.Y - 0.05, herepos.Z - width / 2);
                        SplashParticleProps.AddPos.Set(width, 0, width);

                        SplashParticleProps.AddVelocity.Set(0, 0, 0);
                        SplashParticleProps.QuantityMul = 0.01f;

                        World.SpawnParticles(SplashParticleProps);

                        SpawnWaterMovementParticles(1, 0, +0.05, 0);
                    }
                }
            }

            
            base.OnGameTick(dt);

            if (cnt++ > 30)
            {
                float affectedness = World.BlockAccessor.GetLightLevel(SidedPos.XYZ.AsBlockPos, EnumLightLevelType.OnlySunLight) < 14 ? 1 : 0;
                windMotion = Api.ModLoader.GetModSystem<WeatherSystemBase>().WeatherDataSlowAccess.GetWindSpeed(SidedPos.XYZ) * affectedness;
                cnt = 0;
            }

            if (AnimManager.ActiveAnimationsByAnimCode.ContainsKey("fly"))
            {
                SidedPos.X += Math.Max(0, (windMotion - 0.2) / 20.0);
            }

            if (ServerPos.SquareDistanceTo(Pos.XYZ) > 0.01 && !FeetInLiquid)
            {
                float desiredYaw = (float)Math.Atan2(ServerPos.X - Pos.X, ServerPos.Z - Pos.Z);

                float yawDist = GameMath.AngleRadDistance(SidedPos.Yaw, desiredYaw);
                Pos.Yaw += GameMath.Clamp(yawDist, -35 * dt, 35 * dt);
                Pos.Yaw = Pos.Yaw % GameMath.TWOPI;
            }
        }


        private void SetAnimation(string animCode, float speed)
        {
            AnimationMetaData animMeta;
            if (!AnimManager.ActiveAnimationsByAnimCode.TryGetValue(animCode, out animMeta))
            {
                animMeta = new AnimationMetaData()
                {
                    Code = animCode,
                    Animation = animCode,
                    AnimationSpeed = speed,                   
                };

                AnimManager.ActiveAnimationsByAnimCode.Clear();
                AnimManager.ActiveAnimationsByAnimCode[animMeta.Animation] = animMeta;
                return;
            }

            animMeta.AnimationSpeed = speed;
            UpdateDebugAttributes();
        }

        public override void OnReceivedServerAnimations(int[] activeAnimations, int activeAnimationsCount, float[] activeAnimationSpeeds)
        {
            // We control glide and fly animations entirely client side

            if (activeAnimationsCount == 0)
            {
                AnimManager.ActiveAnimationsByAnimCode.Clear();
                AnimManager.StartAnimation("fly");
            }

            string active = "";

            bool found = false;

            for (int i = 0; i < activeAnimationsCount; i++)
            {
                int crc32 = activeAnimations[i];
                for (int j = 0; j < Properties.Client.LoadedShape.Animations.Length; j++)
                {
                    Animation anim = Properties.Client.LoadedShape.Animations[j];
                    int mask = ~(1 << 31); // Because I fail to get the sign bit transmitted correctly over the network T_T
                    if ((anim.CodeCrc32 & mask) == (crc32 & mask))
                    {
                        if (AnimManager.ActiveAnimationsByAnimCode.ContainsKey(anim.Code)) break;
                        if (anim.Code == "glide" || anim.Code == "fly") continue;

                        string code = anim.Code == null ? anim.Name.ToLowerInvariant() : anim.Code;
                        active += ", " + code;
                        AnimationMetaData animmeta = null;
                        Properties.Client.AnimationsByMetaCode.TryGetValue(code, out animmeta);

                        if (animmeta == null)
                        {
                            animmeta = new AnimationMetaData()
                            {
                                Code = code,
                                Animation = code,
                                CodeCrc32 = anim.CodeCrc32
                            };
                        }

                        animmeta.AnimationSpeed = activeAnimationSpeeds[i];

                        AnimManager.ActiveAnimationsByAnimCode[anim.Code] = animmeta;

                        found = true;
                    }
                }
            }

            if (found)
            {
                AnimManager.StopAnimation("fly");
                AnimManager.StopAnimation("glide");

                (Properties.Client.Renderer as EntityShapeRenderer).AddRenderFlags = VertexFlags.FoliageWindWaveBitMask;
                (Properties.Client.Renderer as EntityShapeRenderer).WindWaveIntensity = WatchedAttributes.GetDouble("windWaveIntensity");
            } else
            {
                (Properties.Client.Renderer as EntityShapeRenderer).AddRenderFlags = 0;
            }

            

        }
    }
}
