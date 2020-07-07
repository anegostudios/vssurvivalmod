using System;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorTemporalStabilityAffected : EntityBehavior
    {
        ILoadedSound tempStabSoundDrain;
        ILoadedSound tempStabSoundLow;
        ILoadedSound tempStabSoundVeryLow;
        AmbientModifier rainfogAmbient;
        SimpleParticleProperties rustParticles;
        NormalizedSimplexNoise fogNoise;

        ICoreClientAPI capi;
        SystemTemporalStability tempStabilitySystem;
        float oneSecAccum = 0;
        float threeSecAccum = 0;
        double hereTempStabChangeVelocity;

        double glitchEffectStrength;
        double fogEffectStrength;

        public double TempStabChangeVelocity { get; set; }

        bool requireInitSounds;
        bool enabled = true;
        bool isSelf;

        public double OwnStability
        {
            get { return entity.WatchedAttributes.GetDouble("temporalStability"); }
            set { entity.WatchedAttributes.SetDouble("temporalStability", value); }
        }

        public EntityBehaviorTemporalStabilityAffected(Entity entity) : base(entity)
        {

        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            tempStabilitySystem = entity.Api.ModLoader.GetModSystem<SystemTemporalStability>();

            if (entity.Api.Side == EnumAppSide.Client)
            {
                requireInitSounds = true;
            }

            enabled = entity.Api.World.Config.GetBool("temporalStability", false);

            if (!entity.WatchedAttributes.HasAttribute("temporalStability"))
            {
                OwnStability = 1;
            }
        }




        void initSoundsAndEffects()
        {
            capi = entity.Api as ICoreClientAPI;
            isSelf = capi.World.Player.Entity.EntityId == entity.EntityId;
            if (!isSelf) return;

            // Effects
            fogNoise = NormalizedSimplexNoise.FromDefaultOctaves(4, 1, 0.9, 123);

            rustParticles = new SimpleParticleProperties()
            {
                Color = ColorUtil.ToRgba(150, 50, 25, 15),
                ParticleModel = EnumParticleModel.Quad,
                MinSize = 0.1f,
                MaxSize = 0.5f,
                GravityEffect = 0,
                LifeLength = 2f,
                WithTerrainCollision = false,
                ShouldDieInLiquid = false,
                RandomVelocityChange = true,
                MinVelocity = new Vec3f(-1f, -1f, -1f),
                AddVelocity = new Vec3f(2f, 2f, 2f),
                MinQuantity = 1,
                AddQuantity = 0,
            };

            rustParticles.AddVelocity = new Vec3f(0f, 30f, 0);
            rustParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -8);


            float b = 0.25f;
            capi.Ambient.CurrentModifiers["brownrainandfog"] = rainfogAmbient = new AmbientModifier()
            {
                AmbientColor = new WeightedFloatArray(new float[] { 0.5f * 132 / 255f, 0.5f * 115 / 255f, 0.5f * 112f / 255f, 1 }, 0),
                FogColor = new WeightedFloatArray(new float[] { b * 132 / 255f, b * 115 / 255f, b * 112f / 255f, 1 }, 0),
                FogDensity = new WeightedFloat(0.05f, 0),
            }.EnsurePopulated();



            // Sounds
            tempStabSoundDrain = capi.World.LoadSound(new SoundParams()
            {
                Location = new AssetLocation("sounds/effect/tempstab-drain.ogg"),
                ShouldLoop = true,
                RelativePosition = true,
                DisposeOnFinish = false,
                SoundType = EnumSoundType.SoundGlitchunaffected,
                Volume = 0f
            });

            tempStabSoundLow = capi.World.LoadSound(new SoundParams()
            {
                Location = new AssetLocation("sounds/effect/tempstab-low.ogg"),
                ShouldLoop = true,
                RelativePosition = true,
                DisposeOnFinish = false,
                SoundType = EnumSoundType.SoundGlitchunaffected,
                Volume = 0f
            });

            tempStabSoundVeryLow = capi.World.LoadSound(new SoundParams()
            {
                Location = new AssetLocation("sounds/effect/tempstab-verylow.ogg"),
                ShouldLoop = true,
                RelativePosition = true,
                DisposeOnFinish = false,
                SoundType = EnumSoundType.SoundGlitchunaffected,
                Volume = 0f
            });
            
        }

        internal void AddStability(double amount)
        {
            OwnStability += amount;
        }

        public override string PropertyName()
        {
            return "temporalstabilityaffected";
        }

        BlockPos tmpPos = new BlockPos();

        public override void OnGameTick(float deltaTime)
        {
            if (!enabled) return;

            if (requireInitSounds)
            {
                initSoundsAndEffects();
                requireInitSounds = false;
            }

            deltaTime = GameMath.Min(0.5f, deltaTime);
            double hereStability = tempStabilitySystem.GetTemporalStability(entity.SidedPos.X, entity.SidedPos.Y, entity.SidedPos.Z);

            entity.Attributes.SetDouble("tempStabChangeVelocity", TempStabChangeVelocity);

            double gain = TempStabChangeVelocity > 0 ? (TempStabChangeVelocity / 200.0) : (TempStabChangeVelocity / 800.0);

            OwnStability = GameMath.Clamp(OwnStability + gain, 0f, 1);
            double ownStability = OwnStability;

            TempStabChangeVelocity = (hereTempStabChangeVelocity - TempStabChangeVelocity) * deltaTime;

            float glitchEffectExtraStrength = tempStabilitySystem.GetGlitchEffectExtraStrength();

            double targetGlitchEffectStrength = Math.Max(0, Math.Max(0, (0.2f - ownStability) * 1 / 0.2f) + glitchEffectExtraStrength);
            glitchEffectStrength += (targetGlitchEffectStrength - glitchEffectStrength) * deltaTime;
            glitchEffectStrength = GameMath.Clamp(glitchEffectStrength, 0, 1.1f);

            double targetFogEffectStrength = Math.Max(0, Math.Max(0, (0.3f - ownStability) * 1 / 0.3f) + glitchEffectExtraStrength);
            fogEffectStrength += (targetFogEffectStrength - fogEffectStrength) * deltaTime;
            fogEffectStrength = GameMath.Clamp(fogEffectStrength, 0, 1.1f);

            hereTempStabChangeVelocity = hereStability - 1;

            oneSecAccum += deltaTime;
            if (oneSecAccum > 1)
            {
                oneSecAccum = 0;
                updateSoundsAndEffects(hereStability, Math.Max(0, ownStability - 1.5f*glitchEffectExtraStrength));
            }
            threeSecAccum += deltaTime;
            if (threeSecAccum > 4)
            {
                threeSecAccum = 0;
                if (entity.World.Side == EnumAppSide.Server && ownStability < 0.13)
                {
                    entity.ReceiveDamage(new DamageSource() {
                        DamageTier = 0,
                        Source = EnumDamageSource.Machine,
                        Type = EnumDamageType.Poison
                    }, (float)(0.15 - ownStability));
                }
            }

            if (isSelf)
            {
                capi.Render.ShaderUniforms.GlitchStrength = 0;
            }

            if (isSelf && (fogEffectStrength > 0.05 || glitchEffectStrength > 0.05))
            {
                capi.Render.ShaderUniforms.GlitchStrength = (float)glitchEffectStrength;
                capi.Render.ShaderUniforms.GlobalWorldWarp = (float)(capi.World.Rand.NextDouble() < 0.015 ? (Math.Max(0, glitchEffectStrength - 0.05f) * capi.World.Rand.NextDouble() * capi.World.Rand.NextDouble()) : 0);
                capi.Render.ShaderUniforms.WindWaveCounter += (float)(capi.World.Rand.NextDouble() < 0.015 ? 9 * capi.World.Rand.NextDouble() : 0);
                capi.Render.ShaderUniforms.WaterWaveCounter += (float)(capi.World.Rand.NextDouble() < 0.015 ? 9 * capi.World.Rand.NextDouble() : 0);

                if (capi.World.Rand.NextDouble() < 0.002)
                {
                    capi.Input.MouseYaw += (float)capi.World.Rand.NextDouble() * 0.125f - 0.125f/2;
                    capi.Input.MousePitch += (float)capi.World.Rand.NextDouble() * 0.125f - 0.125f/2;
                }

                tmpPos.Set((int)entity.Pos.X, (int)entity.Pos.Y, (int)entity.Pos.Z);
                float sunb = capi.World.BlockAccessor.GetLightLevel(tmpPos, EnumLightLevelType.OnlySunLight) / 22f;

                float strength = Math.Min(1, (float)(glitchEffectStrength));

                double fognoise = fogEffectStrength * Math.Abs(fogNoise.Noise(0, capi.InWorldEllapsedMilliseconds/1000f)) / 60f;

                rainfogAmbient.FogDensity.Value = 0.05f + (float)fognoise;

                rainfogAmbient.AmbientColor.Weight = strength;
                rainfogAmbient.FogColor.Weight = strength;
                rainfogAmbient.FogDensity.Weight = (float)Math.Pow(strength, 2);

                rainfogAmbient.FogColor.Value[0] = sunb * 116 / 255f;
                rainfogAmbient.FogColor.Value[1] = sunb * 77 / 255f;
                rainfogAmbient.FogColor.Value[2] = sunb * 49 / 255f;

                rainfogAmbient.AmbientColor.Value[0] = 0.5f * 116 / 255f;
                rainfogAmbient.AmbientColor.Value[1] = 0.5f * 77 / 255f;
                rainfogAmbient.AmbientColor.Value[2] = 0.5f * 49 / 255f;

                //new float[] { 0.5f * 132 / 255f, 0.5f * 115 / 255f, 0.5f * 112f / 255f, 1 }

                rustParticles.MinVelocity.Set(-0.1f, 0.1f, 0.1f);
                rustParticles.AddVelocity.Set(0.2f, 0.2f, 0.2f);
                rustParticles.Color = ColorUtil.ToRgba((int)(strength * 150), 50, 25, 15);
                rustParticles.MaxSize = 0.25f;
                rustParticles.RandomVelocityChange = false;
                rustParticles.MinVelocity.Set(0, 0, 0);
                rustParticles.AddVelocity.Set(0, 1, 0);


                Vec3d position = new Vec3d();
                EntityPos plrPos = capi.World.Player.Entity.Pos;

                float tries = 20 * strength;

                while (tries-- > 0)
                {
                    float offX = (float)capi.World.Rand.NextDouble() * 24 - 12;
                    float offY = (float)capi.World.Rand.NextDouble() * 24 - 12;
                    float offZ = (float)capi.World.Rand.NextDouble() * 24 - 12;

                    position.Set(plrPos.X + offX, plrPos.Y + offY, plrPos.Z + offZ);
                    BlockPos pos = new BlockPos((int)position.X, (int)position.Y, (int)position.Z);

                    if (!capi.World.BlockAccessor.IsValidPos(pos)) continue;

                    
                    rustParticles.MinPos = position;
                    capi.World.SpawnParticles(rustParticles);
                }
            }
        }

        private void updateSoundsAndEffects(double hereStability, double ownStability)
        {
            if (!isSelf || tempStabSoundDrain == null) return;

            // Effects            


            // Sounds

            if (hereStability < 0.95f && ownStability < 0.65f)
            {
                if (!tempStabSoundDrain.IsPlaying)
                {
                    tempStabSoundDrain.Start();
                }

                tempStabSoundDrain.FadeTo(Math.Min(1, 3 * ( 1 - hereStability)), 0.95f, (s) => {  });
            } else
            {
                tempStabSoundDrain.FadeTo(0, 0.95f, (s) => { tempStabSoundDrain.Stop(); });
            }

            SurfaceMusicTrack.ShouldPlayMusic = ownStability > 0.45f;
            CaveMusicTrack.ShouldPlayCaveMusic = ownStability > 0.2f;

            if (ownStability < 0.4f)
            {
                if (!tempStabSoundLow.IsPlaying)
                {
                    tempStabSoundLow.Start();
                }

                float volume = (0.4f - (float)ownStability) * 1/0.4f;
                tempStabSoundLow.FadeTo(Math.Min(1, volume), 0.95f, (s) => {  });
            } else
            {
                tempStabSoundLow.FadeTo(0, 0.95f, (s) => { tempStabSoundLow.Stop(); });
            }

            if (ownStability < 0.25f)
            {
                if (!tempStabSoundVeryLow.IsPlaying)
                {
                    tempStabSoundVeryLow.Start();
                }

                float volume = (0.25f - (float)ownStability)*1/0.25f;
                tempStabSoundVeryLow.FadeTo(Math.Min(1, volume)/5f, 0.95f, (s) => { });
            } else
            {
                tempStabSoundVeryLow.FadeTo(0, 0.95f, (s) => { tempStabSoundVeryLow.Stop(); });
            }
        }


    }
}
