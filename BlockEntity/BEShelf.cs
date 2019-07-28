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

namespace Vintagestory.GameContent
{
    public class BlockEntityShelf : BlockEntityContainer, IBlockShapeSupplier
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "shelf";

        Block block;

        public BlockEntityShelf()
        {
            inv = new InventoryGeneric(8, "shelf-0", null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            block = api.World.BlockAccessor.GetBlock(pos);
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty) {
                return TryTake(byPlayer, blockSel);
            } else
            {
                if (slot.Itemstack.Collectible is BlockCrock) return TryPut(slot, blockSel);
            }
            return false;
        }



        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            bool up = blockSel.SelectionBoxIndex > 0;

            for (int i = up ? 4 : 0; i < (up ? 8 : 4); i++)
            {
                if (inv[i].Empty)
                {
                    slot.TryPutInto(api.World, inv[i]);
                    MarkDirty(true);
                    return true;
                }
            }

            return true;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            bool up = blockSel.SelectionBoxIndex > 0;

            for (int i = up ? 7 : 3; i >= (up ? 4 : 0); i--)
            {
                if (!inv[i].Empty)
                {
                    inv[i].TryPutInto(api.World, byPlayer.InventoryManager.ActiveHotbarSlot);
                    MarkDirty(true);
                    return true;
                }
            }

            return false;
        }



        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;

            Matrixf mat = new Matrixf();
            mat.RotateYDeg(block.Shape.rotateY);

            for (int i = 0; i < 8; i++)
            {
                if (inv[i].Empty) continue;

                ItemStack stack = inv[i].Itemstack;
                BlockCrock crockblock = stack.Collectible as BlockCrock;
                Vec3f rot = new Vec3f(0, block.Shape.rotateY, 0);

                MeshData mesh = BlockEntityCrock.GetMesh(tessThreadTesselator, api, crockblock, crockblock.GetContents(api.World, stack), crockblock.GetRecipeCode(api.World, stack), rot);

                float y = i >= 4 ? 10 / 16f : 2 / 16f;
                float x = (i % 2 == 0) ? 4 / 16f : 12 / 16f;
                float z = ((i % 4) >= 2) ? 10 / 16f : 4 / 16f;

                Vec4f offset = mat.TransformVector(new Vec4f(x - 0.5f, y, z - 0.5f, 0));
                mesh.Translate(offset.XYZ);
                mesher.AddMeshData(mesh);
            }

            return false;
        }
    }
}
