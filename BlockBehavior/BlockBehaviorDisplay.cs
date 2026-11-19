using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class SlotLocation : Vec3f
    {
        public int PlacementSurfaceIndex;
        public bool IsPlacedItem;

        public SlotLocation(float x, float y, float z) : base(x, y, z) { }

        public string EncodedLocation => Y > 0
            ? string.Format(GlobalConstants.DefaultCultureInfo, "{0}-{1}-{2}-{3}", PlacementSurfaceIndex, X, Z, Y)
            : string.Format(GlobalConstants.DefaultCultureInfo, "{0}-{1}-{2}", PlacementSurfaceIndex, X, Z);

        public SlotLocation UpCopy()
        {
            return new SlotLocation(X, Y + 1, Z) { PlacementSurfaceIndex = PlacementSurfaceIndex, IsPlacedItem = IsPlacedItem };
        }
    }

    public class PlacementSurface
    {
        public string ElementName;
        public int Index;
        /// <summary>
        /// Size in Voxels
        /// </summary>
        public Size3i Size;
        public Vec3f VoxelPosition;
        public string DisplayCategory;
    }

    /// <summary>
    /// Determines the behavior how the displayable can be placed
    /// </summary>
    public enum EnumDisplayableBehavior
    {
        /// <summary>
        /// Item can be placed on top of others
        /// </summary>
        Default,
        /// <summary>
        /// The same item can be placed on top itself
        /// </summary>
        Pileable,
        /// <summary>
        /// Different items of the same class can be placed on top of others
        /// </summary>
        Stacking
    }

    public class DisplayableAttributes
    {
        public EnumDisplayableBehavior Behavior;
        /// <summary>
        /// Size in Voxels
        /// </summary>
        public Size3f Size;
        public CompositeShape Shape;
        public ModelTransform Transform;
        /// <summary>
        /// Angle in degrees
        /// </summary>
        public int RandYRotAngle = 30;
        public string Category;
        public string[] PileableSelectiveElements;
    }

    public class BlockBehaviorDisplay : StrongBlockBehavior, ICustomSelectionBoxRender
    {
        public PlacementSurface[] PlacementSurfaces;
        public CuboidfWithId[] SelectionBoxes;
        protected int maxXDivisions = 32;
        protected int maxZDivisions = 32;

        ICoreClientAPI capi;
        public static Size3f DefaultItemSize = new Size3f(6, 4, 6);

        public static bool IsUsingContainedBlock; // This value is only relevant (and correct) client side

        public BlockBehaviorDisplay(Block block) : base(block) { }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            maxXDivisions = properties["maxXDivisions"].AsInt(32);
            maxZDivisions = properties["maxZDivisions"].AsInt(32);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            capi = api as ICoreClientAPI;
            capi?.Event.RegisterEventBusListener(OnGetTransform, 0.5, "ongettransform");
            capi?.Event.MouseUp += Event_MouseUp;

            base.OnLoaded(api);
            var shape = api.Assets.Get<Shape>(block.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
            List<PlacementSurface> psurfaces = new List<PlacementSurface>();
            List<CuboidfWithId> selBoxes = new List<CuboidfWithId>();

            int slotCount = 0;
            int i = 0;
            foreach (var element in shape.Elements)
            {
                if (!element.Name.StartsWith("psurface")) continue;

                var parts = element.Name["psurface".Length..].Split('-');

                var size = new Size3i(parts[1][1..].ToInt(), parts[2][1..].ToInt(), parts[3][1..].ToInt());
                var baseBos = new Vec3f((float)element.From[0], (float)element.From[1], (float)element.From[2]);
                psurfaces.Add(new PlacementSurface()
                {
                    ElementName = element.Name,
                    Index = parts[0].ToInt(),
                    Size = size,
                    VoxelPosition = baseBos,
                    DisplayCategory = parts.Length > 4 ? parts[4] : null
                });

                int xDivisons = Math.Min(maxXDivisions, size.Width);
                int zDivisons = Math.Min(maxZDivisions, size.Length);

                var xstep = (element.To[0] - baseBos.X) / xDivisons;
                var zstep = (element.To[2] - baseBos.Z) / zDivisons;

                for (int x = 0; x < xDivisons; x++)
                {
                    double x1 = baseBos.X + x * xstep;
                    double x2 = baseBos.X + (x+1) * xstep;

                    for (int z = 0; z < zDivisons; z++)
                    {
                        double z1 = baseBos.Z + z * zstep;
                        double z2 = baseBos.Z + (z + 1) * zstep;

                        var cubf = new CuboidfWithId(
                            x1 / 16.0,
                            baseBos.Y / 16.0,
                            z1 / 16.0,
                            x2 / 16.0,
                            baseBos.Y / 16.0,
                            z2 / 16.0
                        );

                        cubf.Id = new SlotLocation((float)(x * xstep), 0, (float)(z * zstep))
                        {
                            PlacementSurfaceIndex = i
                        }.EncodedLocation;
                        selBoxes.Add(cubf);
                        slotCount++;
                    }
                }

                i++;
            }

            SelectionBoxes = selBoxes.ToArray();
            PlacementSurfaces = psurfaces.ToArray();
        }

        private void Event_MouseUp(MouseEvent e)
        {
            IsUsingContainedBlock = false;
        }


        private void OnGetTransform(string eventName, ref EnumHandling handling, IAttribute data)
        {
            var tree = data as TreeAttribute;
            if (tree.GetString("target") != "onshelfTransform") return;

            ItemSlot slot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            var attr = GetDisplayableAttributes(slot, "shelf");
            if (attr?.Transform == null) return;

            handling = EnumHandling.PreventDefault;
            tree.SetBool("preventDefault", true);
            attr.Transform.ToTreeAttribute(tree);
        }

        public void RenderSelectionBoxes(BlockSelection blockSel, RenderBoxDelegate renderBoxHandler)
        {
            float linewidthMul = 1.6f * capi.Settings.Float["wireframethickness"];

            var be = block.GetBEBehavior<BEBehaviorDisplay>(blockSel.Position);
            if (be == null)
            {
                renderBoxHandler(Cuboidf.Default(), linewidthMul, block.GetSelectionColor(capi, blockSel.Position));
                return;
            }

            var selBoxes = block.GetSelectionBoxes(capi.World.BlockAccessor, blockSel.Position);
            var heldSlot = capi.World.Player.Entity.RightHandItemSlot;

            // We're looking at a wall or have no item in hands and not looking at a placed object
            var selectedSlotId = blockSel.SelectionBoxId;
            var slotLoc = decodeSlotid(selectedSlotId);

            if (blockSel.SelectionBoxIndex >= selBoxes.Length) return;

            var selBox = selBoxes[blockSel.SelectionBoxIndex];

            if (UsingItemsPreview(be) || placingItemsPreview(be))
            {
                renderBoxHandler(selBox, linewidthMul, block.GetSelectionColor(capi, blockSel.Position));
                return;
            }

            if (selectedSlotId.StartsWith("p-"))
            {
                selBox = selBoxes.FirstOrDefault(box => (box as CuboidfWithId)?.Id == selectedSlotId.Substring(2)) ?? selBox;
            }

            var psurface = PlacementSurfaces[slotLoc.PlacementSurfaceIndex];
            Size3f size = getItemSize(heldSlot, psurface);
            if (size != null)
            {
                bool placeable = true;
                var targetSlotId = blockSel.SelectionBoxId;

                var wm = size.Width / 16;
                var hm = size.Height / 16;
                var lm = size.Length / 16;

                var offset = getOffsetFromPreviewCuboid(size, be.MeshAngleRad);

                if (slotLoc.X + offset.X < 0 || slotLoc.Z + offset.Z < 0 || slotLoc.X + offset.X > psurface.Size.Width - size.Width || slotLoc.Z + offset.Z > psurface.Size.Length - size.Length)
                {
                    placeable = false;
                }

                if (psurface.Size.Width < size.Width || psurface.Size.Height < size.Height || psurface.Size.Length < size.Length)
                {
                    placeable = false;
                }

                if (placeable && be.getCollidingSlotId(blockSel, new Cuboidf(size)) != null)
                {
                    placeable = false;
                }

                selBox = selBox.RotatedCopyRad(0, -be.MeshAngleRad, 0, new Vec3d(0.5, 0, 0.5));

                bool shelfUndersidePreview = capi.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityShelf
                    && blockSel.Face == BlockFacing.DOWN;

                Cuboidf previewCuboid =
                    new Cuboidf(0, shelfUndersidePreview ? -1 / 16f : 1 / 16f, 0, wm, hm + 1 / 16f, lm)
                    .Translate(selBox.Start)
                    .Translate(offset.X / 16f, 0, offset.Z / 16f)
                ;

                previewCuboid = previewCuboid.RotatedCopyRad(0, be.MeshAngleRad, 0, new Vec3d(0.5, 0, 0.5));

                Vec4f color = placeable ? new Vec4f(0, 0, 0, 0.5f) : new Vec4f(0.5f, 0, 0, 0.5f);

                renderBoxHandler(previewCuboid, linewidthMul, color);
            }

            return;
        }

        public bool placingItemsPreview(BEBehaviorDisplay be)
        {
            var heldSlot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            var blockSel = capi?.World.Player.CurrentBlockSelection;
            var selectedSlotId = blockSel?.SelectionBoxId;
            if (heldSlot == null || heldSlot.Empty || selectedSlotId == null || heldSlot.Itemstack.Collectible.GetTool(heldSlot) == EnumTool.Wrench) return true;

            var slotLoc = decodeSlotid(selectedSlotId);
            if (slotLoc == null) return true;
            var psurface = PlacementSurfaces[slotLoc.PlacementSurfaceIndex];
            var dattr = GetDisplayableAttributes(heldSlot, psurface);

            if (dattr?.Behavior == EnumDisplayableBehavior.Stacking)
            {
                var placedSlot = be.InventoryInv[selectedSlotId];
                return placedSlot != null && GetDisplayableAttributes(placedSlot, psurface)?.Behavior == EnumDisplayableBehavior.Stacking;
            }

            return false;
        }

        public bool UsingItemsPreview(BEBehaviorDisplay be)
        {
            var blockSel = capi?.World.Player.CurrentBlockSelection;
            if (blockSel == null) return false;
            var selectedSlotId = be.getSelectedNonEmptySlotId(blockSel);
            if (selectedSlotId == null) return false;
            var slot = be.InventoryInv[selectedSlotId];

            return slot?.Itemstack?.Collectible.GetCollectibleInterface<IContainedInteractable>() != null;
        }


        /// <summary>
        /// Returns offset value in voxels
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static Vec3f getOffsetFromPreviewCuboid(Size3f size, float rad)
        {
            return new Vec3f(
                -size.Width / 2,
                0,
                -size.Length / 2
            );
        }

        public static Size3f getItemSize(ItemSlot slot, string displayType)
        {
            return GetDisplayableAttributes(slot, displayType)?.Size;
        }

        public static Size3f getItemSize(ItemSlot slot, PlacementSurface psurface)
        {
            return GetDisplayableAttributes(slot, psurface)?.Size;
        }

        public static DisplayableAttributes GetDisplayableAttributes(ItemSlot slot, PlacementSurface psurface)
        {
            return GetDisplayableAttributes(slot, psurface.DisplayCategory ?? "shelf", psurface);
        }

        public static DisplayableAttributes GetDisplayableAttributes(ItemSlot slot, string displayType)
        {
            return GetDisplayableAttributes(slot, displayType, null);
        }

        private static DisplayableAttributes GetDisplayableAttributes(ItemSlot slot, string displayType, PlacementSurface psurface)
        {
            var collObj = slot.Itemstack?.Collectible;

            var interfaceProps = collObj?.GetCollectibleInterface<IDisplayableProps>()?.GetDisplayableProps(slot, displayType);
            if (interfaceProps != null) return interfaceProps;

            var heldDAttr = collObj?.Attributes?["displayable"][displayType].AsObject<DisplayableAttributes>(null);
            if (heldDAttr != null) return heldDAttr;

            if (displayType == "shelf" && BlockEntityShelf.GetShelvableLayout(slot.Itemstack) is EnumShelvableLayout layout)
            {
                return new DisplayableAttributes()
                {
                    Size = getLegacyShelfItemSize(layout, psurface),
                    Transform = getLegacyShelfTransform(slot, layout)
                };
            }

            if (collObj?.Attributes?.IsTrue("shelvable") == true)
            {
                return new DisplayableAttributes()
                {
                    Size = DefaultItemSize,
                    Transform = collObj?.Attributes["onshelfTransform"].AsObject<ModelTransform>() ?? collObj?.Attributes["onDisplayTransform"].AsObject<ModelTransform>()
                };
            }

            return null;
        }

        private static Size3f getLegacyShelfItemSize(EnumShelvableLayout layout, PlacementSurface psurface)
        {
            if (psurface == null) return DefaultItemSize;

            return layout switch
            {
                EnumShelvableLayout.Halves => new Size3f(psurface.Size.Width / 2f, DefaultItemSize.Height, psurface.Size.Length),
                EnumShelvableLayout.SingleCenter => new Size3f(psurface.Size.Width, DefaultItemSize.Height, psurface.Size.Length),
                _ => DefaultItemSize
            };
        }

        private static ModelTransform getLegacyShelfTransform(ItemSlot slot, EnumShelvableLayout layout)
        {
            ItemStack stack = slot.Itemstack;
            CollectibleObject collObj = stack.Collectible;
            JsonObject attr = collObj.Attributes;

            return collObj.GetCollectibleInterface<IShelvable>()?.GetOnShelfTransform(stack)
                ?? layout switch
                {
                    EnumShelvableLayout.Quadrants => attr?["onShelfQuarterTransform"].AsObject<ModelTransform>(),
                    EnumShelvableLayout.Halves => attr?["onShelfHalfTransform"].AsObject<ModelTransform>(),
                    EnumShelvableLayout.SingleCenter => attr?["onShelfFullTransform"].AsObject<ModelTransform>(),
                    _ => null
                }
                ?? attr?["onshelfTransform"].AsObject<ModelTransform>()
                ?? attr?["onDisplayTransform"].AsObject<ModelTransform>()
            ;
        }

        public static SlotLocation decodeSlotid(string slotid)
        {
            if (slotid == null) return null;

            bool placedItem = slotid.StartsWith("p-");
            var parts = (placedItem ? slotid.Substring(2) : slotid).Split('-');

            if (parts.Length < 3) return null;

            return new SlotLocation(parts[1].ToFloat(), parts.Length > 3 ? parts[3].ToFloat() : 0, parts[2].ToFloat())
            {
                PlacementSurfaceIndex = parts[0].ToInt(),
                IsPlacedItem = placedItem
            };
        }



        public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            int texSubId = block.GetBEBehavior<BEBehaviorDisplay>(pos)?.GetParticleColorTextureSubId() ?? 0;
            return capi.BlockTextureAtlas.GetAverageColor(texSubId) | 0xff << 24;
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            int texSubId = block.GetBEBehavior<BEBehaviorDisplay>(pos)?.GetParticleColorTextureSubId() ?? 0;
            return capi.BlockTextureAtlas.GetRandomColor(texSubId, rndIndex) | 0xff << 24;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            if (block.GetBEBehavior<BEBehaviorDisplay>(pos)?.DisplayStack is { } displayStack)
            {
                handling = EnumHandling.PreventDefault;

                return displayStack.Clone();
            }

            return base.OnPickBlock(world, pos, ref handling);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
        {
            if (block.GetBEBehavior<BEBehaviorDisplay>(pos)?.DisplayStack is { } displayStack)
            {
                handling = EnumHandling.PreventDefault;

                return [displayStack];
            }

            return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            var drops = block.Drops;
            drops[0] = drops[0].Clone();
            ArgumentNullException.ThrowIfNull(drops[0].ResolvedItemstack);
            drops[0].ResolvedItemstack!.SetFrom(handbookStack);

            return drops;
        }

        public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (block.GetBEBehavior<BEBehaviorDisplay>(pos)?.MeshAngleRad is { } meshAngleRad)
            {
                decalMesh.Rotate(0, meshAngleRad, 0);
            }
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            var be = block.GetBEBehavior<BEBehaviorDisplay>(selection.Position);
            if (be == null) return [];

            WorldInteraction[] interactions = ObjectCacheUtil.GetOrCreate(world.Api, "displayBlockInteractions", () =>
            {
                List<ItemStack> displayStacklist = new List<ItemStack>();

                int count = 5;
                string currentCodePart = "";

                foreach (var hstack in ObjectCacheUtil.TryGet<ItemStack[]>(world.Api, "handbookallstacks"))
                {
                    DisplayableAttributes displayableProps = hstack.Collectible.GetCollectibleInterface<IDisplayableProps>()?.GetDisplayableProps(new DummySlot(hstack), "shelf");
                    displayableProps ??= hstack.ItemAttributes?["displayable"]?["shelf"]?.AsObject<DisplayableAttributes>();
                    if (displayableProps == null) continue;

                    if (hstack.Collectible.Code.FirstPathPart() != currentCodePart)
                    {
                        currentCodePart = hstack.Collectible.Code.FirstPathPart();
                        count = 0;
                    }

                    if (count >= 5) continue;

                    displayStacklist.Add(hstack);
                }

                displayStacklist.Shuffle(world.Api.World.Rand);

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-shelf-place",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = displayStacklist.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-shelf-take",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "rotate-displayable-item-90-cw",
                        HotKeyCode = "ctrl",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = ObjectCacheUtil.GetToolStacks(world.Api, EnumTool.Wrench)
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "rotate-displayable-item-90-ccw",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = ObjectCacheUtil.GetToolStacks(world.Api, EnumTool.Wrench)
                    }
                };
            });

            return interactions;
        }
    }
}
