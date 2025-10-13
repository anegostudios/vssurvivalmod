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
    public class BlockEntityBookshelf : BlockEntityDisplay
    {
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "bookshelf";
        public override string AttributeTransformCode => "onshelfTransform";

        public float MeshAngleRad
        {
            get => bh?.MeshAngleY ?? 0;
            set => bh!.MeshAngleY = value;
        }

        InventoryGeneric inv;
        Block block = null!;
        MeshData? mesh;

        float[]? mat;
        private BEBehaviorShapeMaterialFromAttributes? bh;

        public string? Type => bh?.Type;
        public string? Material => bh?.Material;

        public int[]? UsableSlots {
            get {
                if (block is not BlockBookshelf bs || Type == null) return System.Array.Empty<int>();
                bs.UsableSlots.TryGetValue(Type, out var slots);
                return slots ?? System.Array.Empty<int>();
            }
        }

        public BlockEntityBookshelf()
        {
            inv = new InventoryGeneric(14, "bookshelf-0", null, null);
        }

        void initShelf()
        {
            if (Api == null || Type == null || Block is not BlockBookshelf bookshelf) return;

            if (Api.Side == EnumAppSide.Client)
            {
                mesh = bookshelf.GetOrCreateMesh(Type, Material!);
                mat = Matrixf.Create().Translate(0.5f, 0.5f, 0.5f).RotateY(MeshAngleRad).Translate(-0.5f, -0.5f, -0.5f).Values;
            }

            if (!bookshelf.UsableSlots.ContainsKey(Type))
            {
                bh!.Type = bookshelf.UsableSlots.First().Key;
            }

            var usableslots = UsableSlots;
            for (int i = 0; i < Inventory.Count; i++)
            {
                if (!usableslots.Contains(i))
                {
                    Inventory[i].MaxSlotStackSize = 0;
                }
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            bh = GetBehavior<BEBehaviorShapeMaterialFromAttributes>();
            block = api.World.BlockAccessor.GetBlock(Pos);
            base.Initialize(api);

            if (mesh == null && Type != null)
            {
                initShelf();
            }
        }

        public override void OnBlockPlaced(ItemStack? byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            initShelf();
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            CollectibleObject? colObj = slot.Itemstack?.Collectible;
            bool shelvable = colObj?.Attributes != null && colObj.Attributes["bookshelveable"].AsBool();

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

                if (TryPut(slot, blockSel))
                {
                    Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                    var blockSelSelectionBoxIndex = blockSel.SelectionBoxIndex - 5;
                    Api.World.Logger.Audit("{0} Put 1x{1} into Bookshelf slotid {2} at {3}.",
                        byPlayer.PlayerName,
                        inv[blockSelSelectionBoxIndex].Itemstack.Collectible.Code,
                        blockSelSelectionBoxIndex,
                        Pos
                    );
                    return true;
                }
            }

            return false;
        }



        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex - 5;
            if (index < 0 || index >= inv.Count) return false;
            if (!UsableSlots.Contains(index)) return false;

            for (int i = 0; i < inv.Count; i++)
            {
                int slotnum = (index + i) % inv.Count;
                if (inv[slotnum].Empty)
                {
                    int moved = slot.TryPutInto(Api.World, inv[slotnum]);
                    MarkDirty();
                    return moved > 0;
                }
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex - 5;
            if (index < 0 || index >= inv.Count) return false;

            if (!inv[index].Empty)
            {
                ItemStack stack = inv[index].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    AssetLocation? sound = stack.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                }
                Api.World.Logger.Audit("{0} Took 1x{1} from Bookshelf slotid {2} at {3}.",
                    byPlayer.PlayerName,
                    stack.Collectible.Code,
                    index,
                    Pos
                );

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos);
                }

                MarkDirty();
                return true;
            }

            return false;
        }

        protected override float[][] genTransformationMatrices()
        {
            tfMatrices = new float[Inventory.Count][];

            for (int i = 0; i < Inventory.Count; i++)
            {
                float x = (i % 7) * 2f / 16f + 1 / 16f - 0.5f + 1/16f;
                float y = (i / 7) * 7.5f / 16f + 1/16f;
                float z = 6.5f / 16f - 0.5f - 2.5f/16f;

                Vec3f off = new Vec3f(x, y, z);
                off = new Matrixf().RotateY(MeshAngleRad).TransformVector(off.ToVec4f(0)).XYZ;

                tfMatrices[i] =
                    new Matrixf()
                    .Translate(off.X, off.Y, off.Z)
                    .Translate(0.5f, 0, 0.5f)
                    .RotateY(MeshAngleRad)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values
                ;

            }

            return tfMatrices;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            initShelf();

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
    }
}
