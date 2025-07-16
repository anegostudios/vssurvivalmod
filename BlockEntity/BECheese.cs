﻿using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

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

        public int SlicesLeft
        {
            get
            {
                if (inv[0].Empty) return 0;
                ItemCheese cheese = inv[0].Itemstack.Collectible as ItemCheese;
                switch (cheese?.Part)
                {
                    case "1slice": return 1;
                    case "2slice": return 2;
                    case "3slice": return 3;
                    case "4slice": return 4;
                    default: return 0;
                }
            }
        }

        public ItemStack TakeSlice()
        {
            if (inv[0].Empty) return null;
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

            tessThreadTesselator.TesselateShape(Block, (Api as ICoreClientAPI).TesselatorManager.GetCachedShape(inv[0].Itemstack.Item.Shape.Base), out MeshData modeldata);
            modeldata.Scale(new Vec3f(0.5f, 0, 0.5f), 1f, 1f, 1f);
            mesher.AddMeshData(modeldata);
            return true;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            dsc.Append(BlockEntityShelf.PerishableInfoCompact(Api, inv[0], 0));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                MarkDirty(true);
            }
        }
    }
}
