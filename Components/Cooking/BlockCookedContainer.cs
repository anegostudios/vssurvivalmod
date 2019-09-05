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
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockCookedContainer : BlockContainer, IInFirepitRendererSupplier
    {
        public static SimpleParticleProperties smokeHeld;
        public static SimpleParticleProperties foodSparks;

        WorldInteraction[] interactions;


        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "cookedContainerBlockInteractions", () =>
            {
                List<ItemStack> fillableStacklist = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Attributes?["mealContainer"].AsBool() == true)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) fillableStacklist.AddRange(stacks);
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-cookedcontainer-takefood",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = fillableStacklist.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-cookedcontainer-pickup",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right
                    }
                };
            });
        }

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
            ItemStack[] contents = GetNonEmptyContents(capi.World, itemstack);

            float yoff = 2.5f;

            MeshRef meshref = meshCache.GetOrCreateMealMeshRef(this, this.Shape, recipe, contents, new Vec3f(0, yoff/16f, 0));
            if (meshref != null) renderinfo.ModelRef = meshref;
        }
        


        public void SetContents(string recipeCode, float servings, ItemStack containerStack, ItemStack[] stacks)
        {
            base.SetContents(containerStack, stacks);

            containerStack.Attributes.SetFloat("quantityServings", servings);
            containerStack.Attributes.SetString("recipeCode", recipeCode);
        }


        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
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


        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);

            IWorldAccessor world = entityItem.World;
            if (world.Side != EnumAppSide.Server) return;

            if (entityItem.Swimming && world.Rand.NextDouble() < 0.01)
            {
                ItemStack[] stacks = GetContents(world, entityItem.Itemstack);
                if (MealMeshCache.ContentsRotten(stacks))
                {
                    for (int i = 0; i < stacks.Length; i++)
                    {
                        if (stacks[i] != null && stacks[i].StackSize > 0 && stacks[i].Collectible.Code.Path == "rot") {
                            world.SpawnItemEntity(stacks[i], entityItem.ServerPos.XYZ);
                        }
                    }

                    Block emptyPotBlock = world.GetBlock(new AssetLocation(Attributes["emptiedBlockCode"].AsString()));
                    entityItem.Itemstack = new ItemStack(emptyPotBlock);
                    entityItem.WatchedAttributes.MarkPathDirty("itemstack");
                }
            }
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos);

            BlockEntityCookedContainer bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCookedContainer;

            if (bec != null)
            {
                ItemStack[] contentStacks = bec.GetNonEmptyContentStacks();
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

        internal float GetServings(IWorldAccessor world, ItemStack byItemStack)
        {
            return (float)byItemStack.Attributes.GetDecimal("quantityServings");
        }

        internal void SetServings(IWorldAccessor world, ItemStack byItemStack, float value)
        {
            byItemStack.Attributes.SetFloat("quantityServings", value);
        }

        public CookingRecipe GetMealRecipe(IWorldAccessor world, ItemStack containerStack)
        {
            string recipecode = GetRecipeCode(world, containerStack);
            return world.CookingRecipes.FirstOrDefault((rec) => recipecode == rec.Code);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            CookingRecipe recipe = GetMealRecipe(world, inSlot.Itemstack);
            float servings = inSlot.Itemstack.Attributes.GetFloat("quantityServings");

            ItemStack[] stacks = GetContents(world, inSlot.Itemstack);


            if (recipe != null) {
                if (servings == 1) {
                    dsc.AppendLine(Lang.Get("{0} serving of {1}", Math.Round(servings, 1), recipe.GetOutputName(world, stacks)));
                } else
                {
                    dsc.AppendLine(Lang.Get("{0} servings of {1}", Math.Round(servings, 1), recipe.GetOutputName(world, stacks)));
                }
            }

            BlockMeal mealblock = api.World.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;
            string nutriFacts = mealblock.GetContentNutritionFacts(api.World, inSlot, stacks, null);

            if (nutriFacts != null) dsc.AppendLine(nutriFacts);

            ItemSlot slot = BlockCrock.GetDummySlotForFirstPerishableStack(api.World, stacks, null, inSlot.Inventory);
            slot.Itemstack.Collectible.AppendPerishableInfoText(slot, dsc, world);
        }

        


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?["mealContainer"].AsBool() == true)
            {
                BlockEntityCookedContainer bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCookedContainer;
                if (bec == null) return false;

                return bec.ServeInto(byPlayer, hotbarSlot);
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




        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel != null)
            {
                Block selectedBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                if (selectedBlock?.Attributes?["mealcontainer"]?.AsBool() == true)
                {
                    ServeIntoBowl(selectedBlock, blockSel.Position, slot, byEntity.World);
                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }
            }
            
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }



        public void ServeIntoBowl(Block selectedBlock, BlockPos pos, ItemSlot potslot, IWorldAccessor world)
        {
            if (world.Side == EnumAppSide.Client) return;

            string code = selectedBlock.Attributes["mealBlockCode"].AsString();
            Block mealblock = api.World.GetBlock(new AssetLocation(code));

            world.BlockAccessor.SetBlock(mealblock.BlockId, pos);

            IBlockEntityMealContainer bemeal = api.World.BlockAccessor.GetBlockEntity(pos) as IBlockEntityMealContainer;
            if (bemeal == null) return;

            bemeal.RecipeCode = GetRecipeCode(world, potslot.Itemstack);

            ItemStack[] stacks = GetContents(api.World, potslot.Itemstack);
            for (int i = 0; i < stacks.Length; i++)
            {
                bemeal.inventory[i].Itemstack = stacks[i].Clone();
            }



            float quantityServings = GetServings(world, potslot.Itemstack);
            float servingsToTransfer = Math.Min(quantityServings, selectedBlock.Attributes["servingCapacity"].AsFloat(1));

            bemeal.QuantityServings = servingsToTransfer;


            SetServings(world, potslot.Itemstack, quantityServings - servingsToTransfer);

            if (quantityServings <= 0)
            {
                Block emptyPotBlock = world.GetBlock(new AssetLocation(Attributes["emptiedBlockCode"].AsString()));
                potslot.Itemstack = new ItemStack(emptyPotBlock);
            }

            potslot.MarkDirty();
            bemeal.MarkDirty(true);
        }



        public void ServeIntoBowlStack(ItemSlot bowlSlot, ItemSlot potslot, IWorldAccessor world)
        {
            if (world.Side == EnumAppSide.Client) return;

            string code = bowlSlot.Itemstack.Block.Attributes["mealBlockCode"].AsString();
            Block mealblock = api.World.GetBlock(new AssetLocation(code)) as Block;

            ItemStack[] stacks = GetContents(api.World, potslot.Itemstack);

            float quantityServings = GetServings(world, potslot.Itemstack);
            float servingsToTransfer = Math.Min(quantityServings, bowlSlot.Itemstack.Block.Attributes["servingCapacity"].AsFloat(1));

            ItemStack stack = new ItemStack(mealblock);
            (mealblock as IBlockMealContainer).SetContents(GetRecipeCode(world, potslot.Itemstack), stack, stacks, servingsToTransfer);
            
            SetServings(world, potslot.Itemstack, quantityServings - servingsToTransfer);

            if (quantityServings - servingsToTransfer <= 0)
            {
                Block emptyPotBlock = world.GetBlock(new AssetLocation(Attributes["emptiedBlockCode"].AsString()));
                potslot.Itemstack = new ItemStack(emptyPotBlock);
            }

            potslot.MarkDirty();

            bowlSlot.Itemstack = stack;
            bowlSlot.MarkDirty();
        }






        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            BlockEntityCookedContainer bem = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCookedContainer;
            if (bem == null) return base.GetRandomColor(capi, pos, facing);

            ItemStack[] stacks = bem.GetNonEmptyContentStacks();
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
            ItemStack[] stacks = GetNonEmptyContents(capi.World, stack);
            if (stacks.Length == 0) return base.GetRandomColor(capi, stack);

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


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
