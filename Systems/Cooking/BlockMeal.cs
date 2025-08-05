using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public interface IBlockMealContainer
    {
        void SetContents(string? recipeCode, ItemStack containerStack, ItemStack?[] stacks, float quantityServings = 1);

        string? GetRecipeCode(IWorldAccessor world, ItemStack? containerStack);
        ItemStack[] GetContents(IWorldAccessor world, ItemStack containerStack);

        ItemStack[] GetNonEmptyContents(IWorldAccessor world, ItemStack containerStack);
        float GetQuantityServings(IWorldAccessor world, ItemStack containerStack);

        void SetQuantityServings(IWorldAccessor world, ItemStack containerStack, float quantityServings);
        public CookingRecipe? GetCookingRecipe(IWorldAccessor world, ItemStack? containerStack);

    }

    public interface IBlockEntityMealContainer
    {
        string? RecipeCode { get; set; }
        InventoryBase inventory { get; }
        float QuantityServings { get; set; }

        ItemStack[] GetNonEmptyContentStacks(bool cloned = true);

        void MarkDirty(bool redrawonclient, IPlayer? skipPlayer = null);
    }

    public class BlockMeal : BlockContainer, IBlockMealContainer, IContainedMeshSource, IContainedInteractable, IContainedCustomName, IGroundStoredParticleEmitter, IHandBookPageCodeProvider
    {
        protected virtual bool PlacedBlockEating => true;
        MealMeshCache? meshCache;

        Vec3d gsSmokePos = new Vec3d(0.5, 0.125, 0.5);

        public static BlockMeal[]? AllMealBowls => mealBowls;
        static BlockMeal[]? mealBowls;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (CollisionBoxes[0] != null) gsSmokePos.Y = CollisionBoxes[0].MaxY;
            mealBowls = api.World.Blocks.Where(b =>  b is BlockMeal && b.FirstCodePart().Contains("bowl")).Cast<BlockMeal>().ToArray();

            meshCache = api.ModLoader.GetModSystem<MealMeshCache>();
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }

        /// <summary>
        /// Should return 2 values: 1. nutrition mul, 2. health gain/loss mul
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="slot"></param>
        /// <param name="forEntity"></param>
        /// <returns></returns>
        public virtual float[] GetNutritionHealthMul(BlockPos? pos, ItemSlot slot, EntityAgent? forEntity)
        {
            return [1f, 1f];
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

                BlockCookedContainer.smokeHeld.MinPos = pos.AddCopy(-0.05, 0.1, -0.05);
                byEntity.World.SpawnParticles(BlockCookedContainer.smokeHeld);
            }
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (!tryHeldBeginEatMeal(slot, byEntity, ref handHandling))
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            return tryHeldContinueEatMeal(secondsUsed, slot, byEntity);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            tryFinishEatMeal(secondsUsed, slot, byEntity, true);
        }




        protected virtual bool tryHeldBeginEatMeal(ItemSlot slot, EntityAgent byEntity, ref EnumHandHandling handHandling)
        {
            if (!byEntity.Controls.ShiftKey && GetContentNutritionProperties(api.World, slot, byEntity) != null)
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

        protected bool tryPlacedBeginEatMeal(ItemSlot slot, IPlayer byPlayer)
        {
            if (GetContentNutritionProperties(api.World, slot, byPlayer.Entity) != null)
            {
                api.World.RegisterCallback((dt) =>
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

        protected virtual bool tryHeldContinueEatMeal(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            if (GetContentNutritionProperties(byEntity.World, slot, byEntity) == null) return false;

            Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ.Add(byEntity.LocalEyePos);
            pos.Y -= 0.4f;

            IPlayer? player = (byEntity as EntityPlayer)?.Player;

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

                return secondsUsed <= 1.5f;
            }

            // Let the client decide when he is done eating
            return true; 
        }


        protected bool tryPlacedContinueEatMeal(float secondsUsed, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!byPlayer.Entity.Controls.ShiftKey || GetContentNutritionProperties(api.World, slot, byPlayer.Entity) == null || slot.Itemstack is not ItemStack stack) return false;

            if (api.Side == EnumAppSide.Client)
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



                if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
                {
                    ItemStack[] contents = GetNonEmptyContents(api.World, stack);
                    if (contents.Length > 0)
                    {
                        ItemStack rndStack = contents[api.World.Rand.Next(contents.Length)];
                        api.World.SpawnCubeParticles(blockSel.Position.ToVec3d().Add(0.5f, 2 / 16f, 0.5f), rndStack, 0.2f, 4, 0.5f);
                    }
                }

                return secondsUsed <= 1.5f;
            }

            // Let the client decide when he is done eating
            return true;
        }


        protected virtual bool tryFinishEatMeal(float secondsUsed, ItemSlot slot, EntityAgent byEntity, bool handleAllServingsConsumed)
        {
            FoodNutritionProperties[]? multiProps = GetContentNutritionProperties(byEntity.World, slot, byEntity);

            if (byEntity.World.Side == EnumAppSide.Client || multiProps == null || secondsUsed < 1.45) return false;

            if (slot.Itemstack is not ItemStack foodSourceStack || (byEntity as EntityPlayer)?.Player is not IPlayer player) return false;
            slot.MarkDirty();

            float servingsLeft = GetQuantityServings(byEntity.World, foodSourceStack);
            ItemStack[] stacks = GetNonEmptyContents(api.World, foodSourceStack);

            if (stacks.Length == 0)
            {
                servingsLeft = 0;
            }
            else
            {
                string? recipeCode = GetRecipeCode(api.World, foodSourceStack);
                servingsLeft = Consume(byEntity.World, player, slot, stacks, servingsLeft, string.IsNullOrEmpty(recipeCode));
            }

            if (servingsLeft <= 0)
            {
                if (handleAllServingsConsumed)
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
                            if (!player.InventoryManager.TryGiveItemstack(new ItemStack(block), true))
                            {
                                byEntity.World.SpawnItemEntity(new ItemStack(block), byEntity.SidedPos.XYZ);
                            }
                        }
                    }
                    else
                    {
                        slot.TakeOut(1);
                        slot.MarkDirty();
                    }
                }
            }
            else
            {
                if (slot.Empty || slot.StackSize == 1)
                {
                    (foodSourceStack.Collectible as BlockMeal)?.SetQuantityServings(byEntity.World, foodSourceStack, servingsLeft);
                    slot.Itemstack = foodSourceStack;
                }
                else
                {
                    ItemStack? splitStack = slot.TakeOut(1);
                    (foodSourceStack.Collectible as BlockMeal)?.SetQuantityServings(byEntity.World, splitStack, servingsLeft);

                    ItemStack originalStack = slot.Itemstack;
                    slot.Itemstack = splitStack;

                    if (!player.InventoryManager.TryGiveItemstack(originalStack, true))
                    {
                        byEntity.World.SpawnItemEntity(originalStack, byEntity.SidedPos.XYZ);
                    }
                }
            }

            return true;
        }




        public bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!byPlayer.Entity.Controls.ShiftKey) return false;

            return tryPlacedBeginEatMeal(slot, byPlayer);
        }

        public bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!byPlayer.Entity.Controls.ShiftKey) return false;

            return tryPlacedContinueEatMeal(secondsUsed, slot, byPlayer, blockSel);
        }

        public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (tryFinishEatMeal(secondsUsed, slot, byPlayer.Entity, true))
            {
                be.MarkDirty(true);
            }
        }






        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!PlacedBlockEating) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            
            ItemStack stack = OnPickBlock(world, blockSel.Position);

            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                if (byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                {
                    world.BlockAccessor.SetBlock(0, blockSel.Position);
                    world.PlaySoundAt(Sounds.Place, byPlayer, byPlayer);
                    return true;
                }
                return false;
            }

            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityMeal bemeal)
            {
                DummySlot dummySlot = new DummySlot(stack, bemeal.inventory);
                dummySlot.MarkedDirty += () => true;

                return tryPlacedBeginEatMeal(dummySlot, byPlayer);
            }

            return false;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!PlacedBlockEating) return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
            if (!byPlayer.Entity.Controls.ShiftKey) return false;

            ItemStack stack = OnPickBlock(world, blockSel.Position);
            return tryPlacedContinueEatMeal(secondsUsed, new DummySlot(stack), byPlayer, blockSel);
        }


        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!PlacedBlockEating) base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
            if (!byPlayer.Entity.Controls.ShiftKey) return;

            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityMeal bemeal)
            {
                ItemStack stack = OnPickBlock(world, blockSel.Position);
                DummySlot dummySlot = new DummySlot(stack, bemeal.inventory);
                dummySlot.MarkedDirty += () => true;


                if (tryFinishEatMeal(secondsUsed, dummySlot, byPlayer.Entity, false))
                {
                    float servingsLeft = GetQuantityServings(world, stack);

                    if (bemeal.QuantityServings <= 0)
                    {
                        Block block = world.GetBlock(new AssetLocation(Attributes["eatenBlock"].AsString()));
                        world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position);
                    }
                    else
                    {
                        bemeal.QuantityServings = servingsLeft;
                        bemeal.MarkDirty(true);
                    }
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
            float[] nmul = GetNutritionHealthMul(null, inSlot, eatingPlayer.Entity);

            FoodNutritionProperties[] multiProps = GetContentNutritionProperties(world, inSlot, contentStacks, eatingPlayer.Entity, mulwithStackSize, nmul[0], nmul[1]);
            if (multiProps == null) return remainingServings;

            float totalHealth = 0;
            EntityBehaviorHunger? ebh = eatingPlayer.Entity.GetBehavior<EntityBehaviorHunger>();
            if (ebh == null) throw new Exception(eatingPlayer.Entity.Code.ToString() + "does not have EntityBehaviorHunger defined properly");
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
            float temp = inSlot.Itemstack?.Collectible.GetTemperature(world, inSlot.Itemstack) ?? 20;
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
                float satLossDelay = Math.Min(1.3f, mul * 3) * 10 + sat / 70f * 60f;

                eatingPlayer.Entity.ReceiveSaturation(sat, nutriProps.FoodCategory, satLossDelay, 1f);

                if (nutriProps.EatenStack?.ResolvedItemstack != null)
                {
                    if (!eatingPlayer.InventoryManager.TryGiveItemstack(nutriProps.EatenStack.ResolvedItemstack.Clone(), true))
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



        public override FoodNutritionProperties? GetNutritionProperties(IWorldAccessor world, ItemStack itemstack, Entity forEntity)
        {
            return null;
        }

        public static FoodNutritionProperties[] GetContentNutritionProperties(IWorldAccessor world, ItemSlot inSlot, ItemStack?[]? contentStacks, EntityAgent? forEntity, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
        {
            List<FoodNutritionProperties> foodProps = new List<FoodNutritionProperties>();
            if (contentStacks == null || inSlot.Itemstack is not ItemStack mealStack) return foodProps.ToArray();

            bool timeFrozen = mealStack.Attributes.GetBool("timeFrozen");

            for (int i = 0; i < contentStacks.Length; i++)
            {
                var contentStack = contentStacks[i];
                if (contentStack == null || GetIngredientStackNutritionProperties(world, contentStack, forEntity) is not FoodNutritionProperties stackProps) continue;

                float mul = mulWithStacksize ? contentStack.StackSize : 1;

                FoodNutritionProperties props = stackProps.Clone();

                float spoilState = 0;
                DummySlot slot = new DummySlot(contentStack, inSlot.Inventory);
                if (!timeFrozen)
                {
                    TransitionState? state = contentStack.Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
                    spoilState = state != null ? state.TransitionLevel : 0;
                }
                
                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, mealStack, forEntity);
                float healthLoss = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, mealStack, forEntity);
                props.Satiety *= satLossMul * nutritionMul * mul;
                props.Health *= healthLoss * healthMul * mul;

                foodProps.Add(props);
            }

            return foodProps.ToArray();
        }

        public static FoodNutritionProperties? GetIngredientStackNutritionProperties(IWorldAccessor world, ItemStack? stack, EntityAgent? forEntity)
        {
            if (stack == null) return null;

            CollectibleObject obj = stack.Collectible;
            ItemStack? cookedstack = obj.CombustibleProps?.SmeltedStack?.ResolvedItemstack;
            WaterTightContainableProps? liquidProps = BlockLiquidContainerBase.GetContainableProps(stack);

            FoodNutritionProperties? nutriProps = liquidProps?.NutritionPropsPerLitreWhenInMeal;
            if (liquidProps != null && nutriProps != null)
            {
                nutriProps = nutriProps.Clone();
                float litre = stack.StackSize / liquidProps.ItemsPerLitre;
                nutriProps.Health *= litre;
                nutriProps.Satiety *= litre;
            }

            nutriProps ??= obj.Attributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>();

            if (cookedstack != null)
            {
                obj = cookedstack.Collectible;
                liquidProps = BlockLiquidContainerBase.GetContainableProps(cookedstack);
            }

            if (liquidProps != null && nutriProps == null)
            {
                nutriProps = liquidProps.NutritionPropsPerLitre;
                if (nutriProps != null)
                {
                    nutriProps = nutriProps.Clone();
                    float litre = stack.StackSize / liquidProps.ItemsPerLitre;
                    nutriProps.Health *= litre;
                    nutriProps.Satiety *= litre;
                }
            }

            return nutriProps ?? obj.GetNutritionProperties(world, stack, forEntity);
        }

        public FoodNutritionProperties[]? GetContentNutritionProperties(IWorldAccessor world, ItemSlot inSlot, EntityAgent forEntity)
        {
            ItemStack[] stacks = GetNonEmptyContents(world, inSlot.Itemstack);
            if (stacks == null || stacks.Length == 0) return null;

            float[] nmul = GetNutritionHealthMul(null, inSlot, forEntity);
            return GetContentNutritionProperties(world, inSlot, stacks, forEntity, GetRecipeCode(world, inSlot.Itemstack) == null, nmul[0], nmul[1]);
        }


        public virtual string GetContentNutritionFacts(IWorldAccessor world, ItemSlot inSlotorFirstSlot, ItemStack[] contentStacks, EntityAgent? forEntity, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
        {
            FoodNutritionProperties[] props = GetContentNutritionProperties(world, inSlotorFirstSlot, contentStacks, forEntity, mulWithStacksize, nutritionMul, healthMul);

            Dictionary<EnumFoodCategory, float> totalSaturation = new Dictionary<EnumFoodCategory, float>();
            float totalHealth = 0;

            for (int i = 0; i < props.Length; i++)
            {
                FoodNutritionProperties prop = props[i];
                if (prop == null) continue;

                totalSaturation.TryGetValue(prop.FoodCategory, out float sat);

                totalHealth += prop.Health;
                totalSaturation[prop.FoodCategory] = sat + prop.Satiety;
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


        public string GetContentNutritionFacts(IWorldAccessor world, ItemSlot inSlot, EntityAgent? forEntity, bool mulWithStacksize = false)
        {
            float[] nmul = GetNutritionHealthMul(null, inSlot, forEntity);
            return GetContentNutritionFacts(world, inSlot, GetNonEmptyContents(world, inSlot.Itemstack), forEntity, mulWithStacksize, nmul[0], nmul[1]);
        }


        public void SetContents(string? recipeCode, ItemStack containerStack, ItemStack?[] stacks, float quantityServings = 1)
        {
            base.SetContents(containerStack, stacks);

            if (recipeCode != null) containerStack.Attributes.SetString("recipeCode", recipeCode);
            containerStack.Attributes.SetFloat("quantityServings", quantityServings);

            if (stacks.Length > 0)
            {
                SetTemperature(api.World, containerStack, stacks[0]?.Collectible.GetTemperature(api.World, stacks[0]) ?? 20);
            }
        }


        public float GetQuantityServings(IWorldAccessor world, ItemStack byItemStack)
        {
            return (float)byItemStack.Attributes.GetDecimal("quantityServings");
        }

        public void SetQuantityServings(IWorldAccessor world, ItemStack? byItemStack, float value)
        {
            if (byItemStack == null) return;

            if (value <= 0f)
            {
                byItemStack.Attributes.RemoveAttribute("recipeCode");
                byItemStack.Attributes.RemoveAttribute("quantityServings");
                byItemStack.Attributes.RemoveAttribute("contents");
                return;
            }
            byItemStack.Attributes.SetFloat("quantityServings", value);
        }


        public string? GetRecipeCode(IWorldAccessor world, ItemStack? containerStack)
        {
            return containerStack?.Attributes.GetString("recipeCode");
        }

        public CookingRecipe? GetCookingRecipe(IWorldAccessor world, ItemStack? containerStack)
        {
            string? recipecode = GetRecipeCode(world, containerStack);
            return api.GetCookingRecipe(recipecode);
        }


        
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            MultiTextureMeshRef? meshref = meshCache!.GetOrCreateMealInContainerMeshRef(this, GetCookingRecipe(capi.World, itemstack), GetNonEmptyContents(capi.World, itemstack));
            if (meshref != null) renderinfo.ModelRef = meshref;
        }

        public virtual MeshData? GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos? forBlockPos = null)
        {
            if (api is not ICoreClientAPI capi) return null;
            return meshCache!.GenMealInContainerMesh(this, GetCookingRecipe(capi.World, itemstack), GetNonEmptyContents(capi.World, itemstack));
        }

        public virtual string GetMeshCacheKey(ItemStack itemstack)
        {
            return meshCache!.GetMealHashCode(itemstack).ToString();
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos);

            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMeal bem)
            {
                SetContents(bem.RecipeCode, stack, bem.GetNonEmptyContentStacks(), bem.QuantityServings);
            }

            return stack;
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[] { new BlockDropItemStack(handbookStack) };
        }

        public virtual string HandbookPageCodeForStack(IWorldAccessor world, ItemStack stack)
        {
            if (GetRecipeCode(world, stack) is string code) return "handbook-mealrecipe-" + code;
            return GuiHandbookItemStackPage.PageCodeForStack(stack);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return new ItemStack[] { OnPickBlock(world, pos) };
        }

        protected bool displayContentsInfo = true;

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot.Itemstack is not ItemStack mealStack) return;
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            float temp = GetTemperature(world, mealStack);
            if (temp > 20)
            {
                dsc.AppendLine(Lang.Get("Temperature: {0}°C", (int)temp));
            }

            CookingRecipe? recipe = GetCookingRecipe(world, mealStack);

            ItemStack[] stacks = GetNonEmptyContents(world, mealStack);
            DummyInventory dummyInv = new DummyInventory(api);

            ItemSlot slot = BlockCrock.GetDummySlotForFirstPerishableStack(api.World, stacks, null, dummyInv);
            dummyInv.OnAcquireTransitionSpeed += (transType, stack, mul) =>
            {
                float val = mul * GetContainingTransitionModifierContained(world, inSlot, transType);

                if (inSlot.Inventory != null) val *= inSlot.Inventory.GetTransitionSpeedMul(transType, mealStack);

                return val;
            };

            slot.Itemstack?.Collectible.AppendPerishableInfoText(slot, dsc, world);

            float servings = GetQuantityServings(world, mealStack);

            if (recipe != null)
            {
                if (Math.Round(servings, 1) < 0.05)
                {
                    dsc.AppendLine(Lang.Get("{1}% serving of {0}", recipe.GetOutputName(world, stacks).UcFirst(), Math.Round(servings * 100, 0)));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("{0} serving of {1}", Math.Round(servings, 1), recipe.GetOutputName(world, stacks).UcFirst()));
                }

            }
            else if (mealStack.Attributes.HasAttribute("quantityServings"))
            {
                if (Math.Round(servings, 1) < 0.05)
                {
                    dsc.AppendLine(Lang.Get("meal-servingsleft-percent", Math.Round(servings * 100, 0)));
                }
                else dsc.AppendLine(Lang.Get("{0} servings left", Math.Round(servings, 1)));
            }
            else if (displayContentsInfo && !MealMeshCache.ContentsRotten(stacks))
            {
                dsc.AppendLine(Lang.Get("Contents: {0}", Lang.Get("meal-ingredientlist-" + stacks.Length, stacks.Select(stack => Lang.Get("{0}x {1}", stack.StackSize, stack.GetName())))));
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



        public string GetContainedName(ItemSlot inSlot, int quantity)
        {
            return GetHeldItemName(inSlot.Itemstack);
        }

        public string GetContainedInfo(ItemSlot inSlot)
        {
            CookingRecipe? recipe = GetCookingRecipe(api.World, inSlot.Itemstack);
            ItemStack[] stacks = GetNonEmptyContents(api.World, inSlot.Itemstack);

            if (inSlot.Itemstack?.Block is not BlockMeal contBlock) return Lang.Get("unknown");

            var emptyCode = contBlock.Attributes?["eatenBlock"].AsString();
            string emptyName = new ItemStack(emptyCode == null ? contBlock : api.World.GetBlock(emptyCode)).GetName();

            if (stacks.Length == 0) return Lang.GetWithFallback("contained-empty-container", "{0} (Empty)", emptyName);

            string? outputName = recipe?.GetOutputName(api.World, stacks).UcFirst() ?? stacks[0].GetName();
            float servings = inSlot.Itemstack?.Attributes.GetFloat("quantityServings", 1) ?? 1;
            if (MealMeshCache.ContentsRotten(stacks))
            {
                outputName = Lang.Get("Rotten Food");
                servings = 1;
            }

            return Lang.Get("contained-food-singleservingmax", Math.Round(servings, 1), outputName, emptyName, PerishableInfoCompactContainer(api, inSlot));
        }


        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);

            IWorldAccessor world = entityItem.World;
            if (world.Side != EnumAppSide.Server) return;

            if (entityItem.Swimming && world.Rand.NextDouble() < 0.01)
            {
                ItemStack[] stacks = GetNonEmptyContents(world, entityItem.Itemstack);

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

                var eatenBlock = Attributes["eatenBlock"].AsString();
                if (eatenBlock == null) return;
                Block block = world.GetBlock(new AssetLocation(eatenBlock));

                entityItem.Itemstack = new ItemStack(block);
                entityItem.WatchedAttributes.MarkPathDirty("itemstack");
            }
        }



        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            if (capi.World.BlockAccessor.GetBlockEntity(pos) is not BlockEntityContainer bem || bem.GetNonEmptyContentStacks(false) is not ItemStack[] stacks || stacks.Length == 0)
            {
                return base.GetRandomColor(capi, pos, facing, rndIndex);
            }

            return GetRandomContentColor(capi, stacks);
        }


        public override int GetRandomColor(ICoreClientAPI capi, ItemStack stack)
        {
            if (GetNonEmptyContents(capi.World, stack) is not ItemStack[] stacks || stacks.Length == 0) return base.GetRandomColor(capi, stack);

            return GetRandomContentColor(capi, stacks);
        }

        public override TransitionState[]? UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            if (inslot.Itemstack is not ItemStack mealStack) return null;

            ItemStack[] stacks = GetNonEmptyContents(world, mealStack);
            foreach (var stack in stacks) stack.StackSize *= (int)Math.Max(1, mealStack.Attributes.TryGetFloat("quantityServings") ?? 1);
            SetContents(mealStack, stacks);

            TransitionState[]? states = base.UpdateAndGetTransitionStates(world, inslot);

            stacks = GetNonEmptyContents(world, mealStack);
            if (stacks.Length == 0 || MealMeshCache.ContentsRotten(stacks))
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    var transProps = stacks[i].Collectible.GetTransitionableProperties(world, stacks[i], null);
                    var spoilProps = transProps?.FirstOrDefault(props => props.Type == EnumTransitionType.Perish);
                    if (spoilProps == null) continue;
                    stacks[i] = stacks[i].Collectible.OnTransitionNow(GetContentInDummySlot(inslot, stacks[i]), spoilProps);
                }
                SetContents(mealStack, stacks);

                mealStack.Attributes.RemoveAttribute("recipeCode");
                mealStack.Attributes.RemoveAttribute("quantityServings");
            }

            foreach (var stack in stacks) stack.StackSize /= (int)Math.Max(1, mealStack.Attributes.TryGetFloat("quantityServings") ?? 1);
            SetContents(mealStack, stacks);

            if (stacks.Length == 0 && AssetLocation.CreateOrNull(Attributes?["eatenBlock"]?.AsString()) is AssetLocation loc && world.GetBlock(loc) is Block block)
            {
                mealStack = new ItemStack(block);
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
                    HotKeyCode = "shift"
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public virtual bool ShouldSpawnGSParticles(IWorldAccessor world, ItemStack stack) => world.Rand.NextDouble() < (GetTemperature(world, stack) - 50) / 320 / 8;

        public virtual void DoSpawnGSParticles(IAsyncParticleManager manager, BlockPos pos, Vec3f offset)
        {
            BlockCookedContainer.smokeHeld.MinPos = pos.ToVec3d().AddCopy(gsSmokePos).AddCopy(offset);
            manager.Spawn(BlockCookedContainer.smokeHeld);
        }

    }
}
