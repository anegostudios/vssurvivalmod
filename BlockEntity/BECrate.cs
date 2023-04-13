using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityCrate : BlockEntityContainer
    {
        InventoryGeneric inventory;
        BlockCrate ownBlock;

        public string type = "wood-aged";
        public string label = null;
        public string preferredLidState = "closed";
        public int quantitySlots = 16;
        public bool retrieveOnly = false;
        float rotAngleY;

        MeshData ownMesh;
        MeshData labelMesh;
        ICoreClientAPI capi;

        Cuboidf selBoxCrate;
        Cuboidf selBoxLabel;

        int labelColor;
        
        ItemStack labelStack;


        public virtual float MeshAngle
        {
            get { return rotAngleY; }
            set
            {
                rotAngleY = value;
            }
        }


        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "crate";

        public LabelProps LabelProps
        {
            get
            {
                if (label == null) return null;
                ownBlock.Props.Labels.TryGetValue(label, out var prop);
                return prop;
            }
        }

        public string LidState
        {
            get
            {
                if (inventory.Empty) return preferredLidState;
                var stack = inventory.FirstNonEmptySlot.Itemstack;

                bool? displayInsideCrate = stack.ItemAttributes?["displayInsideCrate"].Exists != true ? null : stack.ItemAttributes?["displayInsideCrate"].AsBool(true);

                bool hasContentTexture = stack.ItemAttributes?["inContainerTexture"].Exists == true || (stack.Block != null && stack.Block.DrawType == EnumDrawType.Cube && displayInsideCrate != false) || displayInsideCrate == true;

                return hasContentTexture ? preferredLidState : "closed";
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            ownBlock = Block as BlockCrate;
            capi = api as ICoreClientAPI;

            bool isNewlyplaced = inventory == null;
            if (isNewlyplaced)
            {
                InitInventory(Block);
            }

            base.Initialize(api);

            if (api.Side == EnumAppSide.Client && !isNewlyplaced)
            {
                loadOrCreateMesh();
            }

            if (Api.Side == EnumAppSide.Server)
            {
                inventory.SlotModified += Inventory_SlotModified;
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack?.Attributes != null)
            {
                string nowType = byItemStack.Attributes.GetString("type", ownBlock.Props.DefaultType);
                string nowLabel = byItemStack.Attributes.GetString("label");
                string nowLidState = byItemStack.Attributes.GetString("lidState", "closed");

                if (nowType != type || nowLabel != label || nowLidState != preferredLidState)
                {
                    this.label = nowLabel;
                    this.type = nowType;
                    this.preferredLidState = nowLidState;
                    InitInventory(Block);
                    Inventory.LateInitialize(InventoryClassName + "-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, Api);
                    Inventory.ResolveBlocksOrItems();
                    Inventory.OnAcquireTransitionSpeed = Inventory_OnAcquireTransitionSpeed;
                    MarkDirty();
                }
            }

            base.OnBlockPlaced();
        }



        public bool OnBlockInteractStart(IPlayer byPlayer, BlockSelection blockSel)
        {
            bool put = byPlayer.Entity.Controls.ShiftKey;
            bool take = !put;
            bool bulk = byPlayer.Entity.Controls.CtrlKey;

            ItemSlot ownSlot = inventory.FirstNonEmptySlot;
            var hotbarslot = byPlayer.InventoryManager.ActiveHotbarSlot;

            bool drawIconLabel = put && hotbarslot?.Itemstack?.ItemAttributes?["pigment"]?["color"].Exists == true && blockSel.SelectionBoxIndex == 1;

            if (drawIconLabel)
            {
                if (!inventory.Empty)
                {
                    JsonObject jobj = hotbarslot.Itemstack.ItemAttributes["pigment"]["color"];
                    int r = jobj["red"].AsInt();
                    int g = jobj["green"].AsInt();
                    int b = jobj["blue"].AsInt();

                    labelColor = ColorUtil.ToRgba(255, (int)GameMath.Clamp(r * 1.2f, 0, 255), (int)GameMath.Clamp(g * 1.2f, 0, 255), (int)GameMath.Clamp(b * 1.2f, 0, 255));
                    labelStack = inventory.FirstNonEmptySlot.Itemstack.Clone();
                    labelMesh = null;

                    byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/chalkdraw"), blockSel.Position.X + blockSel.HitPosition.X, blockSel.Position.Y + blockSel.HitPosition.Y, blockSel.Position.Z + blockSel.HitPosition.Z, byPlayer, true, 8);

                    MarkDirty(true);
                }
                else
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "empty", Lang.Get("Can't draw item symbol on an empty crate. Put something inside the crate first"));
                }

                return true;
            }

            if (take && ownSlot != null)
            {
                ItemStack stack = bulk ? ownSlot.TakeOutWhole() : ownSlot.TakeOut(1);
                if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                {
                    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5f + blockSel.Face.Normalf.X, 0.5f + blockSel.Face.Normalf.Y, 0.5f + blockSel.Face.Normalf.Z));
                }
                else
                {
                    didMoveItems(stack, byPlayer);
                }

                if (inventory.Empty)
                {
                    labelMesh = null;
                }

                ownSlot.MarkDirty();
                MarkDirty();
            }

            if (put && !hotbarslot.Empty)
            {
                if (ownSlot == null)
                {
                    if (hotbarslot.TryPutInto(Api.World, inventory[0], bulk ? hotbarslot.StackSize : 1) > 0)
                    {
                        didMoveItems(inventory[0].Itemstack, byPlayer);
                    }
                }
                else
                {
                    if (hotbarslot.Itemstack.Equals(Api.World, ownSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                    {
                        List<ItemSlot> skipSlots = new List<ItemSlot>();
                        while (hotbarslot.StackSize > 0 && skipSlots.Count < inventory.Count)
                        {
                            var wslot = inventory.GetBestSuitedSlot(hotbarslot, skipSlots);
                            if (wslot.slot == null) break;

                            if (hotbarslot.TryPutInto(Api.World, wslot.slot, bulk ? hotbarslot.StackSize : 1) > 0)
                            {
                                didMoveItems(wslot.slot.Itemstack, byPlayer);
                                if (!bulk) break;
                            }

                            skipSlots.Add(wslot.slot);
                        }
                    }
                }

                hotbarslot.MarkDirty();
                MarkDirty();
            }


            return true;
        }

        protected void didMoveItems(ItemStack stack, IPlayer byPlayer)
        {
            if (Api.Side == EnumAppSide.Client) loadOrCreateMesh();

            capi?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            AssetLocation sound = stack?.Block?.Sounds?.Place;
            Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
        }

        protected virtual void InitInventory(Block block)
        {
            if (block?.Attributes != null)
            {
                var props = block.Attributes["properties"][type];
                if (!props.Exists) props = block.Attributes["properties"]["*"];
                quantitySlots = props["quantitySlots"].AsInt(quantitySlots);
                retrieveOnly = props["retrieveOnly"].AsBool(false);
            }

            inventory = new InventoryGeneric(quantitySlots, null, null, null);
            inventory.BaseWeight = 1f;
            inventory.OnGetSuitability = (sourceSlot, targetSlot, isMerge) => (isMerge ? (inventory.BaseWeight + 3) : (inventory.BaseWeight + 1)) + (sourceSlot.Inventory is InventoryBasePlayer ? 1 : 0);
            inventory.OnGetAutoPullFromSlot = GetAutoPullFromSlot;
            inventory.OnGetAutoPushIntoSlot = GetAutoPushIntoSlot;

            if (block?.Attributes != null)
            {
                if (block.Attributes["spoilSpeedMulByFoodCat"][type].Exists == true)
                {
                    inventory.PerishableFactorByFoodCategory = block.Attributes["spoilSpeedMulByFoodCat"][type].AsObject<Dictionary<EnumFoodCategory, float>>();
                }

                if (block.Attributes["transitionSpeedMul"][type].Exists == true)
                {
                    inventory.TransitionableSpeedMulByType = block.Attributes["transitionSpeedMul"][type].AsObject<Dictionary<EnumTransitionType, float>>();
                }
            }

            inventory.PutLocked = retrieveOnly;
            inventory.OnInventoryClosed += OnInvClosed;
            inventory.OnInventoryOpened += OnInvOpened;
        }


        private void Inventory_SlotModified(int obj)
        {
            MarkDirty(false);
        }

        public Cuboidf[] GetSelectionBoxes()
        {
            if (selBoxCrate == null)
            {
                selBoxCrate = ownBlock.SelectionBoxes[0].RotatedCopy(0, ((int)Math.Round(rotAngleY * GameMath.RAD2DEG / 90)) * 90, 0, new Vec3d(0.5, 0, 0.5));
                selBoxLabel = ownBlock.SelectionBoxes[1].RotatedCopy(0, rotAngleY * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0, 0.5));
            }

            if (Api.Side == EnumAppSide.Client)
            {
                var hotbarslot = (Api as ICoreClientAPI).World.Player.InventoryManager.ActiveHotbarSlot;
                if (hotbarslot?.Itemstack?.ItemAttributes?["pigment"]?["color"].Exists == true)
                {
                    return new Cuboidf[] { selBoxCrate, selBoxLabel };
                }
            }

            return new Cuboidf[] { selBoxCrate };
        }

        #region Load/Store

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            var block = worldForResolving.GetBlock(new AssetLocation(tree.GetString("blockCode"))) as BlockCrate;

            type = tree.GetString("type", block?.Props.DefaultType);
            label = tree.GetString("label");
            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);

            labelColor = tree.GetInt("labelColor");
            labelStack = tree.GetItemstack("labelStack");
            preferredLidState = tree.GetString("lidState");

            if (labelStack != null && !labelStack.ResolveBlockOrItem(worldForResolving))
            {
                labelStack = null;
            }

            if (inventory == null)
            {
                if (tree.HasAttribute("blockCode"))
                {
                    InitInventory(block);
                }
                else
                {
                    InitInventory(null);
                }
            }

            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                loadOrCreateMesh();
                MarkDirty(true);
            }

            base.FromTreeAttributes(tree, worldForResolving);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (Block != null) tree.SetString("forBlockCode", Block.Code.ToShortString());

            if (type == null) type = ownBlock.Props.DefaultType; // No idea why. Somewhere something has no type. Probably some worldgen ruins

            tree.SetString("label", label);
            tree.SetString("type", type);
            tree.SetFloat("meshAngle", MeshAngle);
            tree.SetInt("labelColor", labelColor);
            tree.SetString("lidState", preferredLidState);

            tree.SetItemstack("labelStack", labelStack);
        }


        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            base.OnLoadCollectibleMappings(worldForResolve, oldBlockIdMapping, oldItemIdMapping, schematicSeed);

            if (labelStack != null && !labelStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
            {
                labelStack = null;
            }
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);

            labelStack?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(labelStack), blockIdMapping, itemIdMapping);
        }

        #endregion


        private ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            if (atBlockFace == BlockFacing.DOWN)
            {
                return inventory.FirstNonEmptySlot;
            }

            return null;
        }

        private ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            var slot = inventory.FirstNonEmptySlot;
            if (slot == null) return inventory[0];

            if (slot.Itemstack.Equals(Api.World, fromSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                return slot;
            }

            return null;
        }

        protected virtual void OnInvOpened(IPlayer player)
        {
            inventory.PutLocked = retrieveOnly && player.WorldData.CurrentGameMode != EnumGameMode.Creative;

        }

        protected virtual void OnInvClosed(IPlayer player)
        {
            inventory.PutLocked = retrieveOnly;
        }

        #region Meshing

        private void loadOrCreateMesh()
        {
            BlockCrate block = Block as BlockCrate;
            if (Block == null)
            {
                block = Api.World.BlockAccessor.GetBlock(Pos) as BlockCrate;
                Block = block;
            }
            if (block == null) return;

            string cacheKey = "crateMeshes" + block.FirstCodePart();
            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(Api, cacheKey, () => new Dictionary<string, MeshData>());

            MeshData mesh;

            CompositeShape cshape = ownBlock.Props[type].Shape;
            if (cshape?.Base == null)
            {
                return;
            }

            var firstStack = inventory.FirstNonEmptySlot?.Itemstack;

            string meshKey = type + block.Subtype + "-" + label + "-" + LidState + "-" + (LidState == "closed" ? null : firstStack?.StackSize + "-" + firstStack?.GetHashCode());

            if (!meshes.TryGetValue(meshKey, out mesh))
            {
                mesh = block.GenMesh(Api as ICoreClientAPI, firstStack, type, label, LidState, cshape, new Vec3f(cshape.rotateX, cshape.rotateY, cshape.rotateZ));
                meshes[meshKey] = mesh;
            }

            ownMesh = mesh.Clone().Rotate(origin, 0, MeshAngle, 0).Scale(origin, rndScale, rndScale, rndScale);
        }


        void genLabelMesh()
        {
            ItemStack stack = labelStack;

            if (LabelProps?.EditableShape != null && stack != null)
            {
                var capi = Api as ICoreClientAPI;
                int hashCode = stack.GetHashCode(GlobalConstants.IgnoredStackAttributes) + 23*labelColor.GetHashCode();

                if (ownBlock.itemStackRenders.TryGetValue(hashCode, out var val))
                {
                    val.UsedCounter.Add(Pos.GetHashCode());
                    GenLabelMeshWithItemStack(val.TextureSubId);
                    return;
                }

                capi.Render.RenderItemStackToAtlas(stack, capi.BlockTextureAtlas, 52, (texSubid) =>
                {
                    ownBlock.itemStackRenders[hashCode] = new ItemStackRenderCacheItem() { TextureSubId = texSubid, UsedCounter = new HashSet<int>() };
                    ownBlock.itemStackRenders[hashCode].UsedCounter.Add(Pos.GetHashCode());

                    GenLabelMeshWithItemStack(texSubid);
                    capi.BlockTextureAtlas.RegenMipMaps(capi.BlockTextureAtlas.Positions[texSubid].atlasNumber);
                    MarkDirty(true);
                }, ColorUtil.ColorOverlay(labelColor, ColorUtil.WhiteArgb, 0.65f), 0.5f, 1f);
                
            }
        }



        static Vec3f origin = new Vec3f(0.5f, 0f, 0.5f);
        float rndScale => 1 + (GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 100) - 50) / 1000f;

        void GenLabelMeshWithItemStack(int textureSubId)
        {
            var texPos = capi.BlockTextureAtlas.Positions[textureSubId];
            labelMesh = ownBlock.GenLabelMesh(capi, label, texPos, true, null);
            labelMesh.Rotate(origin, 0, rotAngleY + GameMath.PI, 0).Scale(origin, rndScale, rndScale, rndScale);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            bool skipmesh = base.OnTesselation(mesher, tesselator);
            if (skipmesh) return true;

            
            if (ownMesh == null)
            {
                return true;
            }

            if (labelMesh == null)
            {
                genLabelMesh();
            }

            mesher.AddMeshData(ownMesh);
            mesher.AddMeshData(labelMesh);

            return true;
        }

        #endregion


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            int stacksize = 0;
            foreach (var slot in inventory) stacksize += slot.StackSize;

            
            if (stacksize > 0) {
                dsc.AppendLine(Lang.Get("Contents: {0}x{1}", stacksize, inventory.FirstNonEmptySlot.GetStackName()));
            } else
            {
                dsc.AppendLine(Lang.Get("Empty"));
            }

            base.GetBlockInfo(forPlayer, dsc);
        }


        public override void OnBlockUnloaded()
        {
            FreeAtlasSpace();
            base.OnBlockUnloaded();
            
        }

        public override void OnBlockRemoved()
        {
            FreeAtlasSpace();
            base.OnBlockRemoved();
        }

        private void FreeAtlasSpace()
        {
            if (Api?.Side != EnumAppSide.Client) return;

            if (labelStack != null)
            {
                int hashCode = labelStack.GetHashCode(GlobalConstants.IgnoredStackAttributes) + labelColor.GetHashCode();
                if (ownBlock !=null && ownBlock.itemStackRenders.TryGetValue(hashCode, out var val))
                {
                    val.UsedCounter.Remove(Pos.GetHashCode());
                    if (val.UsedCounter.Count == 0)
                    {
                        capi.BlockTextureAtlas.FreeTextureSpace(val.TextureSubId);
                    }
                }
            }
        }
    }
}
