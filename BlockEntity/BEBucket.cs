using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class BlockEntityBucket : BlockEntityContainer, IBlockShapeSupplier
    {
        internal InventoryGeneric inventory;
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "bucket";

        MeshData currentMesh;
        BlockBucket ownBlock;

        public BlockEntityBucket()
        {
            inventory = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = api.World.BlockAccessor.GetBlock(pos) as BlockBucket;

            if (api.Side == EnumAppSide.Client && currentMesh == null)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }


        public override void OnBlockBroken()
        {
            // Don't drop inventory contents
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public ItemStack GetContent()
        {
            return inventory.GetSlot(0).Itemstack;
        }


        internal void SetContent(ItemStack stack)
        {
            inventory.GetSlot(0).Itemstack = stack;
            MarkDirty(true);
        }
        


        internal MeshData GenMesh()
        {
            if (ownBlock == null) return null;
            
            return ownBlock.GenMesh(api as ICoreClientAPI, GetContent(), pos);
        }

        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh);
            return true;
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            if (api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            ItemSlot slot = inventory.GetSlot(0);

            if (slot.Empty) return Lang.Get("Empty");

            return Lang.Get("Contents: {0}x{1}", slot.Itemstack.StackSize, slot.Itemstack.GetName());
        }

    }
}
