using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public delegate MeshData CreateMeshDelegate(ICoreClientAPI capi);

    public class BlockEntityEchoChamber : BlockEntityContainer
    {
        internal InventoryGeneric inventory;
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "echochamber";

        MeshData baseMesh;
        MeshData needleMesh;
        MeshData discMesh;

        EchoChamberRenderer renderer;

        public bool IsPlaying;
        //float prevPitch;

        public BlockEntityEchoChamber()
        {
            inventory = new InventoryGeneric(1, null, null);
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Client)
            {
                (api as ICoreClientAPI).Event.RegisterRenderer(renderer = new EchoChamberRenderer(Pos, api as ICoreClientAPI, getRotation()), EnumRenderStage.Opaque, "echochamber");
                updateMeshesAndRenderer(api as ICoreClientAPI);

                RegisterGameTickListener(OnClientTick, 50);
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

                // 1/log(x * 0.5)-0.8
                // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiIxL2xvZyh4KjAuNSktMC44IiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiMCIsIjUwIiwiMCIsIjEiXSwic2l6ZSI6WzY0OCwzOThdfV0-

                float volume = GameMath.Clamp(1 / (float)Math.Log10(Math.Max(1, dist * 0.5)) - 0.8f, 0, 1);

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
                slot.MarkDirty();
                StartMusic();
                IsPlaying = true;
                MarkDirty(true);
                
            }   
        }

        #region music start/stop

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
            track = (Api as ICoreClientAPI)?.StartTrack(new AssetLocation(trackstring), 99f, EnumSoundType.AmbientGlitchunaffected, onTrackLoaded);

            wasStopped = false;
            Api.World.PlaySoundAt(new AssetLocation("sounds/block/vinyl"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, false, 32);
            updateMeshesAndRenderer(Api as ICoreClientAPI);
        }

        void StopMusic()
        {
            if (Api.Side != EnumAppSide.Client) return;

            track?.Stop();
            track = null;
            Api.Event.UnregisterCallback(handlerId);

            discMesh = null;
            updateMeshesAndRenderer(Api as ICoreClientAPI);

            wasStopped = true;
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
                    Api.World.Logger.Notification("Echo chamber track is diposed? o.O");
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
            if (baseMesh == null) baseMesh = getOrCreateMesh(capi, "echoChamberBaseMesh"+ getRotation(), (cp) => createBaseMesh(cp));

            if (HasDisc)
            {
                if (needleMesh == null) needleMesh = getOrCreateMesh(capi, "echoChamberNeedleMesh", (cp) => createNeedleMesh(cp));
                if (discMesh == null) discMesh = getOrCreateMesh(capi, "echoChamberDisc" + inventory[0].Itemstack.Collectible.LastCodePart() + "Mesh", (cp) => createDiscMesh(cp));
            } else
            {
                needleMesh = null;
                discMesh = null;
            }

            renderer.UpdateMeshes(needleMesh, discMesh);
        }



        private MeshData createDiscMesh(ICoreClientAPI cp)
        {
            MeshData discMesh;
            cp.Tesselator.TesselateItem(inventory[0].Itemstack.Item, out discMesh);
            return discMesh;
        }

        private MeshData createNeedleMesh(ICoreClientAPI capi)
        {
            Shape shape = capi.Assets.TryGet("shapes/block/wood/echochamber-needle.json").ToObject<Shape>();
            MeshData needleMesh;
            capi.Tesselator.TesselateShape(capi.World.BlockAccessor.GetBlock(Pos), shape, out needleMesh);

            return needleMesh;
        }

        private MeshData createBaseMesh(ICoreClientAPI capi)
        {
            Shape shape = capi.Assets.TryGet("shapes/block/wood/echochamber-base.json").ToObject<Shape>();
            MeshData mesh;
            capi.Tesselator.TesselateShape(capi.World.BlockAccessor.GetBlock(Pos), shape, out mesh, new Vec3f(0, getRotation(), 0));

            return mesh;
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


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (!HasDisc) return false;

            mesher.AddMeshData(baseMesh);

            return true;
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
