using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

#nullable disable

public class ItemSlotDisplay : ItemSlotSurvival
{
    public string DisplayCategory;
    public ItemSlotDisplay(InventoryBase inventory, string displayCategory) : base(inventory)
    {
        DisplayCategory = displayCategory;
    }
}

public class BEBehaviorDisplay : BEBehaviorContainer, ILongInteractable, IInteractableWithHelp, IRotatablePlaceable, ITexPositionSource
{
    protected MeshData blockMesh;
    protected ICoreClientAPI capi;
    protected ITexPositionSource texSource;
    protected CollectibleObject nowTesselatingObj;
    protected Shape nowTesselatingShape;
    protected override string InventoryClassName => "cabinet";
    protected Cuboidf[] selectionBoxes;
    protected Cuboidf[] collisionboxes;
    protected Cuboidf[] shelfSelectionBoxes;
    protected InventoryInfinite inventory;
    ItemSlot isUsingSlot;

    protected ItemStack displayStack;
    public ItemStack DisplayStack => displayStack;

    public InventoryInfinite InventoryInv => inventory;

    protected bool meshesGenerated = false;
    protected Dictionary<string, MeshData> MeshCache => ObjectCacheUtil.GetOrCreate(Api, "meshesDisplay-" + ClassCode, () => new Dictionary<string, MeshData>());

    public Dictionary<string, float[]> TfMatrices = new Dictionary<string, float[]>();
    public Dictionary<string, float> customRotationDegBySlot = null;
    public float MeshAngleRad { get; set; }


    /// <summary>
    /// Return a unique code for this type of block. Used as part of the cache key. E.g. for the display case the class code is "displaycase", for the shelf its "openshelf"
    /// </summary>
    public virtual string ClassCode => InventoryClassName;
    public virtual string AttributeTransformCode => "onCabinetTransform";
    public override InventoryBase Inventory => inventory;
    public virtual TextureAtlasPosition this[string textureCode]
    {
        get
        {
            if (texSource != null) return texSource[textureCode];

            IDictionary<string, CompositeTexture> textures = nowTesselatingObj is Item item ? item.Textures : (nowTesselatingObj as Block).Textures;
            AssetLocation texturePath = null;

            // Prio 1: Get from collectible textures
            if (textures.TryGetValue(textureCode, out CompositeTexture tex))
            {
                texturePath = tex.Baked.BakedName;
            }

            // Prio 2: Get from collectible textures, use "all" code
            if (texturePath == null && textures.TryGetValue("all", out tex))
            {
                texturePath = tex.Baked.BakedName;
            }

            // Prio 3: Get from currently tesselating shape
            if (texturePath == null)
            {
                nowTesselatingShape?.Textures.TryGetValue(textureCode, out texturePath);
            }

            // Prio 4: The code is the path
            if (texturePath == null)
            {
                texturePath = new AssetLocation(textureCode);
            }

            return getOrCreateTexPos(texturePath);
        }
    }
    public Size2i AtlasSize => capi!.BlockTextureAtlas.Size;


    public BEBehaviorDisplay(BlockEntity blockentity) : base(blockentity)
    {
        var bhd = Block.GetBehavior<BlockBehaviorDisplay>();

        inventory = new InventoryInfinite((slotid, inv) =>
        {
            var psurface = bhd.PlacementSurfaces[slotid.Split('-')[0].ToInt()];
            return new ItemSlotDisplay(inv, psurface.DisplayCategory);
        });
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        capi = api as ICoreClientAPI;
        // Must be added after Initialize(), so we can override the transition speed value
        Inventory.OnAcquireTransitionSpeed += Inv_OnAcquireTransitionSpeed;
        capi?.Event.RegisterEventBusListener(OnSetTransform, 0.5, "onsettransform");
    }

    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        displayStack = byItemStack?.Clone();
        displayStack?.StackSize = 1;
    }


    protected void OnSetTransform(string eventName, ref EnumHandling handling, IAttribute data)
    {
        var target = (data as TreeAttribute).GetString("target");

        if (target != AttributeTransformCode && target != "onshelfTransform" /* pre 1.22 syntax */) return;

        if (Pos.DistanceTo(capi.World.Player.Entity.Pos.XYZ) > 20) return; // Just for nearby shelves to reduce chunk redraws

        var hslot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
        var collObj = hslot.Itemstack.Collectible;
        var heldDAttr = collObj?.Attributes?["displayable"]["shelf"].AsObject<DisplayableAttributes>(null);
        if (heldDAttr != null)
        {
            if (collObj.Attributes == null) collObj.Attributes = new JsonObject(new JObject());
            collObj.Attributes.Token["displayable"]["shelf"]["transform"] = JToken.FromObject(ModelTransform.CreateFromTreeAttribute(data as TreeAttribute));
        }

        MeshCache.Clear();
        MarkMeshesDirty(Api.World);
        Api.World.BlockAccessor.MarkBlockDirty(Pos);   // always redraw on client after updating meshes
    }


    protected float Inv_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
    {
        if (transType is EnumTransitionType.Dry or EnumTransitionType.Melt)
        {
            // Since we can now have multiple OnAcquireTransitionSpeed invocations stacked we have to multiply this to offset the base 0.25f
            return (container.Room?.ExitCount == 0 ? 2f : 0.5f) * 4f;
        }
        if (Api == null) return 0;

        if (transType is not EnumTransitionType.Ripen) return 1;

        return GameMath.Clamp((1 - container.GetPerishRate() - 0.5f) * 3, 0, 1);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);

        BlockSelection blockSel = forPlayer.CurrentBlockSelection;
        if (blockSel == null || !blockSel.Position.Equals(Pos)) return;

        var slotLoc = BlockBehaviorDisplay.decodeSlotid(blockSel.SelectionBoxId);
        if (slotLoc?.IsPlacedItem != true) return;

        ItemSlot slot = inventory[slotLoc.EncodedLocation];
        if (slot == null || slot.Empty) return;

        ItemStack stack = slot.Itemstack;
        TransitionState[] transitionStates = stack.Collectible.UpdateAndGetTransitionStates(Api.World, slot);
        if (transitionStates != null)
        {
            foreach (TransitionState state in transitionStates)
            {
                if (state.Props.Type != EnumTransitionType.Perish) continue;

                float perishRate = stack.Collectible.GetTransitionRateMul(Api.World, slot, state.Props.Type);
                if (perishRate <= 0) continue;

                float transitionLevel = state.TransitionLevel;
                float freshHoursLeft = state.FreshHoursLeft / perishRate;

                sb.Append(stack.GetName());

                if (transitionLevel > 0)
                {
                    sb.Append(", " + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                }
                else
                {
                    double hoursPerday = Api.World.Calendar.HoursPerDay;

                    if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                    {
                        sb.Append(", " + Lang.Get("fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                    }
                    else if (freshHoursLeft > hoursPerday)
                    {
                        sb.Append(", " + Lang.Get("fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                    }
                    else
                    {
                        sb.Append(", " + Lang.Get("fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                    }
                }

                sb.AppendLine();
                return;
            }
        }

        sb.AppendLine(stack.Collectible.GetCollectibleInterface<IContainedCustomName>()?.GetContainedInfo(slot) ?? stack.GetName());
    }

    public bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        if (Api.Side == EnumAppSide.Client && BlockBehaviorDisplay.IsUsingContainedBlock)
        {
            handling = EnumHandling.PreventDefault;
            return true;
        }

        if (blockSel.SelectionBoxId == null) return false;
        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (slot == null) return false;

        if (TryUse(byPlayer, blockSel, ref handling)) return true;
        if (slot.Empty) return TryTake(byPlayer, blockSel, ref handling);
        string selectedSlotId = getSelectedNonEmptySlotId(blockSel);
        if (selectedSlotId != null && byPlayer.InventoryManager.ActiveTool == EnumTool.Wrench)
        {
            return TryRotate(selectedSlotId, byPlayer, blockSel, ref handling);
        }

        return TryPut(byPlayer, blockSel, ref handling);
    }

    public bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventSubsequent;
        var collIci = isUsingSlot?.Itemstack?.Collectible.GetCollectibleInterface<IContainedInteractable>();
        if (collIci != null)
        {
            return collIci.OnContainedInteractStep(secondsUsed, Blockentity, isUsingSlot, byPlayer, blockSel);
        }

        isUsingSlot = null;
        return false;
    }

    public void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventSubsequent;
        var collIci = isUsingSlot?.Itemstack?.Collectible.GetCollectibleInterface<IContainedInteractable>();
        collIci?.OnContainedInteractStop(secondsUsed, Blockentity, isUsingSlot, byPlayer, blockSel);

        isUsingSlot = null;
    }

    public bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventSubsequent;
        var collIci = isUsingSlot?.Itemstack?.Collectible.GetCollectibleInterface<IContainedInteractable>();

        isUsingSlot = null;
        return collIci?.OnContainedInteractCancel(secondsUsed, Blockentity, isUsingSlot, byPlayer, blockSel, EnumItemUseCancelReason.ReleasedMouse) ?? true;
    }

    public WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
    {
        string selectedSlotId = getSelectedNonEmptySlotId(selection);
        if (selectedSlotId == null) return [];

        var slot = inventory[selectedSlotId];
        return slot.Itemstack?.Collectible.GetCollectibleInterface<IContainedInteractable>()?.GetContainedInteractionHelp(Blockentity, slot, forPlayer, selection) ?? [];
    }


    private bool TryUse(IPlayer player, BlockSelection blockSel, ref EnumHandling handling)
    {
        string selectedSlotId = getSelectedNonEmptySlotId(blockSel);
        if (selectedSlotId == null) return false;

        var slot = inventory[selectedSlotId];

        if (slot != null && !slot.Empty)
        {
            var collIci = slot.Itemstack?.Collectible.GetCollectibleInterface<IContainedInteractable>();
            if (collIci != null && collIci.OnContainedInteractStart(Blockentity, slot, player, blockSel))
            {
                handling = EnumHandling.PreventSubsequent;
                BlockBehaviorDisplay.IsUsingContainedBlock = true;
                isUsingSlot = slot;
                Blockentity.MarkDirty();
                return true;
            }
        }

        return false;
    }


    protected bool TryTake(IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        string selectedSlotId = getSelectedNonEmptySlotId(blockSel);
        if (selectedSlotId == null) return false;

        var slot = inventory[selectedSlotId];
        if (slot != null && !slot.Empty)
        {
            handling = EnumHandling.PreventDefault;

            ItemStack stack = slot.TakeOut(1);
            if (byPlayer.InventoryManager.TryGiveItemstack(stack))
            {
                SoundAttributes? sound = stack?.Block?.Sounds?.Place;
                Api.World.PlaySoundAt(sound ?? GlobalConstants.DefaultBuildSound, byPlayer.Entity, byPlayer);
            }

            if (stack?.StackSize > 0)
            {
                Api.World.SpawnItemEntity(stack, Pos);
            }
            Api.World.Logger.Audit("{0} Took 1x{1} from Shelf at {2}.",
                byPlayer.PlayerName,
                stack?.Collectible.Code,
                Pos
            );

            (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            MarkMeshesDirty(Api.World);
            Blockentity.MarkDirty(true);


            var slotLoc = BlockBehaviorDisplay.decodeSlotid(selectedSlotId);
            var aboveSlotLoc = slotLoc.UpCopy();
            while (inventory[aboveSlotLoc.EncodedLocation]?.Empty == false)
            {
                inventory[slotLoc.EncodedLocation].Itemstack = inventory[aboveSlotLoc.EncodedLocation].Itemstack;
                inventory[aboveSlotLoc.EncodedLocation].Itemstack = null;
                slotLoc.Y++;
                aboveSlotLoc.Y++;
            }

            return true;
        }

        return false;
    }




    protected bool TryPut(IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        var bhd = Block.GetBehavior<BlockBehaviorDisplay>();
        var heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        var targetSlotId = blockSel.SelectionBoxId;

        var slotLoc = BlockBehaviorDisplay.decodeSlotid(targetSlotId);
        if (slotLoc == null) return false;
        var psurface = bhd.PlacementSurfaces[slotLoc.PlacementSurfaceIndex];

        DisplayableAttributes heldDAttr = BlockBehaviorDisplay.GetDisplayableAttributes(heldSlot, psurface);

        handling = EnumHandling.PreventSubsequent;

        // 1. Check if selected slot is compatible
        if (heldDAttr == null) return false;

        // 2. Check if slot is large enough
        var availSize = psurface.Size;
        var requiredSize = heldDAttr.Size;

        if (availSize.Width < requiredSize.Width || availSize.Height < requiredSize.Height || availSize.Length < requiredSize.Length)
        {
            (Api as ICoreClientAPI)?.TriggerIngameError(this, "toolarge", Lang.Get("shelfhelp-toolarge-error"));
            return true;
        }

        var offset = BlockBehaviorDisplay.getOffsetFromPreviewCuboid(requiredSize, MeshAngleRad);

        // 2.5 Check not out of bounds
        if (slotLoc.X + offset.X < 0 || slotLoc.Z + offset.Z < 0 || slotLoc.X + offset.X > availSize.Width - requiredSize.Width || slotLoc.Z + offset.Z > availSize.Length - requiredSize.Length)
        {
            (Api as ICoreClientAPI)?.TriggerIngameError(this, "outofbounds", Lang.Get("shelfhelp-outofbounds-error"));
            return true;
        }


        // 3. Check not already occupied
        string selectedSlotId = getCollidingSlotId(blockSel, new Cuboidf(requiredSize));
        if (selectedSlotId != null)
        {
            if (heldDAttr.Behavior == EnumDisplayableBehavior.Pileable && heldSlot.Itemstack.Equals(Api.World, inventory[selectedSlotId].Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                return TryPile(byPlayer, blockSel, selectedSlotId, heldDAttr);
            }

            if (heldDAttr.Behavior == EnumDisplayableBehavior.Stacking)
            {
                return TryStack(byPlayer, blockSel, selectedSlotId, heldDAttr);
            }

            (Api as ICoreClientAPI)?.TriggerIngameError(this, "shelffull", Lang.Get("shelfhelp-shelffull-error"));
            return true;
        }



        // 4. Ok! Can put.
        return placeItem(byPlayer, heldSlot, targetSlotId);
    }

    private bool TryPile(IPlayer byPlayer, BlockSelection blockSel, string collidingSlotId, DisplayableAttributes heldDAttr)
    {
        var heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (heldSlot.Itemstack.Equals(Api.World, inventory[collidingSlotId].Itemstack, GlobalConstants.IgnoredStackAttributes)) return false;
        if (heldDAttr.PileableSelectiveElements.Length < inventory[collidingSlotId].StackSize) return false;

        inventory[collidingSlotId].Itemstack.StackSize++;
        heldSlot.TakeOut(1);
        MarkMeshesDirty(Api.World);
        Blockentity.MarkDirty(true);
        (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
        Api.World.PlaySoundAt(inventory[collidingSlotId].Itemstack?.Block?.Sounds?.Place ?? GlobalConstants.DefaultBuildSound, byPlayer.Entity, byPlayer);
        Api.World.Logger.Audit("{0} Put 1x{1} into Shelf at {2}, slot {3}.",
            byPlayer.PlayerName,
            inventory[collidingSlotId].Itemstack?.Collectible.Code,
            Pos,
            collidingSlotId
        );
        return true;
    }


    private bool TryStack(IPlayer byPlayer, BlockSelection blockSel, string collidingSlotId, DisplayableAttributes heldDAttr)
    {
        var slotLoc = BlockBehaviorDisplay.decodeSlotid(collidingSlotId);
        var psurface = Block.GetBehavior<BlockBehaviorDisplay>().PlacementSurfaces[slotLoc.PlacementSurfaceIndex];
        if (heldDAttr.Category != BlockBehaviorDisplay.GetDisplayableAttributes(inventory[slotLoc.EncodedLocation], psurface).Category) return false;

        SlotLocation topSlotLoc = slotLoc;
        while (inventory[topSlotLoc.EncodedLocation]?.Empty == false) topSlotLoc = topSlotLoc.UpCopy();

        float usedHeight = topSlotLoc.Y * heldDAttr.Size.Height;
        float availHeight = psurface.Size.Height;

        if (usedHeight >= availHeight) return false;

        placeItem(byPlayer, byPlayer.InventoryManager.ActiveHotbarSlot, topSlotLoc.EncodedLocation);
        return true;
    }



    private bool placeItem(IPlayer byPlayer, ItemSlot heldSlot, string targetSlotId)
    {
        inventory.Allocate(targetSlotId);
        var targetSlot = inventory[targetSlotId];
        int moved = heldSlot.TryPutInto(Api.World, targetSlot);
        MarkMeshesDirty(Api.World);
        Blockentity.MarkDirty(true);
        (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

        if (moved > 0)
        {
            Api.World.PlaySoundAt(targetSlot.Itemstack?.Block?.Sounds?.Place ?? GlobalConstants.DefaultBuildSound, byPlayer.Entity, byPlayer);
            Api.World.Logger.Audit("{0} Put 1x{1} into Shelf at {2}, slot {3}.",
                byPlayer.PlayerName,
                targetSlot.Itemstack?.Collectible.Code,
                Pos,
                targetSlotId
            );
            return true;
        }

        return false;
    }


    private bool TryRotate(string slotid, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;
        var ctrl = byPlayer.WorldData.EntityControls.CtrlKey;

        if (customRotationDegBySlot == null) customRotationDegBySlot = new Dictionary<string, float>();

        if (!customRotationDegBySlot.ContainsKey(slotid))
        {
            customRotationDegBySlot[slotid] = 0f;
        }
        else
        {
            customRotationDegBySlot[slotid] += ctrl ? -22.5f : 22.5f;
        }

        Blockentity.MarkDirty(true);
        MarkMeshesDirty(Api.World);
        return true;
    }

    protected List<KeyValuePair<Cuboidf, string>> getContentCuboidsToSlotMapping(int placeSurfaceIndex)
    {
        var mapping = new List<KeyValuePair<Cuboidf, string>>();
        foreach (var (slotid, slot) in inventory.SlotsByslotId)
        {
            if (slot.Empty) continue;

            var slotLoc = BlockBehaviorDisplay.decodeSlotid(slotid);
            if (slotLoc.PlacementSurfaceIndex != placeSurfaceIndex) continue;

            var psurface = Block.GetBehavior<BlockBehaviorDisplay>().PlacementSurfaces[slotLoc.PlacementSurfaceIndex];
            var size = BlockBehaviorDisplay.getItemSize(slot, psurface);
            if (size == null) continue;

            var box = new Cuboidf(
                slotLoc.X - size.Width / 2,
                slotLoc.Y,
                slotLoc.Z - size.Length / 2,
                slotLoc.X + size.Width / 2,
                slotLoc.Y + 1,
                slotLoc.Z + size.Length / 2
            );
            mapping.Add(new KeyValuePair<Cuboidf, string>(box, slotid));
        }

        return mapping;
    }

    public string getSelectedNonEmptySlotId(BlockSelection blockSel)
    {
        var slotLoc = BlockBehaviorDisplay.decodeSlotid(blockSel.SelectionBoxId);
        if (slotLoc == null) return null;
        var mapping = getContentCuboidsToSlotMapping(slotLoc.PlacementSurfaceIndex);
        string selectedSlotId = null;

        foreach (var (box, slotid) in mapping)
        {
            if (box.Contains(slotLoc.X, slotLoc.Y + 0.1f, slotLoc.Z))
            {
                selectedSlotId = slotid;
                break;
            }
        }

        return selectedSlotId;
    }


    public string getCollidingSlotId(BlockSelection blockSel, Cuboidf placeBox)
    {
        var slotLoc = BlockBehaviorDisplay.decodeSlotid(blockSel.SelectionBoxId);
        var mapping = getContentCuboidsToSlotMapping(slotLoc.PlacementSurfaceIndex);
        string selectedSlotId = null;

        foreach (var (box, slotid) in mapping)
        {
            if (box.Intersects(placeBox, slotLoc.X, slotLoc.Y, slotLoc.Z))
            {
                selectedSlotId = slotid;
                break;
            }
        }

        return selectedSlotId;
    }


    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        MeshAngleRad = tree.GetFloat("meshAngleRad");

        if (tree.HasAttribute("rotation"))
        {
            customRotationDegBySlot = new Dictionary<string, float>();
            var rotTree = tree.GetTreeAttribute("rotation");
            foreach (var val in rotTree)
            {
                customRotationDegBySlot[val.Key] = (val.Value as FloatAttribute).value;
            }
        }

        displayStack = tree.GetItemstack("displayStack");
        displayStack?.ResolveBlockOrItem(worldAccessForResolve);

        MarkMeshesDirty(worldAccessForResolve);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        container.ToTreeAttributes(tree);
        tree.SetFloat("meshAngleRad", MeshAngleRad);

        tree.SetItemstack("displayStack", displayStack);

        if (customRotationDegBySlot != null)
        {
            TreeAttribute rotTree = new TreeAttribute();
            foreach (var val in customRotationDegBySlot)
            {
                rotTree[val.Key] = new FloatAttribute(val.Value);
            }

            tree["rotation"] = rotTree;
        }
    }

    public override void OnLoadCollectibleMappings(IWorldAccessor world, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
    {
        base.OnLoadCollectibleMappings(world, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
        if (displayStack?.FixMapping(oldBlockIdMapping, oldItemIdMapping, world) == false)
        {
            displayStack = null;
        }
        else
        {
            displayStack?.Collectible.OnLoadCollectibleMappings(world, new DummySlot(displayStack), oldBlockIdMapping, oldItemIdMapping, resolveImports);
        }
    }

    public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
    {
        base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
        displayStack?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(displayStack), blockIdMapping, itemIdMapping);
    }


    protected void generateMeshes()
    {
        if (capi == null) return;

        var bh = Block.GetBehavior<BlockBehaviorDisplay>();
        var shape = capi.TesselatorManager.GetCachedShape(Block.Shape.Base);
        var psurfaces = Block.GetBehavior<BlockBehaviorDisplay>().PlacementSurfaces;

        var titem = displayStack?.Collectible.GetCollectibleBehavior<CollectibleBehaviorTypedTexture>(true);
        if (titem != null)
        {
            texSource = new TypedTextureSource(capi, capi.BlockTextureAtlas, displayStack!.Collectible.Code.Domain, titem.TextureMapping, titem.GetMaterials(displayStack));
        } else
        {
            texSource = capi.Tesselator.GetTextureSource(Block);
        }

        capi.Tesselator.TesselateShape(new TesselationMetaData()
        {
            TexSource = texSource,
            IgnoreElements = bh.PlacementSurfaces.Select(dp => dp.ElementName).ToArray().Append(Block.Shape.IgnoreElements)
        }, shape, out blockMesh);

        texSource = null;

        foreach (var (slotid, slot) in inventory.SlotsByslotId.ToList())
        {
            var slotLoc = BlockBehaviorDisplay.decodeSlotid(slotid);

            var stack = slot.Itemstack;
            if (stack == null || stack.Collectible?.Code == null) continue;
            var slotd = slot as ItemSlotDisplay;
            getOrCreateMesh(slotd);

            var psurface = psurfaces[slotLoc.PlacementSurfaceIndex];
            var attr = BlockBehaviorDisplay.GetDisplayableAttributes(slot, psurface);
            if (attr == null) return;

            var surfaceVoxelOffset = psurface.VoxelPosition;
            var slotVoxelOffset = slotLoc;
            var voxelOffsetToSlot = BlockBehaviorDisplay.getOffsetFromPreviewCuboid(attr.Size, MeshAngleRad);
            float stackingOffsetY = slotLoc.Y * attr.Size.Height;

            float rndRot = attr.RandYRotAngle <= 0 ? 0 : GameMath.MurmurHash3Mod(Pos.X, Pos.Y + slotid.GetHashCode(), Pos.Z, attr.RandYRotAngle + 1) - attr.RandYRotAngle / 2;
            float rndscaleY = GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z + slotid.GetHashCode(), 101) - 50;

            if (customRotationDegBySlot != null && customRotationDegBySlot.TryGetValue(slotid, out var rot))
            {
                rndRot = rot;
            }

            TfMatrices[slotid] =
                new Matrixf()
                .Translate(0.5f, 0, 0.5f)
                .RotateYDeg(Block.Shape.rotateY)
                .RotateY(MeshAngleRad)
                .Translate(
                    (surfaceVoxelOffset.X + slotVoxelOffset.X + voxelOffsetToSlot.X) / 16f,
                    (surfaceVoxelOffset.Y + stackingOffsetY) / 16f,
                    (surfaceVoxelOffset.Z + slotVoxelOffset.Z + voxelOffsetToSlot.Z) / 16f
                )
                .Translate(
                    -0.5f + attr.Size.Width / 2 / 16f,
                    0,
                    -0.5f + attr.Size.Length / 2 / 16f
                )
                .RotateY(GameMath.DEG2RAD * rndRot)
                .Scale(1, 1 + rndscaleY / 1500f, 1)
                .Translate(-0.5f, 0, -0.5f)
                .Values
            ;
        }

        meshesGenerated = true;
    }

    public Vec3f placementDirection()
    {
        return new Vec3f();
    }


    protected virtual MeshData getOrCreateMesh(ItemSlotDisplay slot)
    {
        MeshData mesh = getCachedMesh(slot);
        if (mesh != null) return mesh;

        var collObj = slot.Itemstack.Collectible;

        var attr = BlockBehaviorDisplay.GetDisplayableAttributes(slot, slot.DisplayCategory);
        if (attr == null) return getDefaultMesh(slot);

        CompositeShape customShape = attr.Shape;
        if (customShape != null)
        {
            if (attr.Behavior == EnumDisplayableBehavior.Pileable)
            {
                string[] eles = new string[slot.StackSize];
                for (int i = 0; i < slot.StackSize; i++) eles[i] = attr.PileableSelectiveElements[i];
                customShape.SelectiveElements = eles;
            }

            string customkey = "displayedShape-" + collObj.Code + "-" + customShape.ToString();
            mesh = ObjectCacheUtil.GetOrCreate(capi, customkey, () =>
                capi.TesselatorManager.CreateMesh(
                    "displayed item shape",
                    customShape,
                    (shape, name) => {
                        var textures = new Dictionary<string, AssetLocation>(shape.Textures);
                        var collTextures = slot.Itemstack.Class == EnumItemClass.Item ? slot.Itemstack.Item.Textures : slot.Itemstack.Block.Textures;
                        foreach (var val in collTextures)
                        {
                            textures[val.Key] = val.Value.Base;
                        }
                        return new ContainedTextureSource(capi, capi.BlockTextureAtlas, textures, string.Format("For displayed item {0}", collObj.Code));
                    },
                    null
            ));
        }
        else
        {
            var meshSource = collObj?.GetCollectibleInterface<IContainedMeshSource>();
            mesh = meshSource?.GenMesh(slot, capi.BlockTextureAtlas, Pos);
        }

        if (mesh == null)
        {
            mesh = getDefaultMesh(slot);
        }

        applyDefaultTranforms(slot, mesh);
        MeshCache[getMeshCacheKey(slot)] = mesh;
        return mesh;
    }

    protected void applyDefaultTranforms(ItemSlotDisplay slot, MeshData mesh)
    {
        var stack = slot.Itemstack;
        var dispAttr = BlockBehaviorDisplay.GetDisplayableAttributes(slot, slot.DisplayCategory);

        if (dispAttr.Transform != null)
        {
            dispAttr.Transform.EnsureDefaultValues();
            mesh.ModelTransform(dispAttr.Transform);
        } else
        {
            if (stack.Class == EnumItemClass.Item && (stack.Item.Shape == null || stack.Item.Shape.VoxelizeTexture))
            {
                mesh.Rotate(GameMath.PIHALF, 0, 0);
                mesh.Scale(0.33f, 0.33f, 0.33f);
                mesh.Translate(0, -7.5f / 16f, 0f);
            }
        }
    }

    protected MeshData getDefaultMesh(ItemSlotDisplay slot)
    {
        MeshData mesh;
        ICoreClientAPI capi = Api as ICoreClientAPI;
        var stack = slot.Itemstack;
        if (stack.Class == EnumItemClass.Block)
        {
            mesh = capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();
        }
        else
        {
            nowTesselatingObj = stack.Collectible;
            nowTesselatingShape = null;
            if (stack.Item.Shape?.Base != null)
            {
                nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
            }
            capi.Tesselator.TesselateItem(stack.Item, out mesh, this);

            mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);
        }

        return mesh;
    }

    protected MeshData getCachedMesh(ItemSlot slot)
    {
        string key = getMeshCacheKey(slot);
        MeshCache.TryGetValue(key, out var meshdata);
        return meshdata;
    }

    protected virtual string getMeshCacheKey(ItemSlot slot)
    {
        var meshSource = slot.Itemstack.Collectible?.GetCollectibleInterface<IContainedMeshSource>();
        return meshSource?.GetMeshCacheKey(slot) ?? slot.Itemstack.Collectible.Code.ToString();
    }

    public void SetMeshAngleRad(float angleRad, IWorldAccessor worldForResolving)
    {
        if (MeshAngleRad == angleRad) return;

        MeshAngleRad = angleRad;
        selectionBoxes = null;
        collisionboxes = null;
        shelfSelectionBoxes = null;
        allBoxes = null;

        if (worldForResolving != null)
        {
            MarkMeshesDirty(worldForResolving);
        }
    }

    public void MarkDisplayDirty(IWorldAccessor worldForResolving)
    {
        MarkMeshesDirty(worldForResolving);
    }

    /// <summary>
    /// Methods implementing this class need to call this at the conclusion of their FromTreeAttributes implementation.  See BEGroundStorage for an example!
    /// </summary>
    protected virtual void MarkMeshesDirty(IWorldAccessor worldForResolving)
    {
        if (worldForResolving.Side == EnumAppSide.Client && Api != null)
        {
            meshesGenerated = false;
            Api.World.BlockAccessor.MarkBlockDirty(Pos);   // always redraw on client after updating meshes
        }

        regenSelectionBoxes();
    }

    protected TextureAtlasPosition getOrCreateTexPos(AssetLocation texturePath)
    {
        TextureAtlasPosition texpos = capi.BlockTextureAtlas[texturePath];

        if (texpos == null)
        {
            bool ok = capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out _, out texpos, null);

            if (!ok)
            {
                capi.World.Logger.Warning("For render in block " + Block.Code + ", item {0} defined texture {1}, no such texture found.", nowTesselatingObj.Code, texturePath);
                return capi.BlockTextureAtlas.UnknownTexturePosition;
            }
        }

        return texpos;
    }


    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        if (!meshesGenerated)
        {
            generateMeshes();
        }

        foreach (var (slotid, slot) in inventory.SlotsByslotId)
        {
            if (slot.Empty || slot.Itemstack.Collectible?.Code == null) continue;
            mesher.AddMeshData(getCachedMesh(slot), TfMatrices[slotid]);
        }

        var tfMatrix = new Matrixf().Translate(0.5f, 0, 0.5f).RotateY(MeshAngleRad).Translate(-0.5f, 0, -0.5f).Values;
        mesher.AddMeshData(blockMesh, tfMatrix);
        return true;
    }

    public Cuboidf[] GetCollisionBoxes()
    {
        if (collisionboxes == null)
        {
            collisionboxes = rotatedCopy(Block.CollisionBoxes);
        }

        return collisionboxes;
    }

    public Cuboidf[] GetSelectionBoxes()
    {
        if (selectionBoxes == null)
        {
            regenSelectionBoxes();
        }

        if (capi != null)
        {
            // Filtered selection boxes so placed items win over covered grid cells.
            if (Blockentity is BlockEntityShelf) return shelfSelectionBoxes;

            var bhd = Block.GetBehavior<BlockBehaviorDisplay>();
            if (bhd.UsingItemsPreview(this) || bhd.placingItemsPreview(this)) return allBoxes;
        }

        return selectionBoxes;
    }

    Cuboidf[] allBoxes;

    private void regenSelectionBoxes()
    {
        List<Cuboidf> cuboids = new List<Cuboidf>();
        // Keep the box groups separate so shelves can remove only grid cells covered by placed items.
        List<Cuboidf> blockCuboids = new List<Cuboidf>();
        List<Cuboidf> placementCuboids = new List<Cuboidf>();
        List<Cuboidf> placedItemCuboids = new List<Cuboidf>();

        // 1. The selection boxes of the block itself
        blockCuboids.AddRange(rotatedCopy(Block.SelectionBoxes));
        cuboids.AddRange(blockCuboids);

        // 2. Placement selection
        var selBoxes = Block.GetBehavior<BlockBehaviorDisplay>().SelectionBoxes;
        foreach (var selbox in selBoxes)
        {
            var rotBox = rotatedCopy(selbox);
            rotBox.Y1 -= 1 / 16f;
            rotBox.Y2 = rotBox.Y1 + 1 / 16f;
            cuboids.Add(rotBox);
            placementCuboids.Add(rotBox);
        }
        selectionBoxes = cuboids.ToArray();

        var psurfaces = Block.GetBehavior<BlockBehaviorDisplay>().PlacementSurfaces;

        // 3. Placed items
        foreach (var (slotid, slot) in inventory.SlotsByslotId)
        {
            if (slot.Empty) continue;

            var slotLoc = BlockBehaviorDisplay.decodeSlotid(slotid);
            var psurface = psurfaces[slotLoc.PlacementSurfaceIndex];
            var dattr = BlockBehaviorDisplay.GetDisplayableAttributes(slot, psurface);
            if (dattr == null) continue;

            var w = dattr.Size.Width / 16f;
            var h = dattr.Size.Height / 16f;
            var l = dattr.Size.Length / 16f;

            var surfaceVoxelOffset = psurface.VoxelPosition;
            var slotVoxelOffset = slotLoc;
            var voxelOffsetToSlot = BlockBehaviorDisplay.getOffsetFromPreviewCuboid(dattr.Size, MeshAngleRad);
            float stackingOffsetY = slotLoc.Y * dattr.Size.Height;

            var cub =
                new CuboidfWithId(0, 0, 0, w, h, l) { Id = "p-" + slotid }
                .Translate(
                    (surfaceVoxelOffset.X + slotVoxelOffset.X + voxelOffsetToSlot.X) / 16f,
                    (surfaceVoxelOffset.Y + stackingOffsetY) / 16f,
                    (surfaceVoxelOffset.Z + slotVoxelOffset.Z + voxelOffsetToSlot.Z) / 16f
                )
            ;


            var rotCub = rotatedCopy(cub);
            cuboids.Add(rotCub);
            // Remember item boxes so shelves can hide covered grid cells.
            placedItemCuboids.Add(rotCub);
        }

        // Let placed items win over grid cells on the same shelf surface.
        List<Cuboidf> shelfBoxes = new List<Cuboidf>();
        shelfBoxes.AddRange(blockCuboids);
        shelfBoxes.AddRange(placementCuboids.Where(cell => !placedItemCuboids.Any(itemBox => overlapsSameSurfaceXZ(cell, itemBox))));
        shelfBoxes.AddRange(placedItemCuboids);
        shelfSelectionBoxes = shelfBoxes.ToArray();

        allBoxes = cuboids.ToArray();
    }

    // XZ only, but only within the same shelf surface.
    private static bool overlapsSameSurfaceXZ(Cuboidf a, Cuboidf b)
    {
        var aLoc = BlockBehaviorDisplay.decodeSlotid((a as CuboidfWithId)?.Id);
        var bLoc = BlockBehaviorDisplay.decodeSlotid((b as CuboidfWithId)?.Id);

        if (aLoc == null || bLoc == null) return false;
        if (aLoc.PlacementSurfaceIndex != bLoc.PlacementSurfaceIndex) return false;

        return a.X1 < b.X2 && a.X2 > b.X1 && a.Z1 < b.Z2 && a.Z2 > b.Z1;
    }

    protected CuboidfWithId[] rotatedCopy(Cuboidf[] cuboids)
    {
        CuboidfWithId[] rotated = new CuboidfWithId[cuboids.Length];
        for (int i = 0; i < rotated.Length; i++)
        {
            rotated[i] = rotatedCopy(cuboids[i]);
        }

        return rotated;
    }

    public CuboidfWithId rotatedCopy(Cuboidf cuboid)
    {
        var rotCub = cuboid.RotatedCopyRad(0, MeshAngleRad, 0, new Vec3d(0.5, 0, 0.5));

        var rotCubIdd = new CuboidfWithId();
        rotCubIdd.Set(rotCub);
        rotCubIdd.Id = (cuboid as CuboidfWithId)?.Id;
        return rotCubIdd;
    }


    public override void OnBlockBroken(IPlayer byPlayer = null)
    {
        inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
    }

    public bool DoPartialSelection() => true;

    public int GetParticleColorTextureSubId()
    {
        var titem = displayStack?.Collectible.GetCollectibleBehavior<CollectibleBehaviorTypedTexture>(true);
        if (titem != null)
        {
            var loc = TypedTextureSource.getMappedTexture("wood-top", displayStack!.Collectible.Code.Domain, titem.GetMaterials(displayStack), titem.TextureMapping);
            capi.BlockTextureAtlas.GetOrInsertTexture(loc, out int subid, out _);
            return subid;
        }
        else
        {
            return Block.TextureSubIdForBlockColor;
        }
    }
}
