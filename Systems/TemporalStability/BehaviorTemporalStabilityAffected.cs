using System;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
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
        WeatherSimulationParticles precipParticleSys;

        float oneSecAccum = 0;
        float threeSecAccum = 0;
        double hereTempStabChangeVelocity;

        double glitchEffectStrength;
        double fogEffectStrength;
        double rustPrecipColorStrength;

        public double TempStabChangeVelocity { get; set; }

        public double GlichEffectStrength => glitchEffectStrength;

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
                precipParticleSys = entity.Api.ModLoader.GetModSystem<WeatherSystemClient>().simParticles;
            }

            enabled = entity.Api.World.Config.GetBool("temporalStability", true);

            if (!entity.WatchedAttributes.HasAttribute("temporalStability"))
            {
                OwnStability = 1;
            }
        }

        public override void OnEntityLoaded()
        {
            capi = entity.Api as ICoreClientAPI;
            if (capi == null) return;

            // capi.World.Player is not initialized yet
            bool isself = (entity as EntityPlayer)?.PlayerUID == capi.Settings.String["playeruid"];

            if (isself)
            {
                capi.Event.RegisterEventBusListener(onChatKeyDownPre, 1, "chatkeydownpre");
                capi.Event.RegisterEventBusListener(onChatKeyDownPost, 1, "chatkeydownpost");
            }
        }

        bool isCommand;

        private void onChatKeyDownPost(string eventName, ref EnumHandling handling, IAttribute data)
        {
            var treeAttr = data as TreeAttribute;
            string text = (treeAttr["text"] as StringAttribute).value;

            // User is trying to cheese the system
            if (isCommand && text.Length > 0 && text[0] != '.' && text[0] != '/')
            {
                float str = (capi.Render.ShaderUniforms.GlitchStrength - 0.5f) * 2;
                (treeAttr["text"] as StringAttribute).value = destabilizeText(text, str);
            }
        }

        private void onChatKeyDownPre(string eventName, ref EnumHandling handling, IAttribute data)
        {
            var treeAttr = data as TreeAttribute;
            int keyCode = (treeAttr["key"] as IntAttribute).value;
            string text = (treeAttr["text"] as StringAttribute).value;

            isCommand = text.Length > 0 && (text[0] == '.' || text[0] == '/');

            if (keyCode != (int)GlKeys.BackSpace && capi.Render.ShaderUniforms.GlitchStrength > 0.5f && (text.Length == 0 || !isCommand))
            {
                float str = (capi.Render.ShaderUniforms.GlitchStrength - 0.5f) * 2;
                (treeAttr["text"] as StringAttribute).value = destabilizeText(text, str);
            }
        }

        private string destabilizeText(string text, float str)
        {
            //those always stay in the middle
            char[] zalgo_mid = new char[] {
                    '\u0315', /*     ̕     */		'\u031b', /*     ̛     */		'\u0340', /*     ̀     */		'\u0341', /*     ́     */
                    '\u0358', /*     ͘     */		'\u0321', /*     ̡     */		'\u0322', /*     ̢     */		'\u0327', /*     ̧     */
                    '\u0328', /*     ̨     */		'\u0334', /*     ̴     */		'\u0335', /*     ̵     */		'\u0336', /*     ̶     */
                    '\u034f', /*     ͏     */		'\u035c', /*     ͜     */		'\u035d', /*     ͝     */		'\u035e', /*     ͞     */
                    '\u035f', /*     ͟     */		'\u0360', /*     ͠     */		'\u0362', /*     ͢     */		'\u0338', /*     ̸     */
                    '\u0337', /*     ̷     */		'\u0361', /*     ͡     */		'\u0489' /*     ҉_     */
                };

            string text3 = "";
            for (int i = 0; i < text.Length; i++)
            {
                text3 += text[i];

                if (i < text.Length - 1 && zalgo_mid.Contains(text[i + 1]))
                {
                    text3 += text[i + 1];
                    i++;
                    continue;
                }

                if (zalgo_mid.Contains(text[i]))
                {
                    continue;
                }

                if (capi.World.Rand.NextDouble() < str)
                {
                    text3 += zalgo_mid[capi.World.Rand.Next(zalgo_mid.Length)];
                }
            }

            return text3;
        }

        void initSoundsAndEffects()
        {
            capi = entity.Api as ICoreClientAPI;
            isSelf = capi.World.Player.Entity.EntityId == entity.EntityId;
            if (!isSelf) return;

            capi.Event.RegisterAsyncParticleSpawner(asyncParticleSpawn);

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

        private bool asyncParticleSpawn(float dt, IAsyncParticleManager manager)
        {
            if (isSelf && (fogEffectStrength > 0.05 || glitchEffectStrength > 0.05))
            {
                tmpPos.Set((int)entity.Pos.X, (int)entity.Pos.Y, (int)entity.Pos.Z);
                float sunb = capi.World.BlockAccessor.GetLightLevel(tmpPos, EnumLightLevelType.OnlySunLight) / 22f;

                float strength = Math.Min(1, (float)(glitchEffectStrength));

                double fognoise = fogEffectStrength * Math.Abs(fogNoise.Noise(0, capi.InWorldEllapsedMilliseconds / 1000f)) / 60f;

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

                rustParticles.Color = ColorUtil.ToRgba((int)(strength * 150), 50, 25, 15);
                rustParticles.MaxSize = 0.25f;
                rustParticles.RandomVelocityChange = false;

                

                rustParticles.MinVelocity.Set(0, 1, 0);
                rustParticles.AddVelocity.Set(0, 5, 0);

                rustParticles.LifeLength = 0.75f;

                Vec3d position = new Vec3d();
                EntityPos plrPos = capi.World.Player.Entity.Pos;

                float tries = 120 * strength;

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

            return true;
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
        public double stabilityOffset;

        float jitterOffset;
        float jitterOffsetedDuration;

        public override void OnGameTick(float deltaTime)
        {
            if (!enabled) return;

            if (requireInitSounds)
            {
                initSoundsAndEffects();
                requireInitSounds = false;
            }

            if (entity.World.Side == EnumAppSide.Client)
            {
                if (!(entity.World.Api as ICoreClientAPI).PlayerReadyFired) return;
            } else
            {
                
                IServerPlayer player = entity.World.PlayerByUid(((EntityPlayer)entity).PlayerUID) as IServerPlayer;
                if (player != null && player.ConnectionState != EnumClientState.Playing) return;
            }


            deltaTime = GameMath.Min(0.5f, deltaTime);

            float changeSpeed = deltaTime / 3;

            double hereStability = stabilityOffset + tempStabilitySystem.GetTemporalStability(entity.SidedPos.X, entity.SidedPos.Y, entity.SidedPos.Z);

            entity.Attributes.SetDouble("tempStabChangeVelocity", TempStabChangeVelocity);

            double gain = TempStabChangeVelocity > 0 ? (TempStabChangeVelocity / 200.0) : (TempStabChangeVelocity / 800.0);

            OwnStability = GameMath.Clamp(OwnStability + gain, 0f, 1);
            double ownStability = OwnStability;

            TempStabChangeVelocity = (hereTempStabChangeVelocity - TempStabChangeVelocity) * deltaTime;

            float glitchEffectExtraStrength = tempStabilitySystem.GetGlitchEffectExtraStrength();

            double targetGlitchEffectStrength = Math.Max(0, Math.Max(0, (0.2f - ownStability) * 1 / 0.2f) + glitchEffectExtraStrength);
            glitchEffectStrength += (targetGlitchEffectStrength - glitchEffectStrength) * changeSpeed;
            glitchEffectStrength = GameMath.Clamp(glitchEffectStrength, 0, 1.1f);

            double targetFogEffectStrength = Math.Max(0, Math.Max(0, (0.3f - ownStability) * 1 / 0.3f) + glitchEffectExtraStrength);
            fogEffectStrength += (targetFogEffectStrength - fogEffectStrength) * changeSpeed;
            fogEffectStrength = GameMath.Clamp(fogEffectStrength, 0, 0.9f);

            double targetRustPrecipStrength = Math.Max(0, Math.Max(0, (0.3f - ownStability) * 1 / 0.3f) + glitchEffectExtraStrength);
            rustPrecipColorStrength += (targetRustPrecipStrength - rustPrecipColorStrength) * changeSpeed;
            rustPrecipColorStrength = GameMath.Clamp(rustPrecipColorStrength, 0, 1f);

            if (precipParticleSys != null)
            {
                precipParticleSys.rainParticleColor = ColorUtil.ColorOverlay(WeatherSimulationParticles.waterColor, WeatherSimulationParticles.lowStabColor, (float)rustPrecipColorStrength);
            }


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

                float tempStormJitterStrength = 9;
                if (capi.Settings.Float.Exists("tempStormJitterStrength"))
                {
                    tempStormJitterStrength = capi.Settings.Float["tempStormJitterStrength"];
                }

                if (capi.World.Rand.NextDouble() < 0.015 && jitterOffset==0)
                {
                    jitterOffset = tempStormJitterStrength * (float)capi.World.Rand.NextDouble() + 3;
                    jitterOffsetedDuration = 0.25f + (float)capi.World.Rand.NextDouble() / 2f;

                    capi.Render.ShaderUniforms.WindWaveCounter += jitterOffset;// (float)(capi.World.Rand.NextDouble() < 0.015 ? tempStormJitterStrength * capi.World.Rand.NextDouble() : 0);
                    capi.Render.ShaderUniforms.WaterWaveCounter += jitterOffset;// (float)(capi.World.Rand.NextDouble() < 0.015 ? tempStormJitterStrength * capi.World.Rand.NextDouble() : 0);
                }

                if (jitterOffset > 0)
                {
                    capi.Render.ShaderUniforms.WindWaveCounter += (float)capi.World.Rand.NextDouble() / 2f - 1/4f;

                    jitterOffsetedDuration -= deltaTime;
                    if (jitterOffsetedDuration <= 0)
                    {
                        //capi.Render.ShaderUniforms.WindWaveCounter -= jitterOffset;
                        //capi.Render.ShaderUniforms.WaterWaveCounter -= jitterOffset;
                        jitterOffset = 0;
                    }
                }



                if (capi.World.Rand.NextDouble() < 0.002 && capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Survival)
                {
                    capi.Input.MouseYaw += (float)capi.World.Rand.NextDouble() * 0.125f - 0.125f/2;
                    capi.Input.MousePitch += (float)capi.World.Rand.NextDouble() * 0.125f - 0.125f/2;
                }

                double fognoise = fogEffectStrength * Math.Abs(fogNoise.Noise(0, capi.InWorldEllapsedMilliseconds/1000f)) / 60f;
                rainfogAmbient.FogDensity.Value = 0.05f + (float)fognoise;
            }
        }

        private void updateSoundsAndEffects(double hereStability, double ownStability)
        {
            if (!isSelf || tempStabSoundDrain == null) return;

            // Effects            

            float fadeSpeed = 3f;

            // Sounds

            if (hereStability < 0.95f && ownStability < 0.65f)
            {
                if (!tempStabSoundDrain.IsPlaying)
                {
                    tempStabSoundDrain.Start();
                }

                tempStabSoundDrain.FadeTo(Math.Min(1, 3 * ( 1 - hereStability)), 0.95f * fadeSpeed, (s) => {  });
            } else
            {
                tempStabSoundDrain.FadeTo(0, 0.95f * fadeSpeed, (s) => { tempStabSoundDrain.Stop(); });
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
                tempStabSoundLow.FadeTo(Math.Min(1, volume), 0.95f * fadeSpeed, (s) => {  });
            } else
            {
                tempStabSoundLow.FadeTo(0, 0.95f * fadeSpeed, (s) => { tempStabSoundLow.Stop(); });
            }

            if (ownStability < 0.25f)
            {
                if (!tempStabSoundVeryLow.IsPlaying)
                {
                    tempStabSoundVeryLow.Start();
                }

                float volume = (0.25f - (float)ownStability)*1/0.25f;
                tempStabSoundVeryLow.FadeTo(Math.Min(1, volume)/5f, 0.95f * fadeSpeed, (s) => { });
            } else
            {
                tempStabSoundVeryLow.FadeTo(0, 0.95f * fadeSpeed, (s) => { tempStabSoundVeryLow.Stop(); });
            }
        }


    }
}
