using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumTopCrustType
    {
        Full, Square, Diagonal
    }

    // Definition: GetContents() must always return a ItemStack[] of array length 6
    // [0] = crust
    // [1-4] = filling
    // [5] = topping (unused atm)
    public class BlockPie : BlockMeal
    {
        public string State => Variant["state"];

        protected override bool PlacedBlockEating => false;

        MealMeshCache ms;


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (!canEat(slot)) return;
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (!canEat(slot)) return false;

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (!canEat(slot)) return;

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }


        protected bool canEat(ItemSlot slot) {
            return
                slot.Itemstack.Attributes.GetInt("size") == 1
                && State != "raw"
            ;
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            ms = api.ModLoader.GetModSystem<MealMeshCache>();

            /*foreach (var val in api.World.Collectibles)
            {
                if (val.Attributes?["inPieProperties"].Exists == true)
                {
                    var pieprops = val.Attributes["inPieProperties"]?.AsObject<InPieProperties>();
                    if (pieprops.PartType == EnumPiePartType.Filling)
                    {
                        Console.WriteLine(string.Format("\"pie-single-{0}\": \"{1} pie\",", val.Code.Path, new ItemStack(val).GetName()));
                    }
                }
            }*/
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            renderinfo.ModelRef = ms.GetOrCreatePieMeshRef(itemstack);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityPie bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie;
            if (bec != null) return bec.Inventory[0].Itemstack.Clone();

            return base.OnPickBlock(world, pos);
        }



        public void TryPlacePie(EntityAgent byEntity, BlockSelection blockSel)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            var pieprops = hotbarSlot.Itemstack.ItemAttributes["inPieProperties"]?.AsObject<InPieProperties>();
            if (pieprops == null || pieprops.PartType != EnumPiePartType.Crust) return;

            BlockPos abovePos = blockSel.Position.UpCopy();

            Block atBlock = api.World.BlockAccessor.GetBlock(abovePos);
            if (atBlock.Replaceable < 6000) return;

            api.World.BlockAccessor.SetBlock(Id, abovePos);

            BlockEntityPie bepie = api.World.BlockAccessor.GetBlockEntity(abovePos) as BlockEntityPie;
            bepie.OnPlaced(byPlayer);
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityPie bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie;
            if (bec != null) return GetHeldItemName(bec.Inventory[0].Itemstack);

            return base.GetPlacedBlockName(world, pos);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            ItemStack[] cStacks = GetContents(api.World, itemStack);
            if (cStacks.Length == 0) return Lang.Get("pie-empty");

            ItemStack cstack = cStacks[1];

            if (cstack == null) return Lang.Get("pie-empty");

            bool equal = true;
            for (int i = 2; equal && i < cStacks.Length - 1; i++)
            {
                if (cStacks[i] == null) continue;

                equal &= cstack.Equals(api.World, cStacks[i], GlobalConstants.IgnoredStackAttributes);
                cstack = cStacks[i];
            }

            if (equal)
            {
                return Lang.Get("pie-single-" + cstack.Collectible.Code.ToShortString());
            } else
            {
                EnumFoodCategory fillingFoodCat =
                    cStacks[1].Collectible.NutritionProps?.FoodCategory
                    ?? cStacks[1].ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory
                    ?? EnumFoodCategory.Vegetable
                ;

                return Lang.Get("pie-mixed-" + fillingFoodCat.ToString().ToLowerInvariant());
            }           
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            ItemStack[] stacks = GetContents(api.World, inSlot.Itemstack);
            BlockMeal mealblock = api.World.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;
            string nutriFacts = mealblock.GetContentNutritionFacts(api.World, inSlot, stacks, null);

            if (nutriFacts != null) dsc.AppendLine(nutriFacts);

            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockEntityPie bep = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie;
            if (bep == null) return "";
            BlockMeal mealblock = api.World.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;
            ItemStack[] stacks = GetContents(api.World, bep.Inventory[0].Itemstack);
            return mealblock.GetContentNutritionFacts(api.World, bep.Inventory[0], stacks, null);

        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityPie bep = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPie;

            return bep.OnInteract(byPlayer);
        }
    }
}

