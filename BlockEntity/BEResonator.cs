using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public delegate MeshData CreateMeshDelegate(ICoreClientAPI capi);

    public class BlockEntityResonator : BlockEntityContainer
    {
        internal InventoryGeneric inventory;
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "echochamber";

        MeshData cylinderMesh;

        ResonatorRenderer renderer;
        public bool IsPlaying;

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        }

        public BlockEntityResonator()
        {
            inventory = new InventoryGeneric(1, null, null);
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Client)
            {
                (api as ICoreClientAPI).Event.RegisterRenderer(renderer = new ResonatorRenderer(Pos, api as ICoreClientAPI, getRotation()), EnumRenderStage.Opaque, "resonator");
                updateMeshesAndRenderer(api as ICoreClientAPI);

                RegisterGameTickListener(OnClientTick, 50);
                animUtil?.InitializeAnimator("resonator", null, null, new Vec3f(0, getRotation(), 0));
            }
        }

        private void OnClientTick(float dt)
        {
            if (track?.Sound != null && track.Sound.IsPlaying)
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                Vec3d plrpos = capi.World.Player.Entity?.Pos?.XYZ;
                if (plrpos == null) return;

                float dist = GameMath.Sqrt(plrpos.SquareDistanceTo(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5));

                // 1/log(x * 0.7)-0.8
                // https://www.desmos.com/calculator/e9rejsvrcj-

                float volume = GameMath.Clamp(1 / (float)Math.Log10(Math.Max(1, dist * 0.7)) - 0.8f, 0, 1);

                track.Sound.SetVolume(volume);
                track.Sound.SetPitch(GameMath.Clamp(1 - capi.Render.ShaderUniforms.GlitchStrength, 0.1f, 1));
            }
        }

        public void OnInteract(IWorldAccessor world, IPlayer byPlayer)
        {
            if (HasDisc)
            {
                ItemStack stack = inventory[0].Itemstack;
                inventory[0].Itemstack = null;
                inventory[0].MarkDirty();

                if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                {
                    world.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 1, 0.5));
                }
                Api.World.Logger.Audit("{0} Took 1x{1} from Resonator at {2}.",
                    byPlayer.PlayerName,
                    stack.Collectible.Code,
                    Pos
                );

                StopMusic();
                IsPlaying = false;
                MarkDirty(true);
                return;
            }

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack?.ItemAttributes == null) return;

            if (slot.Itemstack.ItemAttributes["isPlayableDisc"].AsBool(false) == true)
            {
                string track = slot.Itemstack.ItemAttributes["musicTrack"].AsString(null);
                if (track == null) return;

                inventory[0].Itemstack = slot.TakeOut(1);

                Api.World.Logger.Audit("{0} Put 1x{1} into Resonator at {2}.",
                    byPlayer.PlayerName,
                    inventory[0].Itemstack.Collectible.Code,
                    Pos
                );
                slot.MarkDirty();
                StartMusic();
                IsPlaying = true;
                MarkDirty(true);

            }
        }

        #region Music start/stop

        MusicTrack track;
        long startLoadingMs;
        long handlerId;
        bool wasStopped;

        void StartMusic()
        {
            if (track != null || Api.Side != EnumAppSide.Client) return;

            string trackstring = inventory[0].Itemstack.ItemAttributes["musicTrack"].AsString(null);
            if (trackstring == null) return;
            startLoadingMs = Api.World.ElapsedMilliseconds;
            track = (Api as ICoreClientAPI)?.StartTrack(new AssetLocation(trackstring), 99f, EnumSoundType.MusicGlitchunaffected, onTrackLoaded);

            wasStopped = false;
            Api.World.PlaySoundAt(new AssetLocation("sounds/block/vinyl"), Pos, 0, null, false, 32);
            updateMeshesAndRenderer(Api as ICoreClientAPI);

            animUtil.StartAnimation(new AnimationMetaData()
            {
                Animation = "running",
                Code = "running",
                AnimationSpeed = 1f,
                EaseOutSpeed = 8,
                EaseInSpeed = 8
            });
        }

        void StopMusic()
        {
            if (Api.Side != EnumAppSide.Client) return;

            track?.Stop();
            track = null;
            Api.Event.UnregisterCallback(handlerId);

            cylinderMesh = null;
            updateMeshesAndRenderer(Api as ICoreClientAPI);

            wasStopped = true;

            animUtil.StopAnimation("running");
        }

        private void onTrackLoaded(ILoadedSound sound)
        {
            if (track == null)
            {
                sound?.Dispose();
                return;
            }
            if (sound == null) return;

            track.Sound = sound;

            // Needed so that the music engine does not dispose the sound
            Api.Event.EnqueueMainThreadTask(() => track.loading = true, "settrackloading");

            long longMsPassed = Api.World.ElapsedMilliseconds - startLoadingMs;
            handlerId = RegisterDelayedCallback((dt) => {
                if (sound.IsDisposed)
                {
                    Api.World.Logger.Notification("Resonator track is diposed? o.O");
                }

                if (!wasStopped)
                {
                    sound.Start();
                }

                track.loading = false;

            }, (int)Math.Max(0, 500 - longMsPassed));
        }

        #endregion

        #region mesh stuff

        private void updateMeshesAndRenderer(ICoreClientAPI capi)
        {
            if (HasDisc)
            {
                if (cylinderMesh == null) cylinderMesh = getOrCreateMesh(capi, "resonatorTuningCylinder" + inventory[0].Itemstack.Collectible.LastCodePart() + "Mesh", (cp) => createCylinderMesh(cp));
            } else
            {
                cylinderMesh = null;
            }

            renderer.UpdateMeshes(cylinderMesh);
        }



        private MeshData createCylinderMesh(ICoreClientAPI cp)
        {
            MeshData discMesh;
            cp.Tesselator.TesselateItem(inventory[0].Itemstack.Item, out discMesh);
            return discMesh;
        }


        int getRotation()
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);

            int rot = 0;
            switch (block.LastCodePart())
            {
                case "north": rot = 0; break;
                case "east": rot = 270; break;
                case "south": rot = 180; break;
                case "west": rot = 90; break;
            }

            return rot;
        }

        MeshData getOrCreateMesh(ICoreClientAPI capi, string code, CreateMeshDelegate onCreate)
        {
            object obj;
            if (!Api.ObjectCache.TryGetValue(code, out obj))
            {
                MeshData mesh = onCreate(capi);
                Api.ObjectCache[code] = mesh;
                return mesh;
            }
            else
            {
                return (MeshData)obj;
            }
        }


        #endregion


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            IsPlaying = tree.GetBool("isplaying", false);

            if (worldForResolving.Side == EnumAppSide.Client && this.Api != null)
            {
                if (IsPlaying && inventory[0]?.Itemstack != null) StartMusic();
                else StopMusic();

                MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("isplaying", IsPlaying);
        }

        public bool HasDisc
        {
            get { return !inventory[0].Empty; }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            renderer?.Dispose();
            track?.Stop();
            track = null;
            Api?.Event.UnregisterCallback(handlerId);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            renderer?.Dispose();
            track?.Stop();
            track = null;
            Api.Event.UnregisterCallback(handlerId);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            // Remove perish rate thing

        }

    }
}
