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

namespace Vintagestory.GameContent
{
    
    public class BlockEntityScrollRack : BlockEntityDisplay, IRotatable
    {
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "scrollrack";
        public override string AttributeTransformCode => "onscrollrackTransform";
        public float MeshAngleRad { get; set; }

        InventoryGeneric inv;
        Block block;
        MeshData mesh;

        string type, material;
        float[] mat;

        public string Type => type;
        public string Material => material;

        int[] UsableSlots;
        Cuboidf[] UsableSelectionBoxes;

        public BlockEntityScrollRack()
        {
            inv = new InventoryGeneric(12, "scrollrack-0", null, null);
        }

        public int[] getOrCreateUsableSlots()
        {
            if (UsableSlots != null) return UsableSlots;
            genUsableSlots();
            return UsableSlots;
        }
        public Cuboidf[] getOrCreateSelectionBoxes()
        {
            getOrCreateUsableSlots();
            return UsableSelectionBoxes;
        }

        private void genUsableSlots()
        {
            //var bot = isRack(new Vec3i(0, -1, 0));
            var left = isRack(BEBehaviorDoor.getAdjacentOffset(-1, 0, 0, MeshAngleRad, false));
            var right = isRack(BEBehaviorDoor.getAdjacentOffset(1, 0, 0, MeshAngleRad, false));

            var slotsBySide = (Block as BlockScrollRack).slotsBySide;
            List<int> usableSlots = new List<int>();

            usableSlots.AddRange(slotsBySide["mid"]);
            usableSlots.AddRange(slotsBySide["top"]);

            //if (bot) usableSlots.AddRange(slotsBySide["bot"]);
            if (left) usableSlots.AddRange(slotsBySide["left"]);
            //if (right) usableSlots.AddRange(slotsBySide["right"]);
            this.UsableSlots = usableSlots.ToArray();

            var hitboxes = (Block as BlockScrollRack).slotsHitBoxes;
            UsableSelectionBoxes = new Cuboidf[hitboxes.Length];
            for (int i = 0; i < hitboxes.Length; i++)
            {
                UsableSelectionBoxes[i] = hitboxes[i].RotatedCopy(0, MeshAngleRad * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5));
            }
        }

        private bool isRack(Vec3i offset)
        {
            var be = Api.World.BlockAccessor.GetBlockEntity<BlockEntityScrollRack>(Pos.AddCopy(offset));
            return be != null && be.MeshAngleRad == MeshAngleRad;
        }

        void initShelf()
        {
            if (Api == null || type == null || !(Block is BlockScrollRack)) return;

            if (Api.Side == EnumAppSide.Client)
            {
                mesh = (Block as BlockScrollRack).GetOrCreateMesh(type, material);
                mat = Matrixf.Create().Translate(0.5f, 0.5f, 0.5f).RotateY(MeshAngleRad).Translate(-0.5f, -0.5f, -0.5f).Values;
            }

            if (!(block is BlockScrollRack)) return;

            type = "normal";
        }

        public override void Initialize(ICoreAPI api)
        {
            block = api.World.BlockAccessor.GetBlock(Pos);
            base.Initialize(api);

            if (mesh == null && type != null)
            {
                initShelf();
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            type = byItemStack?.Attributes.GetString("type");
            material = byItemStack?.Attributes.GetString("material");

            initShelf();
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            getOrCreateUsableSlots();

            var slotSides = (Block as BlockScrollRack).slotSide;
            var oppositeSlotIndex = (Block as BlockScrollRack).oppositeSlotIndex;
            var slotside = slotSides[blockSel.SelectionBoxIndex];
            if (slotside == "bot" || slotside == "right")
            {
                var npos = slotside == "bot" ? Pos.DownCopy() : Pos.AddCopy(BEBehaviorDoor.getAdjacentOffset(1, 0, 0, MeshAngleRad, false));
                var be = Api.World.BlockAccessor.GetBlockEntity<BlockEntityScrollRack>(npos);
                var blockSelDown = blockSel.Clone();
                blockSelDown.SelectionBoxIndex = oppositeSlotIndex[blockSelDown.SelectionBoxIndex];
                return be?.OnInteract(byPlayer, blockSelDown) ?? false;
            }

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            CollectibleObject colObj = slot.Itemstack?.Collectible;
            bool shelvable = colObj?.Attributes != null && colObj.Attributes["scrollrackable"].AsBool(false) == true;

            if (slot.Empty || !shelvable)
            {
                if (TryTake(byPlayer, blockSel))
                {
                    return true;
                }
                return false;
            }
            else
            {
                if (shelvable)
                {
                    AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;

                    if (TryPut(slot, blockSel))
                    {
                        Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }



        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            int boxIndex = blockSel.SelectionBoxIndex;
            if (boxIndex < 0 || boxIndex >= inv.Count) return false;
            if (!UsableSlots.Contains(boxIndex)) return false;

            int invIndex = boxIndex;

            if (inv[invIndex].Empty)
            {
                int moved = slot.TryPutInto(Api.World, inv[invIndex]);
                updateMeshes();
                MarkDirty(true);
                return moved > 0;
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            int boxIndex = blockSel.SelectionBoxIndex;
            if (boxIndex < 0 || boxIndex >= inv.Count) return false;

            int invIndex = boxIndex;

            if (!inv[invIndex].Empty)
            {
                ItemStack stack = inv[invIndex].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    AssetLocation sound = stack.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                }

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                updateMeshes();
                MarkDirty(true);
                return true;
            }

            return false;
        }

        protected override float[][] genTransformationMatrices()
        {
            tfMatrices = new float[Inventory.Count][];
            var hitboxes = (Block as BlockScrollRack).slotsHitBoxes;

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

            tree.SetString("type", type);
            tree.SetString("material", material);
            tree.SetFloat("meshAngleRad", MeshAngleRad);
            tree.SetBool("usableSlotsDirty", UsableSlots == null);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            type = tree.GetString("type");
            material = tree.GetString("material");
            MeshAngleRad = tree.GetFloat("meshAngleRad");
            if (tree.GetBool("usableSlotsDirty")) UsableSlots = null;

            initShelf();
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mesher.AddMeshData(mesh, mat);
            base.OnTesselation(mesher, tessThreadTesselator);
            return true;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (forPlayer.CurrentBlockSelection == null)
            {
                base.GetBlockInfo(forPlayer, sb);
                return;
            }

            int index = forPlayer.CurrentBlockSelection.SelectionBoxIndex - 5;

            if (index < 0 || index >= inv.Count)
            {
                base.GetBlockInfo(forPlayer, sb);
                return;
            }

            ItemSlot slot = inv[index];
            if (slot.Empty)
            {
                sb.AppendLine(Lang.Get("Empty"));
            }
            else
            {
                sb.AppendLine(slot.Itemstack.GetName());
            }
        }

        public void OnTransformed(ITreeAttribute tree, int degreeRotation, EnumAxis? flipAxis)
        {
            MeshAngleRad = tree.GetFloat("meshAngleRad");
            MeshAngleRad -= degreeRotation * GameMath.DEG2RAD;
            tree.SetFloat("meshAngleRad", MeshAngleRad);
        }

        internal void clearUsableSlots()
        {
            UsableSlots = null;
            MarkDirty(true);
        }
    }
}
