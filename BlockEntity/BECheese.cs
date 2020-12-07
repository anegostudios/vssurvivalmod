using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BECheese : BlockEntityContainer
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "cheese";

        public BECheese()
        {
            inv = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inv.LateInitialize("cheese-" + Pos, api);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack != null)
            {
                inv[0].Itemstack = byItemStack.Clone();
                inv[0].Itemstack.StackSize = 1;
            }
        }

        public ItemStack TakeSlice()
        {
            ItemCheese cheese = inv[0].Itemstack.Collectible as ItemCheese;
            MarkDirty(true);

            switch (cheese.Part)
            {
                case "1slice":
                    {
                        ItemStack stack = inv[0].Itemstack.Clone();
                        inv[0].Itemstack = null;
                        Api.World.BlockAccessor.SetBlock(0, Pos);            
                        return stack;
                    }
                case "2slice":
                    {
                        ItemStack stack = new ItemStack(Api.World.GetItem(cheese.CodeWithVariant("part", "1slice")));
                        inv[0].Itemstack = stack;
                        return stack.Clone();
                    }
                case "3slice":
                    {
                        ItemStack stack = new ItemStack(Api.World.GetItem(cheese.CodeWithVariant("part", "1slice")));
                        inv[0].Itemstack = new ItemStack(Api.World.GetItem(cheese.CodeWithVariant("part", "2slice")));
                        return stack.Clone();
                    }
                case "4slice":
                    {
                        ItemStack stack = new ItemStack(Api.World.GetItem(cheese.CodeWithVariant("part", "1slice")));
                        inv[0].Itemstack = new ItemStack(Api.World.GetItem(cheese.CodeWithVariant("part", "3slice"))); ;
                        return stack.Clone();
                    }
            }

            return null;
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (inv[0].Empty) return true;

            MeshData modeldata;
            tessThreadTesselator.TesselateShape(Block, (Api as ICoreClientAPI).TesselatorManager.GetCachedShape(inv[0].Itemstack.Item.Shape.Base), out modeldata);
            modeldata.Scale(new Vec3f(0.5f, 0, 0.5f), 0.75f, 0.75f, 0.75f);
            mesher.AddMeshData(modeldata);
            return true;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            dsc.Append(BlockEntityShelf.PerishableInfoCompact(Api, inv[0], 0));
        }
    }
}
