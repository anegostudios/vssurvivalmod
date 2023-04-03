using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{

    public class BlockEntityCorpseReturnTeleporter : BlockEntityTeleporterBase
    {
        public ILoadedSound translocatingSound;

        bool HasFuel = true;
        ICoreServerAPI sapi;
        BlockCorpseReturnTeleporter ownBlock;
        bool canTeleport = false;
        long somebodyIsTeleportingReceivedTotalMs;
        float translocVolume = 0;
        float translocPitch = 0;

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
        }

        public BlockEntityCorpseReturnTeleporter()
        {
            TeleportWarmupSec = 4.4f;
            canTeleport = true;
        }

        protected override Vec3d GetTarget(Entity forEntity)
        {
            if (forEntity is EntityPlayer eplr)
            {
                var plr = eplr.Player as IServerPlayer;
                if (Api.ModLoader.GetModSystem<ModSystemCorpseReturnTeleporter>().lastDeathLocations.TryGetValue(plr.PlayerUID, out var location))
                {
                    return location;
                }
            }

            return null;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            setupGameTickers();

            ownBlock = this.Block as BlockCorpseReturnTeleporter;

            if (api.World.Side == EnumAppSide.Client)
            {
                float rotY = Block.Shape.rotateY;
                animUtil.InitializeAnimator("translocator", null, null, new Vec3f(0, rotY, 0));

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
                sapi = Api as ICoreServerAPI;
                RegisterGameTickListener(OnServerGameTick, 250);
            }
            else
            {
                RegisterGameTickListener(OnClientGameTick, 50);
            }
        }



        private void OnServerGameTick(float dt)
        {
            if (canTeleport)
            {
                HandleTeleportingServer(dt);
            }
        }

        protected override void didTeleport(Entity entity)
        {
            if (entity is EntityPlayer)
            {
                manager.DidTranslocateServer((entity as EntityPlayer).Player as IServerPlayer);
            }

            HasFuel = false;
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

        private void OnClientGameTick(float dt)
        {
            if (ownBlock == null || Api?.World == null || !canTeleport) return;

            if (Api.World.ElapsedMilliseconds - somebodyIsTeleportingReceivedTotalMs > 6000)
            {
                somebodyIsTeleporting = false;
            }

            HandleSoundClient(dt);

            bool selfInside = (Api.World.ElapsedMilliseconds > 100 && Api.World.ElapsedMilliseconds - lastOwnPlayerCollideMs < 100);
            bool playerInside = selfInside || somebodyIsTeleporting;
            bool active = animUtil.activeAnimationsByAnimCode.ContainsKey("teleport");

            if (!selfInside && playerInside)
            {
                manager.lastTranslocateCollideMsOtherPlayer = Api.World.ElapsedMilliseconds;
            }



            if (playerInside)
            {
                var meta = new AnimationMetaData() { Animation = "teleport", Code = "teleport", AnimationSpeed = 1, EaseInSpeed = 1, EaseOutSpeed = 2, Weight = 1, BlendMode = EnumAnimationBlendMode.Add };
                animUtil.StartAnimation(meta);
                animUtil.StartAnimation(new AnimationMetaData() { Animation = "idle", Code = "idle", AnimationSpeed = 1, EaseInSpeed = 1, EaseOutSpeed = 1, Weight = 1, BlendMode = EnumAnimationBlendMode.Average });
            }
            else
            {
                animUtil.StopAnimation("teleport");
            }


            if (animUtil.activeAnimationsByAnimCode.Count > 0 && Api.World.ElapsedMilliseconds - lastOwnPlayerCollideMs > 10000 && Api.World.ElapsedMilliseconds - manager.lastTranslocateCollideMsOtherPlayer > 10000)
            {
                animUtil.StopAnimation("idle");
            }
        }

        protected virtual void HandleSoundClient(float dt)
        {
            var capi = Api as ICoreClientAPI;
            bool ownTranslocate = !(capi.World.ElapsedMilliseconds - lastOwnPlayerCollideMs > 200);
            bool otherTranslocate = !(capi.World.ElapsedMilliseconds - lastEntityCollideMs > 200);

            if (ownTranslocate || otherTranslocate)
            {
                translocVolume = Math.Min(0.5f, translocVolume + dt / 3);
                translocPitch = Math.Min(translocPitch + dt / 3, 2.5f);
                if (ownTranslocate) capi.World.AddCameraShake(0.0575f);
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


        public override void OnEntityCollide(Entity entity)
        {
            if (!HasFuel) return;

            base.OnEntityCollide(entity);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            if (worldAccessForResolve != null && worldAccessForResolve.Side == EnumAppSide.Client)
            {
                somebodyIsTeleportingReceivedTotalMs = worldAccessForResolve.ElapsedMilliseconds;

                if (tree.GetBool("somebodyDidTeleport"))
                {
                    // Might get called from the SystemNetworkProcess thread
                    worldAccessForResolve.Api.Event.EnqueueMainThreadTask(
                        () => worldAccessForResolve.PlaySoundAt(new AssetLocation("sounds/effect/translocate-breakdimension"), Pos.X + 0.5f, Pos.Y + 0.5f, Pos.Z + 0.5f, null, false, 16),
                        "playtelesound"
                    );
                }
            }

            HasFuel = tree.GetBool("hasFuel");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("hasFuel", HasFuel);
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            if (!HasFuel)
            {
                var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (slot.Itemstack.ItemAttributes?.IsTrue("corpseReturnFuel") == true)
                {
                    HasFuel = true;
                    if (Api.Side == EnumAppSide.Server) slot.TakeOut(1);
                    (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    MarkDirty(true);
                }

                return true;
            }

            return false;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (!HasFuel) dsc.AppendLine(Lang.Get("No fuel, add temporal gear"));
        }
    }

    

    public class BlockCorpseReturnTeleporter : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityCorpseReturnTeleporter be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCorpseReturnTeleporter;
            be.OnInteract(byPlayer);
            return true;
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);

            BlockEntityCorpseReturnTeleporter be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCorpseReturnTeleporter;
            if (be == null) return;
            be.OnEntityCollide(entity);
        }

    }


    public class ModSystemCorpseReturnTeleporter : ModSystem
    {
        public Dictionary<string, Vec3d> lastDeathLocations = new Dictionary<string, Vec3d>();
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.PlayerDeath += Event_PlayerDeath;
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;

            sapi = api;
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("lastDeathLocations", lastDeathLocations);
        }

        private void Event_SaveGameLoaded()
        {
            lastDeathLocations = sapi.WorldManager.SaveGame.GetData<Dictionary<string, Vec3d>>("lastDeathLocations", new Dictionary<string, Vec3d>());
        }

        private void Event_PlayerDeath(IServerPlayer byPlayer, DamageSource damageSource)
        {
            lastDeathLocations[byPlayer.PlayerUID] = byPlayer.Entity.Pos.XYZ;
        }


    }
}
