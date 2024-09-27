using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityBaseReturnTeleporter : BlockEntity
    {
        public ILoadedSound translocatingSound;
        float spinupAccum = 0;
        bool activated = false;
        float translocVolume = 0;
        float translocPitch = 0;

        public bool Activated => activated;

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            setupGameTickers();

            if (api.World.Side == EnumAppSide.Client)
            {
                float rotY = Block.Shape.rotateY;
                animUtil.InitializeAnimator("basereturnteleporter", null, null, new Vec3f(0, rotY, 0));

                translocatingSound = (api as ICoreClientAPI).World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/effect/translocate-active.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f(),
                    RelativePosition = false,
                    DisposeOnFinish = false,
                    Volume = 0.5f
                });
            }
        }


        public void setupGameTickers()
        {
            if (Api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerGameTick, 250);
            }
            else
            {
                RegisterGameTickListener(OnClientGameTick, 50);
            }
        }

        private void OnServerGameTick(float dt)
        {
            if (!activated) return;

            spinupAccum += dt;

            if (spinupAccum > 5)
            {
                activated = false;
                var plr = Api.World.NearestPlayer(Pos.X + 0.5, Pos.InternalY + 0.5, Pos.Z + 0.5) as IServerPlayer;
                if (plr.Entity.Pos.DistanceTo(Pos.ToVec3d().Add(0.5, 0, 0.5)) < 5)
                {
                    var pos = plr.GetSpawnPosition(false);
                    plr.Entity.TeleportToDouble(pos.X, pos.Y, pos.Z);

                    Api.World.PlaySoundAt(new AssetLocation("sounds/effect/translocate-breakdimension"), plr.Entity.Pos.X, plr.Entity.Pos.InternalY, plr.Entity.Pos.Z, null, false, 16);
                }

                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/translocate-breakdimension"), Pos.X + 0.5f, Pos.InternalY + 0.5f, Pos.Z + 0.5f, null, false, 16);

                int color = ColorUtil.ToRgba(100, 220, 220, 220);
                Api.World.SpawnParticles(120, color, Pos.ToVec3d(), Pos.ToVec3d().Add(1, 1, 1), new Vec3f(-1, -1, -1), new Vec3f(1, 1, 1), 2, 0, 1);

                color = ColorUtil.ToRgba(255, 53, 221, 172);
                Api.World.SpawnParticles(100, color, Pos.ToVec3d().Add(0, 0.25, 0), Pos.ToVec3d().Add(1, 1.25, 1), new Vec3f(-4, 0, -4), new Vec3f(4, 4, 4), 2, 0.6f, 0.8f, EnumParticleModel.Cube);


                var block = Api.World.GetBlock(new AssetLocation("basereturnteleporter-fried"));
                Api.World.BlockAccessor.SetBlock(block.Id, Pos);
            }            
        }

        private void OnClientGameTick(float dt)
        {
            HandleSoundClient(dt);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            var meta = new AnimationMetaData() { Animation = "deploy", Code = "deploy", AnimationSpeed = 1, EaseInSpeed = 3, EaseOutSpeed = 2, Weight = 1, BlendMode = EnumAnimationBlendMode.Average };
            animUtil.StartAnimation(meta);
        }

        public void OnInteract(IPlayer byPlayer)
        {
            if (activated)
            {
                activated = false;
                animUtil.StopAnimation("active");
                MarkDirty(true);
                return;
            }

            activated = true;
            MarkDirty(true);
            var meta = new AnimationMetaData() { Animation = "active", Code = "active", AnimationSpeed = 1, EaseInSpeed = 1, EaseOutSpeed = 2, Weight = 1, BlendMode = EnumAnimationBlendMode.Add };
            animUtil.StartAnimation(meta);
        }


        protected void HandleSoundClient(float dt)
        {
            var capi = Api as ICoreClientAPI;

            if (activated)
            {
                translocVolume = Math.Min(0.5f, translocVolume + dt / 3);
                translocPitch = Math.Min(translocPitch + dt / 3, 2.5f);
                if (capi != null && capi.World.Player.Entity.Pos.DistanceTo(Pos.ToVec3d().Add(0.5, 0, 0.5)) < 5)
                {
                    capi.World.AddCameraShake(0.0575f);
                }
            }
            else
            {
                translocVolume = Math.Max(0, translocVolume - 2 * dt);
                translocPitch = Math.Max(translocPitch - dt, 0.5f);
            }


            if (translocatingSound.IsPlaying)
            {
                translocatingSound.SetVolume(translocVolume);
                translocatingSound.SetPitch(translocPitch);
                if (translocVolume <= 0) translocatingSound.Stop();
            }
            else
            {
                if (translocVolume > 0) translocatingSound.Start();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("activated", activated);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            activated = tree.GetBool("activated");

            if (activated && Api?.Side == EnumAppSide.Client)
            {
                var meta = new AnimationMetaData() { Animation = "active", Code = "active", AnimationSpeed = 1, EaseInSpeed = 1, EaseOutSpeed = 2, Weight = 1, BlendMode = EnumAnimationBlendMode.Add };
                animUtil.StartAnimation(meta);
            }
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            translocatingSound?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            translocatingSound?.Dispose();
        }
    }

    public class BlockBaseReturnTeleporter : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            interactions = new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-basereturn-activate",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) =>
                    {
                        if (bs == null) return false;
                        var be = GetBlockEntity<BlockEntityBaseReturnTeleporter>(bs.Position);
                        return be != null && !be.Activated;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-basereturn-deactivate",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) =>
                    {
                        if (bs == null) return false;
                        var be = GetBlockEntity<BlockEntityBaseReturnTeleporter>(bs.Position);
                        return be != null && be.Activated;
                    }
                }
            };

            base.OnLoaded(api);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.Entity.Controls.ShiftKey)
            {
                var be = GetBlockEntity<BlockEntityBaseReturnTeleporter>(blockSel.Position);
                be?.OnInteract(byPlayer);
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
