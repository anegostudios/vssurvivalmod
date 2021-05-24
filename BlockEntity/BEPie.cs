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
    public enum EnumPiePartType
    {
        Crust, Filling, Topping
    }
    public class InPieProperties
    {
        /// <summary>
        /// If true, allows mixing of the same nutritionprops food category
        /// </summary>
        public bool AllowMixing = true;
        public EnumPiePartType PartType;
        public AssetLocation Texture;
    }

    // Idea:
    // BlockEntityPie is a single inventory BE that hold a pie item stack
    // that pie item stack is a container with always 6 slots:
    // [0] = crust
    // [1-4] = filling
    // [5] = topping (unused atm)
    // 
    // Eliminates the need to convert it to an itemstack once its placed in inventory
    public class BlockEntityPie : BlockEntityContainer
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "pie";

        MealMeshCache ms;
        MeshData mesh;
        ICoreClientAPI capi;

        public BlockEntityPie() : base()
        {
            inv = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ms = api.ModLoader.GetModSystem<MealMeshCache>();

            capi = api as ICoreClientAPI;

            loadMesh();
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
            if (inv[0].Empty) return null;

            int size = inv[0].Itemstack.Attributes.GetInt("size");
            MarkDirty(true);

            ItemStack stack = inv[0].Itemstack.Clone();
            stack.Attributes.SetInt("size", 1);

            if (size == 1)
            {
                inv[0].Itemstack = null;
                Api.World.BlockAccessor.SetBlock(0, Pos);
            }

            inv[0].Itemstack.Attributes.SetInt("size", size - 1);

            return stack;
        }

        public void OnPlaced(IPlayer byPlayer)
        {
            ItemStack doughStack = byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
            if (doughStack == null) return;

            inv[0].Itemstack = new ItemStack(Block);
            (inv[0].Itemstack.Block as BlockPie).SetContents(inv[0].Itemstack, new ItemStack[6] { doughStack, null, null, null, null, null });
            inv[0].Itemstack.Attributes.SetInt("pieSize", 4);
            

            loadMesh();
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (hotbarSlot?.Itemstack?.Collectible.Tool == EnumTool.Knife)
            {
                ItemStack slicestack = TakeSlice();
                if (!byPlayer.InventoryManager.TryGiveItemstack(slicestack))
                {
                    Api.World.SpawnItemEntity(slicestack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }

            // Filling rules:
            // 1. get inPieProperties
            // 2. any filing there yet? if not, all good
            // 3. Is full: Can't add more.
            // 3. If partially full, must
            //    a.) be of same foodcat
            //    b.) have props.AllowMixing set to true

            if (!hotbarSlot.Empty)
            {
                bool added = TryAddIngredientFrom(hotbarSlot, byPlayer);
                if (added)
                {
                    loadMesh();
                    MarkDirty(true);
                }
                return added;
            } else
            {
                var pieBlock = (inv[0].Itemstack.Block as BlockPie);
                ItemStack[] cStacks = pieBlock.GetContents(Api.World, inv[0].Itemstack);
                bool isfinished = cStacks[1] != null && cStacks[2] != null && cStacks[3] != null && cStacks[4] != null;

                if (!isfinished) return false;
                
                if (!byPlayer.InventoryManager.TryGiveItemstack(inv[0].Itemstack))
                {
                    Api.World.SpawnItemEntity(inv[0].Itemstack, Pos.ToVec3d().Add(0.5, 0.25, 0.5));
                }

                this.inv[0].Itemstack = null;
                Api.World.BlockAccessor.SetBlock(0, Pos);
            }

            return true;
        }

        private bool TryAddIngredientFrom(ItemSlot slot, IPlayer byPlayer = null)
        {
            var pieProps = slot.Itemstack.ItemAttributes?["inPieProperties"]?.AsObject<InPieProperties>(null, slot.Itemstack.Collectible.Code.Domain);
            if (pieProps == null)
            {
                if (byPlayer != null && capi != null) capi.TriggerIngameError(this, "notpieable", Lang.Get("This item can not be added to pies"));
                return false;
            }

            var pieBlock = (inv[0].Itemstack.Block as BlockPie);

            ItemStack[] cStacks = pieBlock.GetContents(Api.World, inv[0].Itemstack);

            bool isFull = cStacks[1] != null && cStacks[2] != null && cStacks[3] != null && cStacks[4] != null;
            bool hasFilling = cStacks[1] != null || cStacks[2] != null || cStacks[3] != null || cStacks[4] != null;

            if (isFull)
            {
                if (pieProps.PartType == EnumPiePartType.Crust)
                {
                    if (cStacks[5] == null)
                    {
                        cStacks[5] = slot.TakeOut(1);
                        pieBlock.SetContents(inv[0].Itemstack, cStacks);
                    } else
                    {
                        ItemStack stack = inv[0].Itemstack;
                        stack.Attributes.SetInt("topCrustType", (stack.Attributes.GetInt("topCrustType") + 1) % 3);
                    }
                    return true;
                }
                if (byPlayer != null && capi != null) capi.TriggerIngameError(this, "piefullfilling", Lang.Get("Can't add more filling - already completely filled pie"));
                return false;
            }

            if (pieProps.PartType != EnumPiePartType.Filling)
            {
                if (byPlayer != null && capi != null) capi.TriggerIngameError(this, "pieneedsfilling", Lang.Get("Need to add a filling next"));
                return false;
            }


            if (!hasFilling)
            {
                cStacks[1] = slot.TakeOut(1);
                pieBlock.SetContents(inv[0].Itemstack, cStacks);
                return true;
            }

            var foodCats = cStacks.Select(stack => stack?.Collectible.NutritionProps?.FoodCategory ?? stack?.ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory ?? EnumFoodCategory.Vegetable).ToArray();
            var stackprops = cStacks.Select(stack => stack?.ItemAttributes["inPieProperties"]?.AsObject<InPieProperties>(null, stack.Collectible.Code.Domain)).ToArray();

            ItemStack cstack = slot.Itemstack;
            EnumFoodCategory foodCat = slot.Itemstack?.Collectible.NutritionProps?.FoodCategory ?? slot.Itemstack?.ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory ?? EnumFoodCategory.Vegetable;

            bool equal = true;
            bool foodCatEquals = true;

            for (int i = 1; equal && i < cStacks.Length - 1; i++)
            {
                if (cstack == null) continue;

                equal &= cStacks[i] == null || cstack.Equals(Api.World, cStacks[i], GlobalConstants.IgnoredStackAttributes);
                foodCatEquals &= cStacks[i] == null || foodCats[i] == foodCat;

                cstack = cStacks[i];
                foodCat = foodCats[i];
            }

            int emptySlotIndex = 2 + (cStacks[2] != null ? 1 + (cStacks[3] != null ? 1 : 0) : 0);

            if (equal)
            {
                cStacks[emptySlotIndex] = slot.TakeOut(1);
                pieBlock.SetContents(inv[0].Itemstack, cStacks);
                return true;
            }

            if (!foodCatEquals)
            {
                if (byPlayer != null && capi != null) capi.TriggerIngameError(this, "piefullfilling", Lang.Get("Can't mix fillings from different food categories"));
                return false;
            } else
            {
                if (!stackprops[1].AllowMixing)
                {
                    if (byPlayer != null && capi != null) capi.TriggerIngameError(this, "piefullfilling", Lang.Get("You really want to mix these to ingredients?! That would taste horrible!"));
                    return false;
                }

                cStacks[emptySlotIndex] = slot.TakeOut(1);
                pieBlock.SetContents(inv[0].Itemstack, cStacks);
                return true;
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (inv[0].Empty) return true;
            mesher.AddMeshData(mesh);
            return true;
        }

        void loadMesh()
        {
            if (Api == null || Api.Side == EnumAppSide.Server || inv[0].Empty) return;
            mesh = ms.GetPieMesh(inv[0].Itemstack);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            dsc.Append(BlockEntityShelf.PerishableInfoCompact(Api, inv[0], 0, false));
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                MarkDirty(true);
                loadMesh();
            }
        }
    }
}
