using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityAnvilPart : BlockEntityContainer
    {
        public MultiTextureMeshRef BaseMeshRef;
        public MultiTextureMeshRef FluxMeshRef;
        public MultiTextureMeshRef TopMeshRef;


        InventoryGeneric inv;

        public int hammerHits;
        AnvilPartRenderer renderer;

        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "anvilpart";

        public BlockEntityAnvilPart()
        {
            inv = new InventoryGeneric(3, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI capi)
            {
                renderer = new AnvilPartRenderer(capi, this);
                updateMeshRefs();
            }
        }

        void updateMeshRefs()
        {
            if (Api.Side == EnumAppSide.Server) return;

            var capi = Api as ICoreClientAPI;

            BaseMeshRef = capi.TesselatorManager.GetDefaultBlockMeshRef(Block);
            
            if (!inv[1].Empty && FluxMeshRef == null)
            {
                MeshData meshdata;
                capi.Tesselator.TesselateShape(Block, API.Common.Shape.TryGet(Api, "shapes/block/metal/anvil/build-flux.json"), out meshdata);
                FluxMeshRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }
            
            if (!inv[2].Empty && TopMeshRef == null)
            {
                MeshData meshdata;
                capi.Tesselator.TesselateShape(Block, API.Common.Shape.TryGet(Api, "shapes/block/metal/anvil/build-top.json"), out meshdata);
                TopMeshRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack != null)
            {
                inv[0].Itemstack = byItemStack.Clone();
                inv[0].Itemstack.StackSize = 1;
            }
        }


        public void OnHammerHitOver(IPlayer byPlayer, Vec3d hitPosition)
        {
            if (inv[1].Empty || inv[2].Empty || !TestReadyToMerge(false)) return;

            hammerHits++;

            float temp = inv[2].Itemstack.Collectible.GetTemperature(Api.World, inv[2].Itemstack);
            if (temp > 800)
            {
                BlockEntityAnvil.bigMetalSparks.MinPos = Pos.ToVec3d().Add(hitPosition.X, hitPosition.Y, hitPosition.Z);
                BlockEntityAnvil.bigMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 700) / 2, 32, 128);
                Api.World.SpawnParticles(BlockEntityAnvil.bigMetalSparks, byPlayer);

                BlockEntityAnvil.smallMetalSparks.MinPos = Pos.ToVec3d().Add(hitPosition.X, hitPosition.Y, hitPosition.Z);
                BlockEntityAnvil.smallMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 770) / 3, 32, 128);
                Api.World.SpawnParticles(BlockEntityAnvil.smallMetalSparks, byPlayer);
            }

            if (hammerHits > 11 && Api.Side == EnumAppSide.Server)
            {
                Api.World.BlockAccessor.SetBlock(Api.World.GetBlock(new AssetLocation("anvil-" + Block.Variant["metal"])).Id, Pos);
            }
        }

        public bool TestReadyToMerge(bool triggerMessage = true)
        {
            if (inv[0].Itemstack.Collectible.GetTemperature(Api.World, inv[0].Itemstack) < 800)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(this, "toocold", Lang.Get("Bottom half to cold to weld, reheat the part on the forge."));
                }
                return false;
            }

            if (inv[1].Empty)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(this, "fluxmissing", Lang.Get("Add powdered borax as flux first."));
                }
                return false;
            }

            if (inv[2].Empty)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(this, "tophalfmissing", Lang.Get("Add the top half anvil first."));
                }
                return false;
            }

            if (inv[2].Itemstack.Collectible.GetTemperature(Api.World, inv[0].Itemstack) < 800)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(this, "toocold", Lang.Get("Top half to cold to weld, reheat the part on the forge."));
                }
                return false;
            }

            return true;
        }


        public bool OnInteract(IPlayer byPlayer)
        {
            if (Block.Variant["part"] != "base") return false;

            ItemSlot hotbarslot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (inv[0].Itemstack.Collectible.GetTemperature(Api.World, inv[0].Itemstack) < 800) {

                if (Api.Side == EnumAppSide.Client)
                {
                    (Api as ICoreClientAPI).TriggerIngameError(this, "toocold", Lang.Get("Too cold to weld, reheat the part on the forge."));
                }

                return false;
            }

            if (inv[1].Empty)
            {
                if (hotbarslot.Itemstack?.Collectible?.Code.Path == "powderedborax")
                {
                    inv[1].Itemstack = hotbarslot.TakeOut(1);
                    updateMeshRefs();
                    return true;

                } else
                {
                    if (Api.Side == EnumAppSide.Client)
                    {
                        (Api as ICoreClientAPI).TriggerIngameError(this, "toocold", Lang.Get("Must apply powdered borax as next step."));
                    }
                    return false;
                }
            }

            if (inv[2].Empty)
            {
                if (hotbarslot.Itemstack?.Block is BlockAnvilPart && hotbarslot.Itemstack.Block.Variant["part"] == "top")
                {
                    Api.World.PlaySoundAt(Block.Sounds.Place, Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, byPlayer);
                    inv[2].Itemstack = hotbarslot.TakeOut(1);
                    updateMeshRefs();
                    return true;
                }

                return false;
            }


            return true;
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Dispose();

            // BaseMeshRef is the engine default mesh, we don't need to dispose it
            FluxMeshRef?.Dispose();
            TopMeshRef?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();
            // BaseMeshRef is the engine default mesh, we don't need to dispose it
            FluxMeshRef?.Dispose();
            TopMeshRef?.Dispose();
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            return true;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            
        }

    }
}
