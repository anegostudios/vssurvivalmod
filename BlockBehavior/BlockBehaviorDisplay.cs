using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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

        public string EncodedLocation => Y > 0 ? (PlacementSurfaceIndex + "-" + X + "-" + Z + "-" + Y) : (PlacementSurfaceIndex + "-" + X + "-" + Z);

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

    public class BlockBehaviorDisplay : BlockBehavior, ICustomSelectionBoxRender
    {
        public PlacementSurface[] PlacementSurfaces;
        public CuboidfWithId[] SelectionBoxes;
        protected int maxXDivisions = 32;
        protected int maxZDivisions = 32;

        ICoreClientAPI capi;
        public static Size3f DefaultItemSize = new Size3f(6, 4, 6);

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

                        cubf.Id = i + "-" + (x * xstep) + "-" + (z * zstep);
                        selBoxes.Add(cubf);
                        slotCount++;
                    }
                }

                i++;
            }

            SelectionBoxes = selBoxes.ToArray();
            PlacementSurfaces = psurfaces.ToArray();
        }


        private void OnGetTransform(string eventName, ref EnumHandling handling, IAttribute data)
        {
            var tree = data as TreeAttribute;

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

            if (placingItemsPreview(be))
            {
                renderBoxHandler(selBox, linewidthMul, block.GetSelectionColor(capi, blockSel.Position));
                return;
            }

            if (selectedSlotId.StartsWith("p-"))
            {
                selBox = selBoxes.FirstOrDefault(box => (box as CuboidfWithId)?.Id == selectedSlotId.Substring(2));
            }

            var psurface = PlacementSurfaces[slotLoc.PlacementSurfaceIndex];
            string displayType = psurface.DisplayCategory ?? "shelf";

            Size3f size = getItemSize(heldSlot, displayType);
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

                if (placeable && be.getCollidingSlotId(blockSel, new Cuboidf(size)) != null)
                {
                    placeable = false;
                }

                selBox = selBox.RotatedCopyRad(0, -be.MeshAngleRad, 0, new Vec3d(0.5, 0, 0.5));

                Cuboidf previewCuboid =
                    new Cuboidf(0, 1 / 16f, 0, wm, hm + 1 / 16f, lm)
                    .Translate(selBox.Start)
                    .Translate(offset.X / 16f, 0, offset.Z / 16f)
                ;

                previewCuboid = previewCuboid.RotatedCopyRad(0, be.MeshAngleRad, 0, new Vec3d(0.5, 0, 0.5));

                renderBoxHandler(previewCuboid, linewidthMul, placeable ? new Vec4f(0, 0, 0, 0.5f) : new Vec4f(0.5f, 0, 0, 0.5f));
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
            string displayType = psurface.DisplayCategory ?? "shelf";

            var dattr = GetDisplayableAttributes(heldSlot, displayType);

            if (dattr?.Behavior == EnumDisplayableBehavior.Stacking && be.getCollidingSlotId(blockSel, new Cuboidf(dattr.Size)) != null)
            {
                return true;
            }

            return false;
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
            var collObj = slot.Itemstack?.Collectible;

            var interfaceSize = collObj?.GetCollectibleInterface<IDisplayableProps>()?.GetDisplayableProps(slot, displayType);
            if (interfaceSize != null) return interfaceSize.Size;

            var size = collObj?.Attributes?["displayable"][displayType]["size"].AsObject(DefaultItemSize);
            return size;
        }

        public static DisplayableAttributes GetDisplayableAttributes(ItemSlot slot, string displayType)
        {
            var collObj = slot.Itemstack?.Collectible;

            var interfaceProps = collObj?.GetCollectibleInterface<IDisplayableProps>()?.GetDisplayableProps(slot, displayType);
            if (interfaceProps != null) return interfaceProps;

            var heldDAttr = collObj?.Attributes?["displayable"][displayType].AsObject<DisplayableAttributes>(null);
            if (heldDAttr != null) return heldDAttr;

            // Backwards compatible check
            if (collObj?.Attributes?.IsTrue("shelvable") == true)
            {
                return new DisplayableAttributes() {
                    Size = DefaultItemSize,
                    Transform = collObj?.Attributes["onshelfTransform"].AsObject<ModelTransform>() ?? collObj?.Attributes["onDisplayTransform"].AsObject<ModelTransform>()
                };
            }

            return null;
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
    }
}
