using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{

    public class EntityBeeMob : EntityAgent
    {
        ILoadedSound buzzSound;
        Vec3f soundpos;

        public override bool IsInteractable { get { return false; } }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            if (buzzSound == null && World.Side == EnumAppSide.Client)
            {
                buzzSound = ((IClientWorldAccessor)World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/creature/beemob.ogg"),
                    ShouldLoop = true,
                    Position = soundpos = Pos.XYZ.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    SoundType = EnumSoundType.Entity,
                    Volume = 0.25f
                });

                buzzSound.Start();
            }

            AnimManager.StartAnimation("enraged");
        }

        public override void OnGameTick(float dt)
        {
            if (soundpos != null)
            {
                soundpos.X = (float)Pos.X;
                soundpos.Y = (float)Pos.Y;
                soundpos.Z = (float)Pos.Z;
                buzzSound.SetPosition(soundpos);
            }
            
            base.OnGameTick(dt);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            buzzSound?.Stop();
            buzzSound?.Dispose();

            base.OnEntityDespawn(despawn);
        }

        

        public override void OnReceivedServerAnimations(int[] activeAnimations, int activeAnimationsCount, float[] activeAnimationSpeeds)
        {
            // We control animations entirely client side
        }
    }
}
