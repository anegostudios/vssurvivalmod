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
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public interface IBlockMealContainer
    {
        void SetContents(string recipeCode, ItemStack containerStack, ItemStack[] stacks, float quantityServings = 1);

        string GetRecipeCode(IWorldAccessor world, ItemStack containerStack);
        ItemStack[] GetContents(IWorldAccessor world, ItemStack containerStack);

        ItemStack[] GetNonEmptyContents(IWorldAccessor world, ItemStack containerStack);
        float GetQuantityServings(IWorldAccessor world, ItemStack containerStack);

        void SetQuantityServings(IWorldAccessor world, ItemStack containerStack, float quantityServings);
    }

    public interface IBlockEntityMealContainer
    {
        string RecipeCode { get; set; }
        InventoryBase inventory { get; }
        float QuantityServings { get; set; }

        ItemStack[] GetNonEmptyContentStacks(bool cloned = true);

        void MarkDirty(bool redrawonclient, IPlayer skipPlayer = null);
    }

    public class BlockMeal : BlockContainer, IBlockMealContainer
    {
        protected virtual bool PlacedBlockEating => true;

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }

        public virtual float GetNutritionMul(BlockPos pos, ItemSlot slot, EntityAgent forEntity)
        {
            return 1f;
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
                    byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y - 0.5f, 0)
                    .Ahead(0.33f, byEntity.Pos.Pitch, byEntity.Pos.Yaw)
                    .Ahead(sideWays, 0, byEntity.Pos.Yaw + GameMath.PIHALF)
                ;

                BlockCookedContainer.smokeHeld.MinPos = pos.AddCopy(-0.05, 0.1, -0.05);
                byEntity.World.SpawnParticles(BlockCookedContainer.smokeHeld);
            }
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (!tryBeginEatMeal(slot, byEntity, ref handHandling))
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            return tryContinueEatMeal(secondsUsed, slot, byEntity);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            tryFinishEatMeal(secondsUsed, slot, byEntity);
        }




        protected virtual bool tryBeginEatMeal(ItemSlot slot, EntityAgent byEntity, ref EnumHandHandling handHandling)
        {
            if (!byEntity.Controls.Sneak && GetContentNutritionProperties(api.World, slot, byEntity) != null)
            {
                byEntity.World.RegisterCallback((dt) =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                    {
                        byEntity.PlayEntitySound("eat", (byEntity as EntityPlayer)?.Player);
                    }
                }, 500);

                handHandling = EnumHandHandling.PreventDefault;
                return true;
            }

            return false;
        }

        protected virtual bool tryContinueEatMeal(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            if (GetContentNutritionProperties(byEntity.World, slot, byEntity) == null) return false;

            Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ.Add(byEntity.LocalEyePos);
            pos.Y -= 0.4f;

            IPlayer player = (byEntity as EntityPlayer).Player;

            if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
            {
                ItemStack[] contents = GetNonEmptyContents(byEntity.World, slot.Itemstack);
                if (contents.Length > 0)
                {
                    ItemStack rndStack = contents[byEntity.World.Rand.Next(contents.Length)];
                    byEntity.World.SpawnCubeParticles(pos, rndStack, 0.3f, 4, 1, player);
                }


            }


            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.Origin.Set(1.1f, 0.5f, 0.5f);
                tf.EnsureDefaultValues();

                tf.Translation.X -= Math.Min(1.7f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Translation.Y += Math.Min(0.4f, secondsUsed * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Scale = 1 + Math.Min(0.5f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Rotation.X += Math.Min(40f, secondsUsed * 350 * 0.75f) / FpHandTransform.ScaleXYZ.X;

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.Y += GameMath.Sin(30 * secondsUsed) / 10 / FpHandTransform.ScaleXYZ.Y;
                }

                byEntity.Controls.UsingHeldItemTransformBefore = tf;


                return secondsUsed <= 1.5f;
            }

            // Let the client decide when he is done eating
            return true; 
        }

        protected virtual void tryFinishEatMeal(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            FoodNutritionProperties[] multiProps = GetContentNutritionProperties(byEntity.World, slot, byEntity);

            if (byEntity.World.Side == EnumAppSide.Server && multiProps != null && secondsUsed >= 1.45f)
            {
                ItemStack foodSourceStack = slot.Itemstack;
                slot.MarkDirty();
                IPlayer player = (byEntity as EntityPlayer).Player;

                float servingsLeft = GetQuantityServings(byEntity.World, foodSourceStack);
                ItemStack[] stacks = GetNonEmptyContents(api.World, foodSourceStack);

                if (stacks.Length == 0)
                {
                    servingsLeft = 0;
                }
                else
                {
                    string recipeCode = GetRecipeCode(api.World, foodSourceStack);
                    servingsLeft = Consume(byEntity.World, player, slot, stacks, servingsLeft, recipeCode == null || recipeCode == "");
                }


                if (servingsLeft <= 0)
                {
                    if (Attributes["eatenBlock"].Exists)
                    {
                        Block block = byEntity.World.GetBlock(new AssetLocation(Attributes["eatenBlock"].AsString()));

                        if (slot.Empty || slot.StackSize == 1)
                        {
                            slot.Itemstack = new ItemStack(block);
                        }
                        else
                        {
                            if (player == null || !player.InventoryManager.TryGiveItemstack(new ItemStack(block), true))
                            {
                                byEntity.World.SpawnItemEntity(new ItemStack(block), byEntity.SidedPos.XYZ);
                            }
                        }
                    } else
                    {
                        slot.TakeOut(1);
                        slot.MarkDirty();
                    }
                }
                else
                {
                    if (slot.Empty || slot.StackSize == 1)
                    {
                        (foodSourceStack.Collectible as BlockMeal).SetQuantityServings(byEntity.World, foodSourceStack, servingsLeft);
                        slot.Itemstack = foodSourceStack;
                    }
                    else
                    {
                        ItemStack splitStack = slot.TakeOut(1);
                        (foodSourceStack.Collectible as BlockMeal).SetQuantityServings(byEntity.World, splitStack, servingsLeft);

                        ItemStack originalStack = slot.Itemstack;
                        slot.Itemstack = splitStack;

                        if (player == null || !player.InventoryManager.TryGiveItemstack(originalStack, true))
                        {
                            byEntity.World.SpawnItemEntity(originalStack, byEntity.SidedPos.XYZ);
                        }
                    }
                }
            }
        }





        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!PlacedBlockEating) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            
            ItemStack stack = OnPickBlock(world, blockSel.Position);

            if (!byPlayer.Entity.Controls.Sneak)
            {
                if (byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                {
                    world.BlockAccessor.SetBlock(0, blockSel.Position);
                    world.PlaySoundAt(this.Sounds.Place, byPlayer, byPlayer);
                    return true;
                }
                return false;
            }

            BlockEntityMeal bemeal = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMeal;
            DummySlot dummySlot = new DummySlot(stack, bemeal.inventory);
            dummySlot.MarkedDirty += () => true;

            if (GetContentNutritionProperties(world, dummySlot, byPlayer.Entity) != null)
            {
                world.RegisterCallback((dt) =>
                {
                    if (byPlayer.Entity.Controls.HandUse == EnumHandInteract.BlockInteract)
                    {
                        byPlayer.Entity.PlayEntitySound("eat", byPlayer);
                    }
                }, 500);

                byPlayer.Entity.StartAnimation("eat");

                return true;
            }

            return false;
        }


        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!PlacedBlockEating) return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);

            if (!byPlayer.Entity.Controls.Sneak) return false;

            ItemStack stack = OnPickBlock(world, blockSel.Position);

            if (world is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.Origin.Set(1.1f, 0.5f, 0.5f);
                tf.EnsureDefaultValues();

                if (ItemClass == EnumItemClass.Item)
                {
                    if (secondsUsed > 0.5f)
                    {
                        tf.Translation.X = GameMath.Sin(30 * secondsUsed) / 10;
                    }

                    tf.Translation.Z += -Math.Min(1.6f, secondsUsed * 4 * 1.57f);
                    tf.Translation.Y += Math.Min(0.15f, secondsUsed * 2);

                    tf.Rotation.Y -= Math.Min(85f, secondsUsed * 350 * 1.5f);
                    tf.Rotation.X += Math.Min(40f, secondsUsed * 350 * 0.75f);
                    tf.Rotation.Z += Math.Min(30f, secondsUsed * 350 * 0.75f);
                }
                else
                {
                    tf.Translation.X -= Math.Min(1.7f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                    tf.Translation.Y += Math.Min(0.4f, secondsUsed * 1.8f) / FpHandTransform.ScaleXYZ.X;
                    tf.Scale = 1 + Math.Min(0.5f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                    tf.Rotation.X += Math.Min(40f, secondsUsed * 350 * 0.75f) / FpHandTransform.ScaleXYZ.X;

                    if (secondsUsed > 0.5f)
                    {
                        tf.Translation.Y += GameMath.Sin(30 * secondsUsed) / 10 / FpHandTransform.ScaleXYZ.Y;
                    }
                }

                byPlayer.Entity.Controls.UsingHeldItemTransformBefore = tf;

                

                if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
                {
                    ItemStack[] contents = GetNonEmptyContents(world, stack);
                    if (contents.Length > 0)
                    {
                        ItemStack rndStack = contents[world.Rand.Next(contents.Length)];
                        world.SpawnCubeParticles(blockSel.Position.ToVec3d().Add(0.5f, 2 / 16f, 0.5f), rndStack, 0.2f, 4, 0.5f);
                    }
                }

                return secondsUsed <= 1.5f;
            }

            // Let the client decide when he is done eating
            return true;
        }


        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!PlacedBlockEating) base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);

            if (!byPlayer.Entity.Controls.Sneak) return;

            ItemStack stack = OnPickBlock(world, blockSel.Position);

            if (world.Side == EnumAppSide.Server && secondsUsed >= 1.45f)
            {
                BlockEntityMeal bemeal = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMeal;
                DummySlot dummySlot = new DummySlot(stack, bemeal.inventory);
                dummySlot.MarkedDirty += () => true;

                ItemStack[] contents = GetNonEmptyContents(world, stack);
                if (contents.Length > 0)
                {
                    float servingsLeft = Consume(world, byPlayer, dummySlot, contents, GetQuantityServings(world, stack), GetRecipeCode(world, stack) == null);
                    bemeal.QuantityServings = servingsLeft;
                } else
                {
                    bemeal.QuantityServings = 0;
                }

                if (bemeal.QuantityServings <= 0)
                {
                    Block block = world.GetBlock(new AssetLocation(Attributes["eatenBlock"].AsString()));
                    world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position);
                }
                else
                {
                    bemeal.MarkDirty(true);
                }
            }
        }



        /// <summary>
        /// Consumes a meal
        /// </summary>
        /// <param name="world"></param>
        /// <param name="eatingPlayer"></param>
        /// <param name="contentStacks"></param>
        /// <param name="recipeCode"></param>
        /// <param name="remainingServings"></param>
        /// <returns>Amount of servings left</returns>
        public virtual float Consume(IWorldAccessor world, IPlayer eatingPlayer, ItemSlot inSlot, ItemStack[] contentStacks, float remainingServings, bool mulwithStackSize)
        {
            FoodNutritionProperties[] multiProps = GetContentNutritionProperties(world, inSlot, contentStacks, eatingPlayer.Entity, mulwithStackSize, GetNutritionMul(null, inSlot, eatingPlayer.Entity));
            if (multiProps == null) return remainingServings;

            float totalHealth = 0;
            EntityBehaviorHunger ebh = eatingPlayer.Entity.GetBehavior<EntityBehaviorHunger>();
            float satiablePoints = ebh.MaxSaturation - ebh.Saturation;
            

            float mealSatpoints = 0;
            for (int i = 0; i < multiProps.Length; i++)
            {
                FoodNutritionProperties nutriProps = multiProps[i];
                if (nutriProps == null) continue;

                mealSatpoints += nutriProps.Satiety;
            }

            float servingsNeeded = GameMath.Clamp(satiablePoints / Math.Max(1, mealSatpoints), 0, 1);
            float servingsToEat = Math.Min(remainingServings, servingsNeeded);

            // Affect the players body temperature by eating meals with a larger temperature difference
            float temp = inSlot.Itemstack.Collectible.GetTemperature(world, inSlot.Itemstack);
            var bh = eatingPlayer.Entity.GetBehavior<EntityBehaviorBodyTemperature>();
            if (bh != null && Math.Abs(temp - bh.CurBodyTemperature) > 10)
            {
                float intensity = Math.Min(1, (temp - bh.CurBodyTemperature) / 30f);
                bh.CurBodyTemperature += GameMath.Clamp(mealSatpoints * servingsToEat / 80f * intensity, 0, 5);
            }


            for (int i = 0; i < multiProps.Length; i++)
            {
                FoodNutritionProperties nutriProps = multiProps[i];
                if (nutriProps == null) continue;

                float mul = servingsToEat;
                float sat = mul * nutriProps.Satiety;

                eatingPlayer.Entity.ReceiveSaturation(sat, nutriProps.FoodCategory, 10 + sat / 70f * 60f, 1f);

                if (nutriProps.EatenStack?.ResolvedItemstack != null)
                {
                    if (eatingPlayer == null || !eatingPlayer.InventoryManager.TryGiveItemstack(nutriProps.EatenStack.ResolvedItemstack.Clone(), true))
                    {
                        world.SpawnItemEntity(nutriProps.EatenStack.ResolvedItemstack.Clone(), eatingPlayer.Entity.SidedPos.XYZ);
                    }
                }

                totalHealth += mul * nutriProps.Health;
            }


            if (totalHealth != 0)
            {
                eatingPlayer.Entity.ReceiveDamage(new DamageSource()
                {
                    Source = EnumDamageSource.Internal,
                    Type = totalHealth > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                }, Math.Abs(totalHealth));
            }


            return Math.Max(0, remainingServings - servingsToEat);
        }



        public override FoodNutritionProperties GetNutritionProperties(IWorldAccessor world, ItemStack itemstack, Entity forEntity)
        {
            return null;
        }

        public static FoodNutritionProperties[] GetContentNutritionProperties(IWorldAccessor world, ItemSlot inSlot, ItemStack[] contentStacks, EntityAgent forEntity, bool mulWithStacksize = false, float nutritionMul = 1)
        {
            List<FoodNutritionProperties> foodProps = new List<FoodNutritionProperties>();
            if (contentStacks == null) return foodProps.ToArray();

            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null) continue;

                CollectibleObject obj = contentStacks[i].Collectible;
                FoodNutritionProperties stackProps;

                if (obj.CombustibleProps != null && obj.CombustibleProps.SmeltedStack != null)
                {
                    stackProps = obj.CombustibleProps.SmeltedStack.ResolvedItemstack.Collectible.GetNutritionProperties(world, obj.CombustibleProps.SmeltedStack.ResolvedItemstack, forEntity);
                }
                else
                {
                    stackProps = obj.GetNutritionProperties(world, contentStacks[i], forEntity);
                }

                if (obj.Attributes?["nutritionPropsWhenInMeal"].Exists == true)
                {
                    stackProps = obj.Attributes?["nutritionPropsWhenInMeal"].AsObject<FoodNutritionProperties>();
                }

                if (stackProps == null) continue;

                float mul = mulWithStacksize ? contentStacks[i].StackSize : 1;

                FoodNutritionProperties props = stackProps.Clone();

                DummySlot slot = new DummySlot(contentStacks[i], inSlot.Inventory);
                TransitionState state = contentStacks[i].Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;
                
                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
                float healthLoss = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, forEntity);
                props.Satiety *= satLossMul * nutritionMul * mul;
                props.Health *= healthLoss * mul;

                foodProps.Add(props);
            }

            return foodProps.ToArray();
        }

        public FoodNutritionProperties[] GetContentNutritionProperties(IWorldAccessor world, ItemSlot inSlot, EntityAgent forEntity)
        {
            ItemStack[] stacks = GetNonEmptyContents(world, inSlot.Itemstack);
            if (stacks == null || stacks.Length == 0) return null;

            return GetContentNutritionProperties(world, inSlot, stacks, forEntity, GetRecipeCode(world, inSlot.Itemstack) == null, GetNutritionMul(null, inSlot, forEntity));
        }


        public virtual string GetContentNutritionFacts(IWorldAccessor world, ItemSlot inSlotorFirstSlot, ItemStack[] contentStacks, EntityAgent forEntity, bool mulWithStacksize = false, float nutritionMul = 1)
        {
            FoodNutritionProperties[] props = GetContentNutritionProperties(world, inSlotorFirstSlot, contentStacks, forEntity, mulWithStacksize, nutritionMul);

            Dictionary<EnumFoodCategory, float> totalSaturation = new Dictionary<EnumFoodCategory, float>();
            float totalHealth = 0;

            for (int i = 0; i < props.Length; i++)
            {
                FoodNutritionProperties prop = props[i];
                if (prop == null) continue;

                float sat = 0;
                totalSaturation.TryGetValue(prop.FoodCategory, out sat);

                DummySlot slot = new DummySlot(contentStacks[i], inSlotorFirstSlot.Inventory);
                
                TransitionState state = contentStacks[i].Collectible.UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
                float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, forEntity);

                totalHealth += prop.Health * healthLossMul;
                totalSaturation[prop.FoodCategory] = sat + prop.Satiety * satLossMul;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Lang.Get("Nutrition Facts"));

            foreach (var val in totalSaturation)
            {
                sb.AppendLine(Lang.Get("nutrition-facts-line-satiety", Lang.Get("foodcategory-" + val.Key.ToString().ToLowerInvariant()), Math.Round(val.Value)));
            }

            if (totalHealth != 0)
            {
                sb.AppendLine("- " + Lang.Get("Health: {0}{1} hp", totalHealth > 0 ? "+" : "", totalHealth));
            }

            return sb.ToString();
        }


        public string GetContentNutritionFacts(IWorldAccessor world, ItemSlot inSlot, EntityAgent forEntity, bool mulWithStacksize = false)
        {
            return GetContentNutritionFacts(world, inSlot, GetNonEmptyContents(world, inSlot.Itemstack), forEntity, mulWithStacksize, GetNutritionMul(null, inSlot, forEntity));
        }


        public void SetContents(string recipeCode, ItemStack containerStack, ItemStack[] stacks, float quantityServings = 1)
        {
            base.SetContents(containerStack, stacks);

            containerStack.Attributes.SetString("recipeCode", recipeCode);
            containerStack.Attributes.SetFloat("quantityServings", quantityServings);

            if (stacks.Length > 0)
            {
                SetTemperature(api.World, containerStack, stacks[0].Collectible.GetTemperature(api.World, stacks[0]));
            }
        }


        public float GetQuantityServings(IWorldAccessor world, ItemStack byItemStack)
        {
            return (float)byItemStack.Attributes.GetDecimal("quantityServings");
        }

        public void SetQuantityServings(IWorldAccessor world, ItemStack byItemStack, float value)
        {
            byItemStack.Attributes.SetFloat("quantityServings", value);
        }


        public string GetRecipeCode(IWorldAccessor world, ItemStack containerStack)
        {
            return containerStack.Attributes.GetString("recipeCode");
        }

        public CookingRecipe GetCookingRecipe(IWorldAccessor world, ItemStack containerStack)
        {
            string recipecode = GetRecipeCode(world, containerStack);
            return world.CookingRecipes.FirstOrDefault((rec) => recipecode == rec.Code);
        }

        MealMeshCache meshCache;
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (meshCache == null) meshCache = capi.ModLoader.GetModSystem<MealMeshCache>();

            MeshRef meshref = meshCache.GetOrCreateMealInContainerMeshRef(this, GetCookingRecipe(capi.World, itemstack), GetNonEmptyContents(capi.World, itemstack));
            if (meshref != null) renderinfo.ModelRef = meshref;
        }



        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos);

            BlockEntityMeal bem = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMeal;

            if (bem != null)
            {
                SetContents(bem.RecipeCode, stack, bem.GetNonEmptyContentStacks(), bem.QuantityServings);
            }

            return stack;
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[] { new BlockDropItemStack(handbookStack) };
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return new ItemStack[] { OnPickBlock(world, pos) };
        }

        protected bool displayContentsInfo = true;

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            float temp = GetTemperature(world, inSlot.Itemstack);
            if (temp > 20)
            {
                dsc.AppendLine(Lang.Get("Temperature: {0}°C", (int)temp));
            }

            CookingRecipe recipe = GetCookingRecipe(world, inSlot.Itemstack);

            ItemStack[] stacks = GetNonEmptyContents(world, inSlot.Itemstack);
            ItemSlot slot = BlockCrock.GetDummySlotForFirstPerishableStack(world, stacks, null, inSlot.Inventory);

            slot.Itemstack?.Collectible.AppendPerishableInfoText(slot, dsc, world);

            float servings = GetQuantityServings(world, inSlot.Itemstack);

            if (recipe != null)
            {
                if (Math.Round(servings, 1) < 0.05)
                {
                    dsc.AppendLine(Lang.Get("{1}% serving of {0}", recipe.GetOutputName(world, stacks).UcFirst(), Math.Round(servings * 100, 0)));
                } else
                {
                    dsc.AppendLine(Lang.Get("{0} serving of {1}", Math.Round(servings, 1), recipe.GetOutputName(world, stacks).UcFirst()));
                }
                
            } else
            {
                if (displayContentsInfo)
                {
                    dsc.AppendLine(Lang.Get("Contents:"));
                    if (stacks != null && stacks.Length > 0)
                    {
                        dsc.AppendLine(stacks[0].StackSize + "x " + stacks[0].GetName());
                    }
                }
            }

            if (!MealMeshCache.ContentsRotten(stacks))
            {
                string facts = GetContentNutritionFacts(world, inSlot, null, recipe == null);

                if (facts != null)
                {
                    dsc.Append(facts);
                }
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

                if (MealMeshCache.ContentsRotten(stacks)) {
                    for (int i = 0; i < stacks.Length; i++)
                    {
                        if (stacks[i] != null && stacks[i].StackSize > 0 && stacks[i].Collectible.Code.Path == "rot")
                        {
                            world.SpawnItemEntity(stacks[i], entityItem.ServerPos.XYZ);
                        }
                    }

                    Block block = world.GetBlock(new AssetLocation(Attributes["eatenBlock"].AsString()));
                    entityItem.Itemstack = new ItemStack(block);
                    entityItem.WatchedAttributes.MarkPathDirty("itemstack");
                }
            }
        }



        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            BlockEntityContainer bem = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (bem == null) return base.GetRandomColor(capi, pos, facing);

            ItemStack[] stacks = bem.GetNonEmptyContentStacks(false);

            if (stacks != null && stacks.Length > 0)
            {
                return GetRandomContentColor(capi, stacks);
            } else
            {
                return base.GetRandomColor(capi, pos, facing);
            }
        }


        public override TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            TransitionState[] states = base.UpdateAndGetTransitionStates(world, inslot);

            ItemStack[] stacks = GetNonEmptyContents(world, inslot.Itemstack);
            if (MealMeshCache.ContentsRotten(stacks))
            {
                inslot.Itemstack.Attributes?.RemoveAttribute("recipeCode");
                inslot.Itemstack.Attributes?.RemoveAttribute("quantityServings");
            }
            if (stacks == null || stacks.Length == 0)
            {
                inslot.Itemstack.Attributes?.RemoveAttribute("recipeCode");
                inslot.Itemstack.Attributes?.RemoveAttribute("quantityServings");
            }

            string eaten = Attributes["eatenBlock"].AsString();
            if ((stacks == null || stacks.Length == 0) && eaten != null)
            {
                Block block = world.GetBlock(new AssetLocation(eaten));

                if (block != null)
                {
                    inslot.Itemstack = new ItemStack(block);
                    inslot.MarkDirty();
                }
            }

            return states;
        }


        public override string GetHeldItemName(ItemStack itemStack)
        {
            ItemStack[] contentStacks = GetContents(api.World, itemStack);
            if (MealMeshCache.ContentsRotten(contentStacks))
            {
                return Lang.Get("Bowl of rotten food");
            }

            return base.GetHeldItemName(itemStack);
        }



        public virtual int GetRandomContentColor(ICoreClientAPI capi, ItemStack[] stacks)
        {
            ItemStack rndStack = stacks[capi.World.Rand.Next(stacks.Length)];
            return rndStack.Collectible.GetRandomColor(capi, rndStack);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-meal-pickup",
                    MouseButton = EnumMouseButton.Right,
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-meal-eat",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sneak"
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

    }
}
