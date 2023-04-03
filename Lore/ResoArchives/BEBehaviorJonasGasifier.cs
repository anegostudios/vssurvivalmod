using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BEBehaviorJonasGasifier : BlockEntityBehavior, INetworkedLight, IIgnitable
    {
        ControlPoint cp;
        bool lit;
        public bool HasFuel => !inventory[0].Empty;
        public bool Lit => lit;

        ModSystemControlPoints modSys;
        string networkCode;
        InventoryGeneric inventory;
        double burnStartTotalHours;

        public BEBehaviorJonasGasifier(BlockEntity blockentity) : base(blockentity)
        {
            inventory = new InventoryGeneric(1, null, null);
        }


        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            this.Api = api;
            registerToControlPoint();

            inventory.LateInitialize("jonasgasifier" + "-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);
            inventory.Pos = Pos;
            inventory.ResolveBlocksOrItems();

            Blockentity.RegisterGameTickListener(onTick, 2001, 12);

            base.Initialize(api, properties);            
        }

        private void onTick(float dt)
        {
            if (lit)
            {
                double hoursPassed = Api.World.Calendar.TotalHours - burnStartTotalHours;
                if (hoursPassed > 4)
                {
                    burnStartTotalHours = Api.World.Calendar.TotalHours;
                    inventory[0].TakeOut(1);
                    if (inventory.Empty)
                    {
                        lit = false;
                        updateState();
                    }
                }
            }
        }

        public void setNetwork(string networkCode)
        {
            this.networkCode = networkCode;
            registerToControlPoint();
            Blockentity.MarkDirty(true);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            lit = false;
        }

        void registerToControlPoint()
        {
            if (networkCode == null) return;

            modSys = Api.ModLoader.GetModSystem<ModSystemControlPoints>();
            var controlpointcode = AssetLocation.Create(networkCode, Block.Code.Domain);
            cp = modSys[controlpointcode];
            cp.ControlData = lit;
        }


        internal void Interact(IPlayer byPlayer, BlockSelection blockSel)
        {
            var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty) return;

            if (slot.Itemstack.Collectible.CombustibleProps != null && slot.Itemstack.Collectible.CombustibleProps.BurnTemperature >= 1100)
            {
                int moved = slot.TryPutInto(Api.World, inventory[0]);
                if (moved > 0)
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/block/charcoal"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, byPlayer);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    slot.MarkDirty();
                    Blockentity.MarkDirty(true);
                }
            }
        }

        void updateState()
        {
            if (cp != null)
            {
                cp.ControlData = lit;
                cp.Trigger();
            }
            Blockentity.MarkDirty(true);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            lit = tree.GetBool("lit");
            networkCode = tree.GetString("networkCode");
            if (networkCode == "") networkCode = null;

            burnStartTotalHours = tree.GetDouble("burnStartTotalHours");

            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("lit", lit);
            tree.SetString("networkCode", networkCode);
            tree.SetDouble("burnStartTotalHours", burnStartTotalHours);

            if (inventory != null)
            {
                ITreeAttribute invtree = new TreeAttribute();
                inventory.ToTreeAttributes(invtree);
                tree["inventory"] = invtree;
            }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if (Api.World.Side == EnumAppSide.Server)
            {
                inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            base.OnBlockBroken(byPlayer);
        }


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            foreach (var slot in inventory)
            {
                slot.Itemstack?.Collectible.OnStoreCollectibleMappings(Api.World, slot, blockIdMapping, itemIdMapping);
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            foreach (var slot in inventory)
            {
                if (slot.Itemstack == null) continue;

                if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
                {
                    slot.Itemstack = null;
                }
                else
                {
                    slot.Itemstack.Collectible.OnLoadCollectibleMappings(worldForResolve, slot, oldBlockIdMapping, oldItemIdMapping);
                }
            }
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (!inventory[0].Empty)
            {
                dsc.AppendLine(Lang.Get("Contents: {0}x {1}", inventory[0].StackSize, inventory[0].GetStackName()));
            }

            if (Api is ICoreClientAPI capi)
            {
                if (capi.Settings.Bool["extendedDebugInfo"] == true)
                {
                    dsc.AppendLine("network code: " + networkCode);
                    dsc.AppendLine(lit ? "On" : "Off");
                }
            }
        }

        public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            if (HasFuel && !lit)
            {
                return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
            }
            
            return EnumIgniteState.NotIgnitablePreventDefault;
        }

        public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            lit = true;
            burnStartTotalHours = Api.World.Calendar.TotalHours;
            updateState();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (HasFuel)
            {
                mesher.AddMeshData(genMesh(new AssetLocation("shapes/block/machine/jonas/gasifier-coal"+ (lit ? "-lit" : "") +".json")));
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        private MeshData genMesh(AssetLocation assetLocation)
        {
            return ObjectCacheUtil.GetOrCreate(Api, "gasifiermesh-" + assetLocation.Path + "-" + Block.Shape.rotateY, () =>
            {
                var shape = Api.Assets.TryGet(assetLocation).ToObject<Shape>();
                (Api as ICoreClientAPI).Tesselator.TesselateShape(Block, shape, out var mesh, Block.Shape.RotateXYZCopy);
                return mesh;
            });
        }
    }
}
