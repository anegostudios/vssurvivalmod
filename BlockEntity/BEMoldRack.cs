using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityMoldRack : BlockEntityDisplay
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "moldrack";

        Block block;


        public BlockEntityMoldRack()
        {
            inv = new InventoryGeneric(5, "moldrack-0", null, null);
            meshes = new MeshData[5];
        }

        public override void Initialize(ICoreAPI api)
        {
            block = api.World.BlockAccessor.GetBlock(Pos);
            base.Initialize(api);
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            CollectibleObject colObj = slot.Itemstack?.Collectible;
            bool rackable = colObj?.Attributes != null && colObj.Attributes["moldrackable"].AsBool(false) == true;

            if (slot.Empty || !rackable)
            {
                if (TryTake(byPlayer, blockSel))
                {
                    return true;
                }
                return false;
            }
            else
            {
                if (rackable)
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
            int index = blockSel.SelectionBoxIndex;

            for (int i = 0; i < inv.Count; i++)
            {
                int slotnum = (index + i) % inv.Count;
                if (inv[slotnum].Empty)
                {
                    int moved = slot.TryPutInto(Api.World, inv[slotnum]);
                    MarkDirty(true);
                    return moved > 0;
                }
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex;

            if (!inv[index].Empty)
            {
                ItemStack stack = inv[index].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    AssetLocation sound = stack.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                }

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                MarkDirty(true);
                return true;
            }

            return false;
        }


        Matrixf mat = new Matrixf();

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mat.Identity();
            mat.RotateYDeg(block.Shape.rotateY);

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        protected override void updateMeshes()
        {
            mat.Identity();
            mat.RotateYDeg(block.Shape.rotateY);

            base.updateMeshes();
        }

        protected override MeshData genMesh(ItemStack stack, int index)
        {
            MeshData mesh;

            ICoreClientAPI capi = Api as ICoreClientAPI;
            if (stack.Class == EnumItemClass.Block)
            {
                mesh = capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();
            }
            else
            {
                nowTesselatingItem = stack.Item;
                nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
                capi.Tesselator.TesselateItem(stack.Item, out mesh, this);

                mesh.RenderPasses.Fill((short)EnumChunkRenderPass.BlendNoCull);
            }
        
            if (stack.Collectible.Attributes?["onmoldrackTransform"].Exists == true)
            {
                ModelTransform transform = stack.Collectible.Attributes?["onmoldrackTransform"].AsObject<ModelTransform>();
                transform.EnsureDefaultValues();

                transform.Rotation.X = 0;
                transform.Rotation.Y = block.Shape.rotateY;
                transform.Rotation.Z = 90;

                mesh.ModelTransform(transform);
            }

            //ModelTransform tf = new ModelTransform();
            //tf.EnsureDefaultValues();
            //mesh.ModelTransform(tf);

            float x = 3/16f + 3 / 16f * index - 1;
            float y = 0;
            float z = 0;

            Vec4f offset = mat.TransformVector(new Vec4f(x, y, z, 0));
            mesh.Translate(offset.XYZ);

            return mesh;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (forPlayer.CurrentBlockSelection == null)
            {
                base.GetBlockInfo(forPlayer, sb);
                return;
            }

            int index = forPlayer.CurrentBlockSelection.SelectionBoxIndex;
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
