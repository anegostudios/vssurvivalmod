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

    public class BlockEntityEchoChamber : BlockEntityContainer, IBlockShapeSupplier
    {
        internal InventoryGeneric inventory;
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "echochamber";

        MeshData baseMesh;
        MeshData needleMesh;
        MeshData discMesh;

        EchoChamberRenderer renderer;

        public bool IsPlaying;
             

        public BlockEntityEchoChamber()
        {
            inventory = new InventoryGeneric(1, null, null);
        }
        

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Client)
            {
                (api as ICoreClientAPI).Event.RegisterRenderer(renderer = new EchoChamberRenderer(pos, api as ICoreClientAPI, getRotation()), EnumRenderStage.Opaque);
                updateMeshesAndRenderer(api as ICoreClientAPI);

                RegisterGameTickListener(OnClientTick, 50);
            }
        }

        private void OnClientTick(float dt)
        {
            if (track?.Sound != null && track.Sound.IsPlaying)
            {
                Vec3d plrpos = (api as ICoreClientAPI).World.Player.Entity?.Pos?.XYZ;
                if (plrpos == null) return;

                float dist = GameMath.Sqrt(plrpos.SquareDistanceTo(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5));

                // 1/log(x + 2) - 0.7
                //http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiIxL2xvZyh4KzIpLTAuNyIsImNvbG9yIjoiIzAwMDAwMCJ9LHsidHlwZSI6MTAwMCwid2luZG93IjpbIi0yMi4zOTkzMzg5NDIzMDc2NTgiLCIzOC42MzU4MTczMDc2OTIyNyIsIi0xLjAwMzI5NTg5ODQzNzQ5OSIsIjIuMDQ4NDYxOTE0MDYyNDk4MiJdfV0-

                float volume = GameMath.Clamp(1 / (float)Math.Log10(Math.Max(1, dist - 1)) - 0.7f, 0, 1);

                track.Sound.SetVolume(volume);
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
                    world.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 1, 0.5));
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
            if (track != null || api.Side != EnumAppSide.Client) return;

            string trackstring = inventory[0].Itemstack.ItemAttributes["musicTrack"].AsString(null);
            if (trackstring == null) return;
            startLoadingMs = api.World.ElapsedMilliseconds;
            track = (api as ICoreClientAPI)?.StartTrack(new AssetLocation(trackstring), 99f, EnumSoundType.Sound, onTrackLoaded);

            api.World.PlaySoundAt(new AssetLocation("sounds/block/vinyl"), pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, null, false, 16);
            updateMeshesAndRenderer(api as ICoreClientAPI);
            wasStopped = false;
        }

        void StopMusic()
        {
            if (api.Side != EnumAppSide.Client) return;

            track?.Stop();
            track = null;
            api.Event.UnregisterCallback(handlerId);

            discMesh = null;
            updateMeshesAndRenderer(api as ICoreClientAPI);

            wasStopped = true;
        }

        private void onTrackLoaded(ILoadedSound sound)
        {
            if (track == null)
            {
                sound?.Dispose();
                return;
            }

            track.Sound = sound;

            long longMsPassed = api.World.ElapsedMilliseconds - startLoadingMs;
            handlerId = RegisterDelayedCallback((dt) => {
                if (!wasStopped) sound.Start();    
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
            capi.Tesselator.TesselateShape(capi.World.BlockAccessor.GetBlock(pos), shape, out needleMesh);

            return needleMesh;
        }

        private MeshData createBaseMesh(ICoreClientAPI capi)
        {
            Shape shape = capi.Assets.TryGet("shapes/block/wood/echochamber-base.json").ToObject<Shape>();
            MeshData mesh;
            capi.Tesselator.TesselateShape(capi.World.BlockAccessor.GetBlock(pos), shape, out mesh, new Vec3f(0, getRotation(), 0));

            return mesh;
        }

        int getRotation()
        {
            Block block = api.World.BlockAccessor.GetBlock(pos);

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
            if (!api.ObjectCache.TryGetValue(code, out obj))
            {
                MeshData mesh = onCreate(capi);
                api.ObjectCache[code] = mesh;
                return mesh;
            }
            else
            {
                return (MeshData)obj;
            }
        }


        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (!HasDisc) return false;

            mesher.AddMeshData(baseMesh);

            return true;
        }
        #endregion


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            IsPlaying = tree.GetBool("isplaying", false);

            if (worldForResolving.Side == EnumAppSide.Client && this.api != null)
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
            renderer?.Unregister();
            track?.Stop();
            track = null;
            api.Event.UnregisterCallback(handlerId);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            renderer?.Unregister();
            track?.Stop();
            track = null;
            api.Event.UnregisterCallback(handlerId);
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            return "";
        }

    }
}
