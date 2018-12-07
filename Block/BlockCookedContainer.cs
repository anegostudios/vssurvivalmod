using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockCookedContainer : BlockContainer, IInFirepitRendererSupplier
    {
        public static SimpleParticleProperties smokeHeld;
        public static SimpleParticleProperties foodSparks;

        static BlockCookedContainer()
        {
            smokeHeld = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(50, 220, 220, 220),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.05f, 0.1f, -0.05f),
                new Vec3f(0.05f, 0.15f, 0.05f),
                1.5f,
                0,
                0.25f,
                0.35f,
                EnumParticleModel.Quad
            );
            smokeHeld.SelfPropelled = true;
            smokeHeld.addPos.Set(0.1, 0.1, 0.1);


            foodSparks = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(255, 83, 233, 255),
                new Vec3d(), new Vec3d(),
                new Vec3f(-3f, 1f, -3f),
                new Vec3f(3f, 8f, 3f),
                0.5f,
                1f,
                0.25f, 0.25f
            );
            foodSparks.glowLevel = 0;
        }




        MealMeshCache meshCache;
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (meshCache == null) meshCache = capi.ModLoader.GetModSystem<MealMeshCache>();

            CookingRecipe recipe = GetCookingRecipe(capi.World, itemstack);
            ItemStack[] contents = GetContents(capi.World, itemstack);

            float yoff = 2.5f; // itemstack.Attributes.GetInt("servings");

            MeshRef meshref = meshCache.GetOrCreateMealMeshRef(this.Shape, recipe, contents, new Vec3f(0, yoff/16f, 0));
            if (meshref != null) renderinfo.ModelRef = meshref;
        }
        


        public void SetContents(string recipeCode, int servings, ItemStack containerStack, ItemStack[] stacks)
        {
            base.SetContents(containerStack, stacks);

            containerStack.Attributes.SetInt("servings", servings);
            containerStack.Attributes.SetString("recipeCode", recipeCode);
        }


        public override void OnHeldIdle(IItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World.Side == EnumAppSide.Client && GetTemperature(byEntity.World, slot.Itemstack) > 50 && byEntity.World.Rand.NextDouble() < 0.07)
            {
                float sideWays = 0.35f;
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;

                if (world.Player.Entity == byEntity && world.Player.CameraMode != EnumCameraMode.FirstPerson)
                {
                    sideWays = 0f;
                }

                Vec3d pos =
                    byEntity.Pos.XYZ.Add(0, byEntity.EyeHeight - 0.5f, 0)
                    .Ahead(0.33f, byEntity.Pos.Pitch, byEntity.Pos.Yaw)
                    .Ahead(sideWays, 0, byEntity.Pos.Yaw + GameMath.PIHALF)
                ;

                smokeHeld.minPos = pos.AddCopy(-0.05, 0.1, -0.05);
                byEntity.World.SpawnParticles(smokeHeld);
            }
        }



        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos);

            BlockEntityCookedContainer bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCookedContainer;

            if (bec != null)
            {
                ItemStack[] contentStacks = bec.GetContentStacks();
                SetContents(bec.RecipeCode, bec.QuantityServings, stack, contentStacks);
                float temp = contentStacks.Length > 0 ? contentStacks[0].Collectible.GetTemperature(world, contentStacks[0]) : 0;
                SetTemperature(world, stack, temp, false);
            }

            return stack;
        }



        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("cookedMeshRefs", out obj))
            {
                Dictionary<int, MeshRef> meshrefs = obj as Dictionary<int, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("cookedMeshRefs");
            }
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return new ItemStack[] { OnPickBlock(world, pos) };
        }


        public CookingRecipe GetCookingRecipe(IWorldAccessor world, ItemStack containerStack)
        {
            return world.CookingRecipes.FirstOrDefault(rec => rec.Code == GetRecipeCode(world, containerStack));
        }

        public string GetRecipeCode(IWorldAccessor world, ItemStack containerStack)
        {
            return containerStack.Attributes.GetString("recipeCode");
        }

        internal int GetServings(IWorldAccessor world, ItemStack byItemStack)
        {
            return byItemStack.Attributes.GetInt("servings");
        }

        internal void SetServings(IWorldAccessor world, ItemStack byItemStack, int value)
        {
            byItemStack.Attributes.SetInt("servings", value);
        }

        public CookingRecipe GetMealRecipe(IWorldAccessor world, ItemStack containerStack)
        {
            string recipecode = GetRecipeCode(world, containerStack);
            return world.CookingRecipes.FirstOrDefault((rec) => recipecode == rec.Code);
        }


        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            CookingRecipe recipe = GetMealRecipe(world, stack);

            int servings = stack.Attributes.GetInt("servings");

            if (recipe != null) {
                if (servings == 1) {
                    dsc.AppendLine(Lang.Get("{0} serving of {1}", servings, recipe.GetOutputName(world, GetContents(world, stack))));
                } else
                {
                    dsc.AppendLine(Lang.Get("{0} servings of {1}", servings, recipe.GetOutputName(world, GetContents(world, stack))));
                }
            }

            BlockMeal mealblock = api.World.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;
            string nutriFacts = mealblock.GetContentNutritionFacts(api.World, GetContents(world, stack), null);

            if (nutriFacts != null) dsc.AppendLine(nutriFacts);

        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Code.Path == "bowl-burned")
            {
                BlockEntityCookedContainer bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCookedContainer;
                if (bec == null) return false;

                bec.ServePlayer(byPlayer);
                return true;
            }

        
            ItemStack stack = OnPickBlock(world, blockSel.Position);

            if (byPlayer.InventoryManager.TryGiveItemstack(stack, true))
            {
                world.BlockAccessor.SetBlock(0, blockSel.Position);
                world.PlaySoundAt(this.Sounds.Place, byPlayer, byPlayer);
                return true;
            }


            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }



        public override void OnHeldInteractStart(IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling)
        {
            if (blockSel != null)
            {
                BlockBowl block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position) as BlockBowl;
                if (block != null)
                {
                    ServeIntoBowl(blockSel.Position, slot, byEntity.World);
                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }
            }
            
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, ref handHandling);
        }



        public void ServeIntoBowl(BlockPos pos, IItemSlot potslot, IWorldAccessor world)
        {
            if (world.Side == EnumAppSide.Client) return;

            Block mealblock = api.World.GetBlock(new AssetLocation("bowl-meal"));

            world.BlockAccessor.SetBlock(mealblock.BlockId, pos);

            BlockEntityMeal bemeal = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMeal;
            if (bemeal == null) return;

            bemeal.RecipeCode = GetRecipeCode(world, potslot.Itemstack);

            ItemStack[] stacks = GetContents(api.World, potslot.Itemstack);
            for (int i = 0; i < stacks.Length; i++)
            {
                bemeal.inventory[i].Itemstack = stacks[i].Clone();
            }

            int quantityServings = GetServings(world, potslot.Itemstack);

            SetServings(world, potslot.Itemstack, quantityServings - 1);

            if (quantityServings <= 0)
            {
                potslot.Itemstack = new ItemStack(api.World.GetBlock(new AssetLocation(FirstCodePart() + "-burned")));
            }

            potslot.MarkDirty();
            bemeal.MarkDirty(true);
        }



        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            BlockEntityCookedContainer bem = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCookedContainer;
            if (bem == null) return base.GetRandomColor(capi, pos, facing);

            ItemStack[] stacks = bem.GetContentStacks();
            ItemStack rndStack = stacks[capi.World.Rand.Next(stacks.Length)];

            if (capi.World.Rand.NextDouble() < 0.4)
            {
                return capi.BlockTextureAtlas.GetRandomPixel(Textures["ceramic"].Baked.TextureSubId);
            }

            if (rndStack.Class == EnumItemClass.Block)
            {
                return rndStack.Block.GetRandomColor(capi, pos, facing);
            }
            else
            {
                return capi.ItemTextureAtlas.GetRandomPixel(rndStack.Item.FirstTexture.Baked.TextureSubId);
            }
        }


        public override int GetRandomColor(ICoreClientAPI capi, ItemStack stack)
        {
            ItemStack[] stacks = GetContents(capi.World, stack);
            ItemStack rndStack = stacks[capi.World.Rand.Next(stacks.Length)];

            if (capi.World.Rand.NextDouble() < 0.4)
            {
                return capi.BlockTextureAtlas.GetRandomPixel(Textures["ceramic"].Baked.TextureSubId);
            }

            return rndStack.Collectible.GetRandomColor(capi, stack);
        }

        public IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return new PotInFirepitRenderer(api as ICoreClientAPI, stack, firepit.pos, forOutputSlot);
        }

        public EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return EnumFirepitModel.Wide;
        }
    }
}
