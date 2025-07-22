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
        public MultiTextureMeshRef? BaseMeshRef;
        public MultiTextureMeshRef? FluxMeshRef;
        public MultiTextureMeshRef? TopMeshRef;


        InventoryGeneric inv;

        public int hammerHits;
        AnvilPartRenderer? renderer;

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
            if (Api is not ICoreClientAPI capi) return;

            BaseMeshRef = capi.TesselatorManager.GetDefaultBlockMeshRef(Block);
            
            if (!inv[1].Empty && FluxMeshRef == null)
            {
                capi.Tesselator.TesselateShape(Block, API.Common.Shape.TryGet(Api, "shapes/block/metal/anvil/build-flux.json"), out MeshData meshdata);
                FluxMeshRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }
            
            if (!inv[2].Empty && TopMeshRef == null)
            {
                capi.Tesselator.TesselateShape(Block, API.Common.Shape.TryGet(Api, "shapes/block/metal/anvil/build-top.json"), out MeshData meshdata);
                TopMeshRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }
        }

        public override void OnBlockPlaced(ItemStack? byItemStack = null)
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

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            slot.Itemstack?.Collectible.DamageItem(Api.World, byPlayer.Entity, slot);

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
                    capi.TriggerIngameError(this, "bottomtoocold", Lang.GetWithFallback("weldanvil-bottomtoocold", "Bottom half to cold to weld, reheat the part on the forge."));
                }
                return false;
            }

            if (inv[1].Empty)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(this, "fluxmissing", Lang.GetWithFallback("weldanvil-fluxmissing", "Must apply powdered borax as next step."));
                }
                return false;
            }

            if (inv[2].Empty)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(this, "tophalfmissing", Lang.GetWithFallback("weldanvil-tophalfmissing", "Add the top half anvil first."));
                }
                return false;
            }

            if (inv[2].Itemstack.Collectible.GetTemperature(Api.World, inv[2].Itemstack) < 800)
            {
                if (triggerMessage && Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(this, "toptoocold", Lang.GetWithFallback("weldanvil-toptoocold", "Top half to cold to weld, reheat the part on the forge."));
                }
                return false;
            }

            return true;
        }


        public bool OnInteract(IPlayer byPlayer)
        {
            if (Block.Variant["part"] != "base") return false;

            ItemSlot hotbarslot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (inv[0].Itemstack.Collectible.GetTemperature(Api.World, inv[0].Itemstack) < 800)
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "bottomtoocold", Lang.GetWithFallback("weldanvil-bottomtoocold", "Bottom half to cold to weld, reheat the part on the forge."));
                return false;
            }

            if (inv[1].Empty)
            {
                if (hotbarslot.Itemstack?.Collectible?.Attributes?.IsTrue("isFlux") == true)
                {
                    inv[1].Itemstack = hotbarslot.TakeOut(1);
                    updateMeshRefs();
                    return true;

                } 
                else
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "fluxmissing", Lang.GetWithFallback("weldanvil-fluxmissing", "Must apply powdered borax as next step."));
                    return false;
                }
            }

            if (inv[2].Empty)
            {
                if (hotbarslot.Itemstack?.Block is BlockAnvilPart partBlock && partBlock.Variant["part"] == "top")
                {
                    if (partBlock.Variant["metal"] == Block.Variant["metal"])
                    {
                        Api.World.PlaySoundAt(partBlock.Sounds.Place, Pos, 0, byPlayer);
                        inv[2].Itemstack = hotbarslot.TakeOut(1);
                        updateMeshRefs();
                        return true;
                    }
                    else
                    {
                        (Api as ICoreClientAPI)?.TriggerIngameError(this, "wrongmetal", Lang.Get("weldanvil-wrongmetal"));
                        return false;
                    }
                }
                else
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "tophalfmissing", Lang.Get("weldanvil-tophalfmissing"));
                    return false;
                }
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
