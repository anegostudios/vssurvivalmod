using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class BlockCookedContainer : BlockCookedContainerBase, IInFirepitRendererSupplier, IContainedMeshSource, IContainedInteractable, IGroundStoredParticleEmitter
    {
        public static SimpleParticleProperties smokeHeld;
        public static SimpleParticleProperties foodSparks;

        Vec3d gsSmokePos = new Vec3d(0.5, 0.3125, 0.5);

        WorldInteraction[]? interactions;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (CollisionBoxes[0] != null) gsSmokePos.Y = CollisionBoxes[0].MaxY;

            if (api is not ICoreClientAPI capi) return;

            meshCache = api.ModLoader.GetModSystem<MealMeshCache>();

            interactions = ObjectCacheUtil.GetOrCreate(api, "cookedContainerBlockInteractions", () =>
            {
                List<ItemStack> fillableStacklist = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Attributes?.IsTrue("mealContainer") == true)
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
            smokeHeld.AddPos.Set(0.1, 0.1, 0.1);


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
            foodSparks.VertexFlags = 0;
        }




        MealMeshCache? meshCache;
        public float yoff = 2.5f;

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (meshCache == null) meshCache = capi.ModLoader.GetModSystem<MealMeshCache>();

            CookingRecipe? recipe = GetCookingRecipe(capi.World, itemstack);
            ItemStack[] contents = GetNonEmptyContents(capi.World, itemstack);

            MultiTextureMeshRef? meshref = meshCache.GetOrCreateMealInContainerMeshRef(this, recipe, contents, new Vec3f(0, yoff/16f, 0));
            if (meshref != null) renderinfo.ModelRef = meshref;
        }


        public virtual string GetMeshCacheKey(ItemStack itemstack)
        {
            return meshCache!.GetMealHashCode(itemstack).ToString();
        }


        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos? forBlockPos = null)
        {
            return meshCache!.GenMealInContainerMesh(this, GetCookingRecipe(api.World, itemstack), GetNonEmptyContents(api.World, itemstack), new Vec3f(0, yoff / 16f, 0));
        }

        public override TransitionState[]? UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            if (inslot.Itemstack is not ItemStack cookedContStack) return null;

            ItemStack[] stacks = GetNonEmptyContents(world, cookedContStack);
            foreach (var stack in stacks) stack.StackSize *= (int)Math.Max(1, cookedContStack.Attributes.TryGetFloat("quantityServings") ?? 1);
            SetContents(cookedContStack, stacks);

            TransitionState[]? states = base.UpdateAndGetTransitionStates(world, inslot);

            stacks = GetNonEmptyContents(world, cookedContStack);
            if (stacks.Length == 0 || MealMeshCache.ContentsRotten(stacks))
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    var transProps = stacks[i].Collectible.GetTransitionableProperties(world, stacks[i], null);
                    var spoilProps = transProps?.FirstOrDefault(props => props.Type == EnumTransitionType.Perish);
                    if (spoilProps == null) continue;
                    stacks[i] = stacks[i].Collectible.OnTransitionNow(GetContentInDummySlot(inslot, stacks[i]), spoilProps);
                }
                SetContents(cookedContStack, stacks);

                cookedContStack.Attributes.RemoveAttribute("recipeCode");
                cookedContStack.Attributes.RemoveAttribute("quantityServings");
            }

            foreach (var stack in stacks) stack.StackSize /= (int)Math.Max(1, cookedContStack.Attributes.TryGetFloat("quantityServings") ?? 1);
            SetContents(cookedContStack, stacks);

            if (stacks.Length == 0 && Attributes?["emptiedBlockCode"]?.AsString() is string emptiedBlockCode && world.GetBlock(new AssetLocation(emptiedBlockCode)) is Block block)
            {
                inslot.Itemstack = new ItemStack(block);
                inslot.MarkDirty();
            }

            return states;
        }




        public override string GetHeldItemName(ItemStack? itemStack)
        {
            ItemStack[] contentStacks = GetContents(api.World, itemStack);
            string? recipeCode = itemStack?.Collectible.GetCollectibleInterface<IBlockMealContainer>()?.GetRecipeCode(api.World, itemStack);
            string code = Lang.Get("mealrecipe-name-" + recipeCode + "-in-container");

            if (recipeCode == null)
            {
                if (MealMeshCache.ContentsRotten(contentStacks))
                {
                    code = Lang.Get("Rotten Food");
                }
                else if (contentStacks.Length > 0)
                {
                    code = contentStacks[0].GetName();
                }
            }

            return Lang.GetMatching(Code?.Domain + AssetLocation.LocationSeparator + ItemClass.Name() + "-" + Code?.Path, code);
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World.Side == EnumAppSide.Client && GetTemperature(byEntity.World, slot.Itemstack) > 50 && byEntity.World.Rand.NextDouble() < 0.07)
            {
                float sideWays = 0.35f;
                

                if ((byEntity as EntityPlayer)?.Player is IClientPlayer byPlayer && byPlayer.CameraMode != EnumCameraMode.FirstPerson)
                {
                    sideWays = 0f;
                }

                Vec3d pos =
                    byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y - 0.5f, 0)
                    .Ahead(0.33f, byEntity.Pos.Pitch, byEntity.Pos.Yaw)
                    .Ahead(sideWays, 0, byEntity.Pos.Yaw + GameMath.PIHALF)
                ;

                smokeHeld.MinPos = pos.AddCopy(-0.05, 0.1, -0.05);
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
                        if (stacks[i] != null && stacks[i].StackSize > 0 && stacks[i].Collectible.Code.Path == "rot")
                        {
                            world.SpawnItemEntity(stacks[i], entityItem.ServerPos.XYZ);
                        }
                    }
                } else
                {
                    ItemStack rndStack = stacks[world.Rand.Next(stacks.Length)];
                    world.SpawnCubeParticles(entityItem.ServerPos.XYZ, rndStack, 0.3f, 25, 1, null);
                }

                Block emptyPotBlock = world.GetBlock(new AssetLocation(Attributes["emptiedBlockCode"].AsString()));
                entityItem.Itemstack = new ItemStack(emptyPotBlock);
                entityItem.WatchedAttributes.MarkPathDirty("itemstack");
            }
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos);

            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityCookedContainer bec)
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
            if (api is not ICoreClientAPI capi) return;

            if (capi.ObjectCache.TryGetValue("cookedMeshRefs", out object? obj))
            {
                if (obj is Dictionary<int, MultiTextureMeshRef> meshrefs)
                {
                    foreach (var val in meshrefs)
                    {
                        val.Value.Dispose();
                    }
                }

                capi.ObjectCache.Remove("cookedMeshRefs");
            }
        }



        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return [OnPickBlock(world, pos)];
        }




        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot.Itemstack is not ItemStack cookedContStack) return;
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            float temp = GetTemperature(world, cookedContStack);
            if (temp > 20)
            {
                dsc.AppendLine(Lang.Get("Temperature: {0}°C", (int)temp));
            }

            CookingRecipe? recipe = GetMealRecipe(world, cookedContStack);
            float servings = cookedContStack.Attributes.GetFloat("quantityServings");

            ItemStack[] stacks = GetNonEmptyContents(world, cookedContStack);


            if (recipe != null) {
                string message;
                string outputName = recipe.GetOutputName(world, stacks);
                if (recipe.CooksInto != null)
                {
                    message = "nonfood-portions";
                }
                else
                {
                    message = "{0} servings of {1}";
                }
                dsc.AppendLine(Lang.Get(message, Math.Round(servings, 1), outputName));
            }

            string? nutriFacts = BlockMeal.AllMealBowls?[0]?.GetContentNutritionFacts(api.World, inSlot, stacks, null);

            if (nutriFacts != null && recipe?.CooksInto == null) dsc.AppendLine(nutriFacts);

            if (cookedContStack.Attributes.GetBool("timeFrozen")) return;

            DummyInventory dummyInv = new DummyInventory(api);

            ItemSlot slot = BlockCrock.GetDummySlotForFirstPerishableStack(api.World, stacks, null, dummyInv);
            dummyInv.OnAcquireTransitionSpeed += (transType, stack, mul) =>
            {
                float val = mul * GetContainingTransitionModifierContained(world, inSlot, transType);

                if (inSlot.Inventory != null) val *= inSlot.Inventory.GetTransitionSpeedMul(transType, cookedContStack);

                return val;
            };
            slot.Itemstack?.Collectible.AppendPerishableInfoText(slot, dsc, world);
        }

        


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?.IsTrue("mealContainer") == true)
            {
                return (world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCookedContainer)?.ServeInto(byPlayer, hotbarSlot) ?? false;
            }



            ItemStack stack = OnPickBlock(world, blockSel.Position);

            if (byPlayer.InventoryManager.TryGiveItemstack(stack, true))
            {
                world.BlockAccessor.SetBlock(0, blockSel.Position);
                world.PlaySoundAt(Sounds.Place, byPlayer, byPlayer);
                return true;
            }


            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }




        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel != null)
            {
                BlockPos onBlockPos = blockSel.Position;
                Block block = byEntity.World.BlockAccessor.GetBlock(onBlockPos);

                if (block is BlockClayOven)
                {
                    // Prevent placing cake when trying add it to the oven
                    return;
                }

                Block selectedBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                if (selectedBlock?.Attributes?.IsTrue("mealContainer") == true)
                {
                    if (!byEntity.Controls.ShiftKey) return;
                    ServeIntoBowl(selectedBlock, blockSel.Position, slot, byEntity.World);
                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }


                float quantityServings = (float)(slot.Itemstack?.Attributes.GetDecimal("quantityServings") ?? 0);
                if (block is BlockGroundStorage)
                {
                    if (!byEntity.Controls.ShiftKey) return;
                    if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityGroundStorage begs || begs.GetSlotAt(blockSel) is not ItemSlot gsslot || gsslot.Empty)
                    {
                        return;
                    }

                    if (gsslot.Itemstack.ItemAttributes?.IsTrue("mealContainer") == true)
                    {
                        if (quantityServings > 0)
                        {
                            ServeIntoStack(gsslot, slot, byEntity.World);
                            gsslot.MarkDirty();
                            begs.updateMeshes();
                            begs.MarkDirty(true);
                        }

                        handHandling = EnumHandHandling.PreventDefault;
                        return;
                    }
                }


            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }






        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            if (capi.World.BlockAccessor.GetBlockEntity(pos) is not BlockEntityCookedContainer bem || bem.GetNonEmptyContentStacks() is not ItemStack[] stacks || stacks.Length == 0)
            { 
                return base.GetRandomColor(capi, pos, facing, rndIndex);
            }

            ItemStack rndStack = stacks[capi.World.Rand.Next(stacks.Length)];

            if (capi.World.Rand.NextDouble() < 0.4)
            {
                return capi.BlockTextureAtlas.GetRandomColor(Textures["ceramic"].Baked.TextureSubId);
            }

            if (rndStack.Class == EnumItemClass.Block)
            {
                return rndStack.Block.GetRandomColor(capi, pos, facing, rndIndex);
            }
            else
            {
                return capi.ItemTextureAtlas.GetRandomColor(rndStack.Item.FirstTexture.Baked.TextureSubId, rndIndex);
            }
        }


        public override int GetRandomColor(ICoreClientAPI capi, ItemStack stack)
        {
            ItemStack[] stacks = GetNonEmptyContents(capi.World, stack);
            if (stacks.Length == 0) return base.GetRandomColor(capi, stack);

            ItemStack rndStack = stacks[capi.World.Rand.Next(stacks.Length)];

            if (capi.World.Rand.NextDouble() < 0.4)
            {
                return capi.BlockTextureAtlas.GetRandomColor(Textures["ceramic"].Baked.TextureSubId);
            }

            return rndStack.Collectible.GetRandomColor(capi, stack);
        }
        

        public IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return new PotInFirepitRenderer(api as ICoreClientAPI, stack, firepit.Pos, forOutputSlot);
        }

        public EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return EnumFirepitModel.Wide;
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }


        public override bool OnSmeltAttempt(InventoryBase inventorySmelting)
        {
            if (Attributes?["isDirtyPot"]?.AsBool(false) == true)
            {
                InventorySmelting inv = (InventorySmelting)inventorySmelting;
                int quantityServings = (int)((float)(inv[1].Itemstack?.Attributes.GetDecimal("quantityServings") ?? 0) + 0.001f);  // forgive some rounding errors, but otherwise round down
                if (quantityServings > 0)
                {
                    ItemStack[] myStacks = GetNonEmptyContents(api.World, inv[1].Itemstack);
                    if (myStacks.Length > 0)
                    {
                        myStacks[0].StackSize = quantityServings;
                        inv.CookingSlots[0].Itemstack = myStacks[0];
                    }
                }

                AssetLocation loc = AssetLocation.CreateOrNull(Attributes?["emptiedBlockCode"]?.AsString());
                if (api.World.GetBlock(loc) is Block block) inv[1].Itemstack = new ItemStack(block);
                return true;
            }

            return false;
        }

        public virtual bool ShouldSpawnGSParticles(IWorldAccessor world, ItemStack stack) => world.Rand.NextDouble() < (GetTemperature(world, stack) - 50) / 160 / 8;

        public virtual void DoSpawnGSParticles(IAsyncParticleManager manager, BlockPos pos, Vec3f offset)
        {
            smokeHeld.MinPos = pos.ToVec3d().AddCopy(gsSmokePos).AddCopy(offset);
            manager.Spawn(smokeHeld);
        }

    }
}
