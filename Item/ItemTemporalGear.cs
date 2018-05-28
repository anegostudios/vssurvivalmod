using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class ItemTemporalGear : Item
    {
        public SimpleParticleProperties particlesHeld;

        public override void OnLoaded(ICoreAPI api)
        {
            particlesHeld = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ColorFromArgb(50, 220, 220, 220),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.1f, -0.1f, -0.1f),
                new Vec3f(0.1f, 0.1f, 0.1f),
                1.5f,
                0,
                0.5f,
                0.75f,
                EnumParticleModel.Cube
            );

            particlesHeld.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.6f);
            particlesHeld.addPos.Set(0, 0, 0);
            particlesHeld.addLifeLength = 0.5f;
        }

        public override void InGuiIdle(IWorldAccessor world, ItemStack stack)
        {
            GuiTransform.Rotation.Y = GameMath.Mod(world.ElapsedMilliseconds / 50f, 360);
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            GroundTransform.Rotation.Y = GameMath.Mod(entityItem.World.ElapsedMilliseconds / 50f, 360);

            if (entityItem.World is IClientWorldAccessor)
            {
                particlesHeld.minQuantity = 1;

                float angle = (entityItem.World.ElapsedMilliseconds / 15f + entityItem.Entityid * 20) % 360;
                float bobbing = entityItem.Collided ? GameMath.Sin(angle * GameMath.DEG2RAD) / 15 : 0;
                Vec3d pos = entityItem.LocalPos.XYZ;
                pos.Y += 0.15f + bobbing;

                SpawnParticles(entityItem.World, pos, false);
            }
        }

        public override void OnHeldIdle(IItemSlot slot, IEntityAgent byEntity)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                
                FpHandTransform.Rotation.Y = GameMath.Mod(byEntity.World.ElapsedMilliseconds / 50f, 360);
                TpHandTransform.Rotation.Y = GameMath.Mod(byEntity.World.ElapsedMilliseconds / 50f, 360);

                /*IRenderAPI rapi = (byEntity.World.Api as ICoreClientAPI).Render;
                Vec3d aboveHeadPos = byEntity.Pos.XYZ.Add(0, byEntity.EyeHeight() - 0.1f, 0);
                Vec3d pos = MatrixToolsd.Project(aboveHeadPos, rapi.PerspectiveProjectionMat, rapi.PerspectiveViewMat, rapi.ScreenWidth, rapi.ScreenHeight);

                
                particlesHeld.minSize = 0.05f;
                particlesHeld.minSize = 0.15f;

                SpawnParticles(byEntity.World, pos);*/
            }

        }

        public override bool OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            if (byEntity.World is IClientWorldAccessor)
            {
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
                ILoadedSound sound;
                byEntity.World.Api.ObjectCache["temporalGearSound"] = sound = world.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/effect/gears.ogg"),
                    ShouldLoop = true,
                    Position = blockSel.Position.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = 1f,
                    Pitch = 0.7f
                });

                sound?.Start();


                byEntity.World.RegisterCallback((dt) =>
                {
                    // Make sure the sound is stopped
                    if (byEntity.Controls.HandUse == EnumHandInteract.None)
                    {
                        sound?.Stop();
                        sound?.Dispose();
                    }

                }, 3600);

                byEntity.World.RegisterCallback((dt) =>
                {
                    // Make sure the sound is stopped
                    if (byEntity.Controls.HandUse == EnumHandInteract.None)
                    {
                        sound?.Stop();
                        sound?.Dispose();
                    }

                }, 20);
            }

            return true;
        }

        public override bool OnHeldInteractStep(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null || !(byEntity is EntityPlayer)) return false;
            
            FpHandTransform.Rotation.Y = GameMath.Mod(byEntity.World.ElapsedMilliseconds / (50f - secondsUsed * 20), 360);
            TpHandTransform.Rotation.Y = GameMath.Mod(byEntity.World.ElapsedMilliseconds / (50f - secondsUsed * 20), 360);

            if (byEntity.World is IClientWorldAccessor)
            {
                particlesHeld.minQuantity = 1;

                Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
                SpawnParticles(byEntity.World, pos, false);

                (byEntity.World as IClientWorldAccessor).ShakeCamera(0.035f);


                ILoadedSound sound = byEntity.World.Api.ObjectCache["temporalGearSound"] as ILoadedSound;
                sound?.SetPitch(0.7f + secondsUsed / 4);

            }

            return secondsUsed < 3.5;
        }

        public override void OnHeldInteractStop(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World.Side == EnumAppSide.Client)
            {
                ILoadedSound sound = byEntity.World.Api.ObjectCache["temporalGearSound"] as ILoadedSound;
                sound?.Stop();
                sound?.Dispose();
                //Console.WriteLine("sound stop");
            }

            if (blockSel == null || secondsUsed < 3.45) return;

            slot.TakeOut(1);
            slot.MarkDirty();

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                byEntity.World.PlaySoundAt(new AssetLocation("sounds/effect/portal.ogg"), byEntity, null, false);

                particlesHeld.minSize = 0.25f;
                particlesHeld.maxSize = 0.5f;
                particlesHeld.minQuantity = 300;
                Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
                SpawnParticles(byEntity.World, pos, true);
            }

            if (byEntity.World.Side == EnumAppSide.Server && byEntity is EntityPlayer)
            {
                IServerPlayer plr = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IServerPlayer;
                ICoreServerAPI sapi = byEntity.World.Api as ICoreServerAPI;

            //    plr.SendMessage(GlobalConstants.GeneralChatGroup, "Spawn point set", EnumChatType.Notification);

                BlockPos pos = blockSel.Position;
                plr.SetSpawnPosition(new PlayerSpawnPos(pos.X, pos.Y, pos.Z) { yaw = byEntity.ServerPos.Yaw, pitch = byEntity.ServerPos.Pitch });
            }

        }

        public override bool OnHeldInteractCancel(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            if (byEntity.World.Side == EnumAppSide.Client)
            {
                ILoadedSound sound = byEntity.World.Api.ObjectCache["temporalGearSound"] as ILoadedSound;
                sound?.Stop();
                sound?.Dispose();
               // Console.WriteLine("sound stop");
            }

            return true;
        }

        void SpawnParticles(IWorldAccessor world, Vec3d pos, bool final)
        {
            if (final || world.Rand.NextDouble() > 0.8)
            {
                int h = 110 + world.Rand.Next(15);
                int v = 100 + world.Rand.Next(50);

                particlesHeld.minPos = pos;
                particlesHeld.color = ColorUtil.ToBGRABytes(ColorUtil.HSV2ARGB(h, 180, v));
                world.SpawnParticles(particlesHeld);
            }
        }

    }
}
