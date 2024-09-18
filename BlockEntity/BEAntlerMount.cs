using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{

    public class BlockEntityAntlerMount : BlockEntityDisplay, IRotatable
    {
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "antlermount";
        public override string AttributeTransformCode => "onAntlerMountTransform";
        public float MeshAngleRad { get; set; }

        InventoryGeneric inv;
        MeshData mesh;

        string type, material;
        float[] mat;

        public string Type => type;
        public string Material => material;


        public BlockEntityAntlerMount()
        {
            inv = new InventoryGeneric(1, "antlermount-0", null, null);
        }

        void init()
        {
            if (Api == null || !(Block is BlockAntlerMount)) return;
            if (type == null) type = "square";

            if (Api.Side == EnumAppSide.Client)
            {
                mesh = (Block as BlockAntlerMount).GetOrCreateMesh(type, material, "rot"+MeshAngleRad);
                mat = Matrixf.Create().Translate(0.5f, 0.5f, 0.5f).RotateY(MeshAngleRad).Translate(-0.5f, -0.5f, -0.5f).Values;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (mesh == null && type != null)
            {
                init();
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            type ??= byItemStack?.Attributes.GetString("type");
            material ??= byItemStack?.Attributes.GetString("material");

            init();
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            CollectibleObject colObj = slot.Itemstack?.Collectible;
            bool shelvable = colObj?.Attributes != null && colObj.Attributes["antlerMountable"].AsBool(false) == true;

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
            int invIndex = 0;

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
            int invIndex = 0;

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
                tfMatrices[i] =
                    new Matrixf()
                    .Translate(0.5f, 0, 0.5f)
                    .RotateY(MeshAngleRad - GameMath.PIHALF)
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
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            type = tree.GetString("type");
            material = tree.GetString("material");
            MeshAngleRad = tree.GetFloat("meshAngleRad");

            init();

            // Do this last!!!
            RedrawAfterReceivingTreeAttributes(worldForResolving);     // Redraw on client after we have completed receiving the update from server
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

            int index = 0;
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

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation,
            Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            MeshAngleRad = tree.GetFloat("meshAngleRad");
            MeshAngleRad -= degreeRotation * GameMath.DEG2RAD;
            tree.SetFloat("meshAngleRad", MeshAngleRad);
        }

    }
}
