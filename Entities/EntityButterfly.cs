using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ModsystemButterflySpawnCondsExtra : ModSystem {
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.sapi = api;

            sapi.Event.OnTrySpawnEntity += Event_OnTrySpawnEntity;
        }

        BlockPos tmpPos = new BlockPos(API.Config.Dimensions.NormalWorld);   // Entity spawning only occurs in the normal world (currently)

        private bool Event_OnTrySpawnEntity(IBlockAccessor blockAccessor, ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {
            if (properties.Server.SpawnConditions?.Runtime?.MaxQuantityByGroup?.Code.Path == "butterfly-*")
            {
                tmpPos.Set((int)spawnPosition.X, (int)spawnPosition.Y, (int)spawnPosition.Z);
                float temperature = blockAccessor.GetClimateAt(tmpPos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, sapi.World.Calendar.TotalDays).Temperature;
                if (temperature < 0) return false;
            }

            return true;
        }
    }

    public class EntityButterfly : EntityAgent
    {
        //double sitHeight = 1;
        public double windMotion;
        private Vec3d tempWinPos;
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

            float temperature = api.World.BlockAccessor.GetClimateAt(Pos.AsBlockPos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, api.World.Calendar.TotalDays).Temperature;
            if (temperature < 0)
            {
                Die(EnumDespawnReason.Removed);
            }

            if (api is ICoreServerAPI)
            {
                tempWinPos = Pos.XYZ;
            }
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            RegisterWindWaveIntensityListener();
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
            RegisterWindWaveIntensityListener();
        }

        private void RegisterWindWaveIntensityListener()
        {
            if (Api.Side == EnumAppSide.Client)
            {
                WatchedAttributes.RegisterModifiedListener("windWaveIntensity", () =>
                {
                    (Properties.Client.Renderer as EntityShapeRenderer).WindWaveIntensity = WatchedAttributes.GetDouble("windWaveIntensity");
                });
            }
        }

        float flapPauseDt = 0;

        public override void OnGameTick(float dt)
        {
            if (Api is ICoreServerAPI)
            {
                base.OnGameTick(dt);

                if (cnt++ > 30)
                {
                    tempWinPos.Set(Pos);
                    var height = Math.Max(0, Pos.Y - World.BlockAccessor.GetRainMapHeightAt((int)tempWinPos.X, (int)tempWinPos.Z));
                    if (height > 1.5 &&
                        World.BlockAccessor.GetLightLevel((int)tempWinPos.X, (int)tempWinPos.Y, (int)tempWinPos.Z, EnumLightLevelType.OnlySunLight) > 14)
                    {
                        windMotion = Api.ModLoader.GetModSystem<WeatherSystemBase>().WeatherDataSlowAccess.GetWindSpeed(tempWinPos);
                    }
                    else
                    {
                        windMotion = 0;
                    }
                    cnt = 0;
                }
                if (!AnimManager.ActiveAnimationsByAnimCode.ContainsKey("rest") &&
                    Math.Abs(windMotion) > 0.4 &&
                    Math.Abs(Pos.Motion.X) < 1)
                {
                    Pos.Motion.X += Math.Clamp((windMotion - 0.2) / 200.0, -0.004, 0.004);
                }
                return;
            }

            if (FeetInLiquid)
            {
                (Properties.Client.Renderer as EntityShapeRenderer).AddRenderFlags |= 6 << 25;
            }
            else
            {
                (Properties.Client.Renderer as EntityShapeRenderer).AddRenderFlags &= ~(6 << 25);
            }

            if (!AnimManager.ActiveAnimationsByAnimCode.ContainsKey("feed") && !AnimManager.ActiveAnimationsByAnimCode.ContainsKey("rest"))
            {
                if (Pos.Y < Pos.Y - 0.05 && !Collided && !FeetInLiquid)
                {
                    SetAnimation("glide", 1);
                }
                if (FeetInLiquid)
                {
                    StopAnimation("glide");
                }

                if ((Pos.Y > Pos.Y - 0.02 || Collided) && !FeetInLiquid)
                {
                    SetAnimation("fly", 2.5f);
                }

                if (FeetInLiquid && flapPauseDt <= 0 && Api.World.Rand.NextDouble() < 0.06)
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
                        double width = SelectionBox.XSize * 0.75f;

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
        }


        private void SetAnimation(string animCode, float speed)
        {
            if (!AnimManager.ActiveAnimationsByAnimCode.TryGetValue(animCode, out AnimationMetaData animMeta))
            {
                animMeta = new AnimationMetaData()
                {
                    Code = animCode,
                    Animation = animCode,
                    AnimationSpeed = speed,
                };

                AnimManager.ActiveAnimationsByAnimCode.Clear();
                AnimManager.ActiveAnimationsByAnimCode[animMeta.Animation.ToLowerInvariant()] = animMeta;
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
                        Properties.Client.AnimationsByMetaCode.TryGetValue(code, out AnimationMetaData animmeta);

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

                        AnimManager.ActiveAnimationsByAnimCode[anim.Code.ToLowerInvariant()] = animmeta;

                        found = true;
                    }
                }
            }

            if (found)
            {
                AnimManager.StopAnimation("fly");
                AnimManager.StopAnimation("glide");

                (Properties.Client.Renderer as EntityShapeRenderer).AddRenderFlags = EnumWindBitModeMask.Bend;
                (Properties.Client.Renderer as EntityShapeRenderer).AddRenderFlags |= 1 << VertexFlags.WindDataBitsPos;
            } else
            {
                (Properties.Client.Renderer as EntityShapeRenderer).AddRenderFlags = 0;
            }
        }
    }
}
