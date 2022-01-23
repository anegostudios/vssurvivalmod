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
    public enum EnumCropStressType
    {
        Unknown = 0,
        TooHot = 1,
        TooCold = 2,
        Eaten = 3
    }

    public class BlockEntityDeadCrop : BlockEntityContainer
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

         

        public override string InventoryClassName => "deadcrop";

        public EnumCropStressType deathReason;


        public BlockEntityDeadCrop()
        {
            inv = new InventoryGeneric(1, "deadcrop-0", null, null);
        }


        public override void Initialize(ICoreAPI api)
        {
            // do not call `base.Initialize()` - we do not want the BEContainer.Initialize() code which does a room check and adds a contents ticker (BEDeadCrop contents is only ever a seed bag, doesn't need ticking)
            this.Api = api;
            foreach (var val in Behaviors)
            {
                val.Initialize(api, val.properties);
            }

            Inventory.LateInitialize(InventoryClassName + "-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);
            Inventory.Pos = Pos;
            Inventory.ResolveBlocksOrItems();
            Inventory.OnAcquireTransitionSpeed = Inventory_OnAcquireTransitionSpeed;
        }


        public ItemStack[] GetDrops(IPlayer byPlayer, float dropQuantityMultiplier)
        {
            if (inv[0].Empty) return new ItemStack[0];
            ItemStack[] drops = inv[0].Itemstack.Block.GetDrops(Api.World, Pos, byPlayer, dropQuantityMultiplier);

            // Minor hack to make dead crop always drop seeds
            var seedStack = drops.FirstOrDefault(stack => stack.Collectible is ItemPlantableSeed);
            if (seedStack == null) {
                seedStack = inv[0].Itemstack.Block.Drops.FirstOrDefault(bstack => bstack.ResolvedItemstack.Collectible is ItemPlantableSeed)?.ResolvedItemstack.Clone();
                if (seedStack != null)
                {
                    drops = drops.Append(seedStack);
                }
            }

            return drops;
        }

        public string GetPlacedBlockName()
        {
            if (inv[0].Empty) return Lang.Get("Dead crop");
            return Lang.Get("Dead {0}", inv[0].Itemstack.GetName());
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            deathReason = (EnumCropStressType)tree.GetInt("deathReason", 0);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("deathReason", (int)deathReason);
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            //base.OnBlockBroken(); - dont drop contents
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            switch (deathReason)
            {
                case EnumCropStressType.TooHot: dsc.AppendLine(Lang.Get("Died from too high temperatues.")); break;
                case EnumCropStressType.TooCold: dsc.AppendLine(Lang.Get("Died from too low temperatures.")); break;
                case EnumCropStressType.Eaten: dsc.AppendLine(Lang.Get("Eaten by wild animals.")); break;
            }

            if (!inv[0].Empty) dsc.Append(inv[0].Itemstack.Block.GetPlacedBlockInfo(Api.World, Pos, forPlayer));
        }

    }
}
 