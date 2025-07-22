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
    public class BlockEntityScrollRack : BlockEntityDisplay
    {
        public override InventoryBase Inventory => inv!;
        public override string InventoryClassName => "scrollrack";
        public override string AttributeTransformCode => "onscrollrackTransform";

        public float MeshAngleRad
        {
            get => bh?.MeshAngleY ?? 0;
            set => bh!.MeshAngleY = value;
        }

        InventoryGeneric? inv;

        public string? Type => bh?.Type;
        public string? Material => bh?.Material;

        int[]? UsableSlots;
        Cuboidf[] UsableSelectionBoxes;
        private BEBehaviorShapeMaterialFromAttributes? bh;

        public BlockEntityScrollRack()
        {
            inv = new InventoryGeneric(12, "scrollrack-0", null, null);
        }

        public int[]? getOrCreateUsableSlots()
        {
            if (UsableSlots == null) genUsableSlots();
            return UsableSlots;
        }

        public Cuboidf[] getOrCreateSelectionBoxes()
        {
            getOrCreateUsableSlots();
            return UsableSelectionBoxes;
        }

        /*
         * Slots 0-11:
         *   01 on the base  (always empty)
         *   234 bottom row  (left only usable if another rack on the side...  but which is left depends on which way we are facing :o )
         *   56 centre pair   (always usable)
         *   789 top row    (left only usable if another rack on the side...  but which is left depends on which way we are facing :o )
         *   1011 on the top (usable even if no other rack above)
         */

        private void genUsableSlots()
        {
            var left = isRack(BEBehaviorDoor.getAdjacentOffset(-1, 0, 0, MeshAngleRad, false));

            var slotsBySide = ((BlockScrollRack)Block).slotsBySide;
            List<int> usableSlots = new List<int>();

            usableSlots.AddRange(slotsBySide["mid"]);
            usableSlots.AddRange(slotsBySide["top"]);
            if (left) usableSlots.AddRange(slotsBySide["left"]);

            UsableSlots = usableSlots.ToArray();

            var hitboxes = ((BlockScrollRack)Block).slotsHitBoxes;
            UsableSelectionBoxes = new Cuboidf[hitboxes.Length];
            for (int i = 0; i < hitboxes.Length; i++)
            {
                UsableSelectionBoxes[i] = hitboxes[i].RotatedCopy(0, MeshAngleRad * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5));
            }
        }

        private bool isRack(Vec3i offset)
        {
            var be = Api.World.BlockAccessor
                .GetBlockEntity<BlockEntityScrollRack>(Pos.AddCopy(offset))
                ?.GetBehavior<BEBehaviorShapeMaterialFromAttributes>();
            return be != null && be.MeshAngleY == MeshAngleRad;
        }

        public override void Initialize(ICoreAPI api)
        {
            bh = GetBehavior<BEBehaviorShapeMaterialFromAttributes>();
            base.Initialize(api);
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            getOrCreateUsableSlots();
            var bsr = ((BlockScrollRack)Block);
            var slotSides = bsr.slotSide;
            var oppositeSlotIndex = bsr.oppositeSlotIndex;
            var slotside = slotSides[blockSel.SelectionBoxIndex];
            if (slotside == "bot" || slotside == "right")
            {
                var npos = slotside == "bot" ? Pos.DownCopy() : Pos.AddCopy(BEBehaviorDoor.getAdjacentOffset(1, 0, 0, MeshAngleRad, false));
                var be = Api.World.BlockAccessor.GetBlockEntity<BlockEntityScrollRack>(npos);
                if (be == null) return false;
                float theirAngle = GameMath.NormaliseAngleRad(be.MeshAngleRad);
                float ourAngle = GameMath.NormaliseAngleRad(MeshAngleRad);
                if (theirAngle % GameMath.PI == ourAngle % GameMath.PI)
                {
                    if (theirAngle != ourAngle && slotside == "right") return false;
                    var blockSelDown = blockSel.Clone();
                    blockSelDown.SelectionBoxIndex = oppositeSlotIndex[theirAngle == ourAngle ? blockSelDown.SelectionBoxIndex : blockSelDown.SelectionBoxIndex ^ 1];
                    return be.OnInteract(byPlayer, blockSelDown);
                }
                return false;
            }

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            CollectibleObject? colObj = slot.Itemstack?.Collectible;
            bool shelvable = colObj?.Attributes != null && colObj.Attributes["scrollrackable"].AsBool(false) == true;

            if (slot.Empty || !shelvable)
            {
                if (TryTake(byPlayer, blockSel))
                {
                    return true;
                }
                return false;
            }

            if (shelvable)
            {
                AssetLocation? sound = slot.Itemstack?.Block?.Sounds?.Place;

                var stackName = slot.Itemstack?.Collectible.Code;
                if (TryPut(slot, blockSel))
                {
                    Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                    Api.World.Logger.Audit("{0} Put 1x{1} into Scroll rack at {2}.",
                        byPlayer.PlayerName,
                        stackName,
                        Pos
                    );
                    return true;
                }
            }

            return false;
        }

        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            int boxIndex = blockSel.SelectionBoxIndex;
            if (boxIndex < 0 || boxIndex >= Inventory.Count) return false;
            if (!UsableSlots.Contains(boxIndex)) return false;

            int invIndex = boxIndex;

            if (inv[invIndex].Empty)
            {
                int moved = slot.TryPutInto(Api.World, inv[invIndex]);
                MarkDirty();
                return moved > 0;
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            int boxIndex = blockSel.SelectionBoxIndex;
            if (boxIndex < 0 || boxIndex >= Inventory.Count) return false;

            int invIndex = boxIndex;

            if (!Inventory[invIndex].Empty)
            {
                ItemStack stack = Inventory[invIndex].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    AssetLocation? sound = stack.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                }

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos);
                }
                Api.World.Logger.Audit("{0} Took 1x{1} from Scroll rack at {2}.",
                    byPlayer.PlayerName,
                    stack.Collectible.Code,
                    Pos
                );

                MarkDirty();
                return true;
            }

            return false;
        }

        protected override float[][] genTransformationMatrices()
        {
            tfMatrices = new float[Inventory.Count][];
            var hitboxes = ((BlockScrollRack)Block).slotsHitBoxes;

            for (int i = 0; i < Inventory.Count; i++)
            {
                var hitbox = hitboxes[i];
                float x = hitbox.MidX;
                float y = hitbox.MidY;
                float z = hitbox.MidZ;

                Vec3f off = new Vec3f(x, y, z);
                off = new Matrixf().RotateY(MeshAngleRad).TransformVector(off.ToVec4f(0)).XYZ;

                tfMatrices[i] =
                    new Matrixf()
                    .Translate(off.X, off.Y, off.Z)
                    .Translate(0.5f, 0, 0.5f)
                    //.Translate(0.5f - 7.25f/16f, 0 - 2.5f/16f, 0.5f - 10/16f)
                    .RotateY(MeshAngleRad - GameMath.PIHALF)
                    //.RotateX(GameMath.PIHALF / 2)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values
                ;
            }

            return tfMatrices;
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("usableSlotsDirty", UsableSlots == null);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (tree.GetBool("usableSlotsDirty")) UsableSlots = null;

            bh?.Init();

            // Do this last!!!
            RedrawAfterReceivingTreeAttributes(worldForResolving);     // Redraw on client after we have completed receiving the update from server
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (forPlayer.CurrentBlockSelection == null)
            {
                base.GetBlockInfo(forPlayer, sb);
                return;
            }

            int index = forPlayer.CurrentBlockSelection.SelectionBoxIndex;

            if (index < 0 || index >= Inventory.Count)
            {
                base.GetBlockInfo(forPlayer, sb);
                return;
            }

            ItemSlot slot = Inventory[index];
            if (slot.Empty)
            {
                // If we are a rack-edge slot and it is full in the other rack, show contents correctly - otherwise it shows 50/50 as empty depending on which of the two blocks the player is precisely looking at
                var slotSides = ((BlockScrollRack)Block).slotSide;
                var slotside = slotSides[index];
                if (slotside == "bot")
                {
                    var be = Api.World.BlockAccessor.GetBlockEntity<BlockEntityScrollRack>(Pos.DownCopy());
                    if (be != null)
                    {
                        float theirAngle = GameMath.NormaliseAngleRad(be.MeshAngleRad);
                        float ourAngle = GameMath.NormaliseAngleRad(MeshAngleRad);
                        if (theirAngle % GameMath.PI == ourAngle % GameMath.PI) slot = be.inv[theirAngle == ourAngle ? index + 10 : 11 - index];
                    }
                }
                else if (slotside == "right")
                {
                    var be = Api.World.BlockAccessor.GetBlockEntity<BlockEntityScrollRack>(Pos.AddCopy(BEBehaviorDoor.getAdjacentOffset(1, 0, 0, MeshAngleRad, false)));
                    if (be != null)
                    {
                        float theirAngle = GameMath.NormaliseAngleRad(be.MeshAngleRad);
                        float ourAngle = GameMath.NormaliseAngleRad(MeshAngleRad);
                        if (theirAngle == ourAngle) slot = be.Inventory[index - 2];
                    }
                }

                sb.AppendLine(slot.Empty ? Lang.Get("Empty") : slot.Itemstack.GetName());
            }
            else
            {
                sb.AppendLine(slot.Itemstack.GetName());
            }
        }

        internal void clearUsableSlots()
        {
            genUsableSlots();
            for (int i = 0; i < Inventory.Count; i++)
            {
                if (UsableSlots.Contains<int>(i)) continue;
                ItemSlot slot = Inventory[i];
                if (slot.Empty) continue;

                // Drop contents which can no longer be held if neighbour removed
                Vec3d vec = Pos.ToVec3d();
                vec.Add(0.5 - GameMath.Cos(MeshAngleRad) * 0.6, 0.15, 0.5 + GameMath.Sin(MeshAngleRad) * 0.6);  // Add appropriate offset for the removed side, depending on orientation
                Api.World.SpawnItemEntity(slot.Itemstack, vec);
                slot.Itemstack = null;
            }

            MarkDirty(true);
        }
    }
}
