using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityCrate : BlockEntityContainer, IRotatable
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

        Cuboidf selBoxCrate;
        Cuboidf selBoxLabel;

        int labelColor;

        ItemStack labelStack;
        ModSystemLabelMeshCache labelCacheSys;

        public bool Labelled => label != null && label != "";

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
                if (preferredLidState == "closed") return preferredLidState;   // Early exit in this common situation - no point in testing contents if it's closed anyhow

                if (inventory.Empty) return preferredLidState;
                var stack = inventory.FirstNonEmptySlot.Itemstack;
                if (stack?.Collectible == null || (stack.ItemAttributes != null && stack.ItemAttributes["inContainerTexture"].Exists)) return preferredLidState;

                bool? displayInsideCrate = stack.ItemAttributes?["displayInsideCrate"].Exists != true ? null : stack.ItemAttributes?["displayInsideCrate"].AsBool(true);
                bool hasContentTexture = (stack.Block != null && stack.Block.DrawType == EnumDrawType.Cube && displayInsideCrate != false) || displayInsideCrate == true;

                return hasContentTexture ? preferredLidState : "closed";    // Always show as closed if there is no content texture
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            ownBlock = (BlockCrate)Block;

            bool isNewlyplaced = inventory == null;
            if (isNewlyplaced)
            {
                InitInventory(Block, api);
            }

            base.Initialize(api);

            if (api.Side == EnumAppSide.Client)
            {
                rndScale = 1 + (GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 100) - 50) / 1000f;

                if (!isNewlyplaced)
                {
                    labelCacheSys = api.ModLoader.GetModSystem<ModSystemLabelMeshCache>();
                    ownMesh = null;
                    PreLoadContentsTexture(api as ICoreClientAPI);
                }
            }
        }

        /// <summary>
        /// On the client-side, if it's an opened crate with contents, then we want to ensure the content texture is loaded to the BlockTextureAtlas
        /// </summary>
        /// <param name="capi"></param>
        protected virtual void PreLoadContentsTexture(ICoreClientAPI capi)
        {
            var contentStack = inventory.FirstNonEmptySlot?.Itemstack;
            if (contentStack != null && LidState != "closed")
            {
                var contentSource = BlockBarrel.getContentTexture(capi, contentStack, out float fillHeight);
                if (contentSource != null)
                {
                    var _ = contentSource["null"];    // GetOrInsert the content texture to the BlockTextureAtlas. Low cost if the texture is already there. If it needs to be inserted, that cannot be done during OnTesselation as that is off-thread
                                                      // The texture code is irrelevant here, it is not used by ContainerTextureSource.cs, but in fact in shape crate/contents.json it is named "null"!!
                                                      // This basically does nothing if the content texture is already present in the BlockTextureAtlas
                }
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
                    InitInventory(Block, Api);   // We need to replace the inventory with one for the new type (may be a different size). It's OK to delete the existing inventory in a newly placed block, it can't hold anything
                    Inventory.LateInitialize(InventoryClassName + "-" + Pos, Api);
                    Inventory.ResolveBlocksOrItems();
                    container.LateInit();
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

            ItemSlot hotbarslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (hotbarslot == null) throw new Exception("Interact called when byPlayer has null ActiveHotbarSlot");

            bool drawIconLabel = bulk && Labelled && hotbarslot.Itemstack?.ItemAttributes?["pigment"]?["color"].Exists == true && blockSel.SelectionBoxIndex == 1;

            if (drawIconLabel)
            {
                if (inventory.Empty && labelStack != null)
                {
                    FreeAtlasSpace();
                    labelStack = null;
                    labelMesh = null;
                    MarkDirty(true);
                    return true;
                }
                else if (!Inventory.Empty)
                {
                    JsonObject jobj = hotbarslot.Itemstack.ItemAttributes["pigment"]["color"];
                    int r = jobj["red"].AsInt();
                    int g = jobj["green"].AsInt();
                    int b = jobj["blue"].AsInt();
                    int newLabelColor = ColorUtil.ToRgba(255, (int)GameMath.Clamp(r * 1.2f, 0, 255), (int)GameMath.Clamp(g * 1.2f, 0, 255), (int)GameMath.Clamp(b * 1.2f, 0, 255));

                    if (labelStack == null || labelColor != newLabelColor)
                    {
                        // Remove previous label from atlas
                        FreeAtlasSpace();

                        labelColor = newLabelColor;
                        labelStack = inventory.FirstNonEmptySlot.Itemstack.Clone();
                        labelStack.Attributes.RemoveAttribute("temperature");
                        labelStack.Attributes.RemoveAttribute("transitionstate");
                        // In theory this might be a good use case for setting timeFrozen = true, but in practice that's not in the list of global ignored itemstack attributes, 
                        // so would cause crates to lose their label later by failing the equality check
                        labelMesh = null;

                        byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/chalkdraw"), blockSel.Position.X + blockSel.HitPosition.X, blockSel.Position.InternalY + blockSel.HitPosition.Y, blockSel.Position.Z + blockSel.HitPosition.Z, byPlayer, true, 8);

                        MarkDirty(true);
                        return true;
                    }
                }
                else if (take)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "empty", Lang.Get("Can't draw item symbol on an empty crate. Put something inside the crate first"));
                }
            }

            if (take)
            {
                int i = 0;
                for (; i < inventory.Count; ++i) if (!inventory[i].Empty) break;
                if (i >= inventory.Count) return true; // Can't take. Crate is empty.

                ItemSlot ownSlot = inventory[i];
                int requestedQuantity = bulk ? ownSlot.Itemstack.Collectible.MaxStackSize : 1;
                for (; i < inventory.Count && ownSlot.StackSize < requestedQuantity; ++i)
                {
                    inventory[i].TryPutInto(Api.World, ownSlot, requestedQuantity - ownSlot.StackSize);
                }
                ItemStack stack = ownSlot.TakeOut(requestedQuantity);

                int originalQuantity = stack.StackSize;
                bool gave = byPlayer.InventoryManager.TryGiveItemstack(stack, true);
                int taken = originalQuantity - stack.StackSize;
                if (gave)
                {
                    if (taken == 0) taken = originalQuantity;
                    if (originalQuantity > taken)
                    {
                        new DummySlot(stack).TryPutInto(Api.World, ownSlot, originalQuantity - taken);
                    }
                    didMoveItems(stack, byPlayer);
                }
                else
                {
                    new DummySlot(stack).TryPutInto(Api.World, ownSlot, originalQuantity - taken);
                }

                if (taken == 0)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "invfull", Lang.Get("item-take-error-invfull"));
                }
                else
                {
                    Api.Logger.Audit("{0} Took {1}x{2} from " + Block?.Code + " at {3}.",
                        byPlayer.PlayerName,
                        taken,
                        stack?.Collectible.Code,
                        Pos
                    );

                    ownSlot.MarkDirty();
                    MarkDirty();
                }
                return true;
            }

            if (put && !hotbarslot.Empty)
            {
                ItemSlot ownSlot = inventory.FirstNonEmptySlot;
                var quantity = bulk ? hotbarslot.StackSize : 1;
                if (ownSlot == null)
                {
                    if (!hotbarslot.Itemstack.Equals(Api.World, labelStack, GlobalConstants.IgnoredStackAttributes))
                    {
                        FreeAtlasSpace();
                        labelStack = null;
                        labelMesh = null;
                    }
                    if (hotbarslot.TryPutInto(Api.World, inventory[0], quantity) > 0)
                    {
                        didMoveItems(inventory[0].Itemstack, byPlayer);
                        Api.World.Logger.Audit("{0} Put {1}x{2} into Crate at {3}.",
                            byPlayer.PlayerName,
                            quantity,
                            inventory[0].Itemstack?.Collectible.Code,
                            Pos
                        );
                    }
                }
                else
                {
                    if (hotbarslot.Itemstack.Equals(Api.World, ownSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                    {
                        List<ItemSlot> skipSlots = new List<ItemSlot>();
                        while (hotbarslot.StackSize > 0 && skipSlots.Count < inventory.Count)
                        {
                            var wslot = inventory.GetBestSuitedSlot(hotbarslot, null, skipSlots);
                            if (wslot.slot == null) break;

                            if (hotbarslot.TryPutInto(Api.World, wslot.slot, quantity) > 0)
                            {
                                didMoveItems(wslot.slot.Itemstack, byPlayer);
                                Api.World.Logger.Audit("{0} Put {1}x{2} into Crate at {3}.",
                                    byPlayer.PlayerName,
                                    quantity,
                                    wslot.slot.Itemstack?.Collectible.Code,
                                    Pos
                                );
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
            if (Api.Side == EnumAppSide.Client)
            {
                ownMesh = null;  // Trigger regeneration of mesh
                PreLoadContentsTexture(Api as ICoreClientAPI);
            }

            (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            AssetLocation sound = stack?.Block?.Sounds?.Place;
            Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
        }

        protected virtual void InitInventory(Block block, ICoreAPI api)
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

            if (api.Side == EnumAppSide.Server)
            {
                inventory.SlotModified += Inventory_SlotModified;
            }

            container.Reset();
        }


        private void Inventory_SlotModified(int obj)
        {
            MarkDirty(false);
        }

        public Cuboidf[] GetSelectionBoxes()
        {
            if (selBoxCrate == null)
            {
                selBoxCrate = Block.SelectionBoxes[0].RotatedCopy(0, ((int)Math.Round(rotAngleY * GameMath.RAD2DEG / 90)) * 90, 0, new Vec3d(0.5, 0, 0.5));
                selBoxLabel = Block.SelectionBoxes[1].RotatedCopy(0, rotAngleY * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0, 0.5));
            }

            if (Api.Side == EnumAppSide.Client)
            {
                ItemSlot hotbarslot = ((ICoreClientAPI)Api).World.Player.InventoryManager.ActiveHotbarSlot;
                if (Labelled && hotbarslot.Itemstack?.ItemAttributes?["pigment"]?["color"].Exists == true)
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
                    InitInventory(block, worldForResolving.Api);
                }
                else
                {
                    InitInventory(null, worldForResolving.Api);
                }
            }

            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                ownMesh = null;  // Trigger regeneration of mesh
                PreLoadContentsTexture(Api as ICoreClientAPI);
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


        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForResolve, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);

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
            var slotNonEmpty = inventory.FirstNonEmptySlot;
            if (slotNonEmpty == null) return inventory[0];

            if (slotNonEmpty.Itemstack.Equals(Api.World, fromSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                foreach (var slot in inventory)
                {
                    if (slot.Itemstack == null || slot.StackSize < slot.Itemstack.Collectible.MaxStackSize)
                        return slot;
                }
                return null;
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
            Block ??= Api.World.BlockAccessor.GetBlock(Pos) as BlockCrate;
            BlockCrate block = Block as BlockCrate;
            if (block == null) return;

            string cacheKey = "crateMeshes" + block.FirstCodePart();
            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(Api, cacheKey, () => new Dictionary<string, MeshData>());


            CompositeShape cshape = ownBlock.Props[type].Shape;
            if (cshape?.Base == null)
            {
                return;
            }

            var firstStack = inventory.FirstNonEmptySlot?.Itemstack;

            string meshKey = type + block.Subtype + "-" + label + "-" + LidState + "-" + (LidState == "closed" ? null : firstStack?.StackSize + "-" + firstStack?.GetHashCode());

            if (!meshes.TryGetValue(meshKey, out MeshData mesh))
            {
                mesh = block.GenMesh(Api as ICoreClientAPI, firstStack, type, label, LidState, cshape, new Vec3f(cshape.rotateX, cshape.rotateY, cshape.rotateZ));
                meshes[meshKey] = mesh;
            }

            ownMesh = mesh.Clone().Rotate(origin, 0, MeshAngle, 0).Scale(origin, rndScale, rndScale, rndScale);
        }

        bool requested = false;
        void genLabelMesh()
        {
            if (LabelProps?.EditableShape == null || labelStack == null || requested) return;

            if (labelCacheSys == null) labelCacheSys = Api.ModLoader.GetModSystem<ModSystemLabelMeshCache>();

            requested = true;
            labelCacheSys.RequestLabelTexture(labelColor, Pos, labelStack, (texSubId) =>
            {
                GenLabelMeshWithItemStack(texSubId);
                ((ICoreClientAPI)Api).Event.EnqueueMainThreadTask(() => MarkDirty(true), "markcratedirty");
                requested = false;
            });
        }


        static Vec3f origin = new Vec3f(0.5f, 0f, 0.5f);
        float rndScale = 1;

        void GenLabelMeshWithItemStack(int textureSubId)
        {
            ICoreClientAPI capi = (ICoreClientAPI)Api;
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
                loadOrCreateMesh();
                if (ownMesh == null) return true;
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
            if (labelStack != null)
            {
                labelCacheSys?.FreeLabelTexture(labelStack, labelColor, Pos);
            }
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation,
            Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            ownMesh = null;
            MeshAngle = tree.GetFloat("meshAngle");
            MeshAngle -= degreeRotation * GameMath.DEG2RAD;
            tree.SetFloat("meshAngle", MeshAngle);
        }
    }
}
