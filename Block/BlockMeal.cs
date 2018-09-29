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
    public class BlockMeal : BlockContainer
    {
        public override void OnHeldIdle(IItemSlot slot, IEntityAgent byEntity)
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

                BlockCookedContainer.smokeHeld.minPos = pos.AddCopy(-0.05, 0.1, -0.05);
                byEntity.World.SpawnParticles(BlockCookedContainer.smokeHeld);
            }
        }


        public override void OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling)
        {
            if (!byEntity.Controls.Sneak && GetContentNutritionProperties(byEntity.World, slot.Itemstack, byEntity as Entity) != null)
            {
                byEntity.World.RegisterCallback((dt) =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                    {
                        byEntity.PlayEntitySound("eat", (byEntity as EntityPlayer)?.Player);
                    }
                }, 500);

                byEntity.StartAnimation("eat");

                handHandling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, ref handHandling);
        }

        /// <summary>
        /// Called every frame while the player is using this collectible. Return false to stop the interaction.
        /// </summary>
        /// <param name="secondsUsed"></param>
        /// <param name="slot"></param>
        /// <param name="byEntity"></param>
        /// <param name="blockSel"></param>
        /// <param name="entitySel"></param>
        /// <returns>False if the interaction should be stopped. True if the interaction should continue</returns>
        public override bool OnHeldInteractStep(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (GetContentNutritionProperties(byEntity.World, slot.Itemstack, byEntity as Entity) == null) return false;

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

                Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ;
                pos.Y += byEntity.EyeHeight - 0.4f;

                if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
                {
                    ItemStack[] contents = GetContents(byEntity.World, slot.Itemstack);
                    ItemStack rndStack = contents[byEntity.World.Rand.Next(contents.Length)];

                    byEntity.World.SpawnCubeParticles(pos, rndStack, 0.3f, 4, 1);
                }

                return secondsUsed <= 1f;
            }

            // Let the client decide when he is done eating
            return true;
        }


        /// <summary>
        /// Called when the player successfully completed the using action, always called once an interaction is over
        /// </summary>
        /// <param name="secondsUsed"></param>
        /// <param name="slot"></param>
        /// <param name="byEntity"></param>
        /// <param name="blockSel"></param>
        /// <param name="entitySel"></param>
        public override void OnHeldInteractStop(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            FoodNutritionProperties[] multiProps = GetContentNutritionProperties(byEntity.World, slot.Itemstack, byEntity as Entity);

            if (byEntity.World.Side == EnumAppSide.Server && multiProps != null && secondsUsed >= 0.95f)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
                IPlayer player = (byEntity as EntityPlayer).Player;

                Block block = byEntity.World.GetBlock(new AssetLocation("bowl-burned"));
                if (player == null || !player.InventoryManager.TryGiveItemstack(new ItemStack(block), true))
                {
                    byEntity.World.SpawnItemEntity(new ItemStack(block), byEntity.LocalPos.XYZ);
                }
                
                foreach (var nutriProps in multiProps)
                {
                    player.Entity.ReceiveSaturation(nutriProps.Saturation, nutriProps.FoodCategory, 10 + nutriProps.Saturation / 100f * 30f);

                    if (nutriProps.EatenStack?.ResolvedItemstack != null)
                    {
                        if (player == null || !player.InventoryManager.TryGiveItemstack(nutriProps.EatenStack.ResolvedItemstack.Clone(), true))
                        {
                            byEntity.World.SpawnItemEntity(nutriProps.EatenStack.ResolvedItemstack.Clone(), byEntity.LocalPos.XYZ);
                        }
                    }

                    if (nutriProps.Health != 0)
                    {
                        byEntity.ReceiveDamage(new DamageSource() {
                            Source = EnumDamageSource.Internal,
                            Type = nutriProps.Health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                        }, Math.Abs(nutriProps.Health));
                    }
                }


            }
        }



        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
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

            

            if (GetContentNutritionProperties(world, stack, byPlayer.Entity) != null)
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
            if (!byPlayer.Entity.Controls.Sneak) return false;

            ItemStack stack = OnPickBlock(world, blockSel.Position);

            if (GetContentNutritionProperties(world, stack, byPlayer.Entity) == null) return false;

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
                    ItemStack[] contents = GetContents(world, stack);
                    ItemStack rndStack = contents[world.Rand.Next(contents.Length)];
                    world.SpawnCubeParticles(blockSel.Position.ToVec3d().Add(0.5f, 2/16f, 0.5f), rndStack, 0.2f, 4, 0.5f);
                }

                return secondsUsed <= 1.5f;
            }

            // Let the client decide when he is done eating
            return true;
        }


        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!byPlayer.Entity.Controls.Sneak) return;

            ItemStack stack = OnPickBlock(world, blockSel.Position);

            FoodNutritionProperties[] multiProps = GetContentNutritionProperties(world, stack, byPlayer.Entity);

            if (world.Side == EnumAppSide.Server && multiProps != null && secondsUsed >= 0.95f)
            {
                foreach (var nutriProps in multiProps)
                {
                    byPlayer.Entity.ReceiveSaturation(nutriProps.Saturation, nutriProps.FoodCategory, 10 + nutriProps.Saturation/100f * 30f);

                    if (nutriProps.EatenStack != null)
                    {
                        if (byPlayer == null || !byPlayer.InventoryManager.TryGiveItemstack(nutriProps.EatenStack.ResolvedItemstack.Clone(), true))
                        {
                            world.SpawnItemEntity(nutriProps.EatenStack.ResolvedItemstack.Clone(), blockSel.Position.ToVec3d().Add(0.5, 0, 0.5));
                        }
                    }

                    if (nutriProps.Health != 0)
                    {
                        byPlayer.Entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = nutriProps.Health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(nutriProps.Health));
                    }
                }

                Block block = world.GetBlock(new AssetLocation("bowl-burned"));
                world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position);
            }
        }

        public override FoodNutritionProperties GetNutritionProperties(IWorldAccessor world, ItemStack itemstack, Entity forEntity)
        {
            return null;
        }

        public FoodNutritionProperties[] GetContentNutritionProperties(IWorldAccessor world, ItemStack[] contentStacks, Entity forEntity)
        {
            List<FoodNutritionProperties> foodProps = new List<FoodNutritionProperties>();
            if (contentStacks == null) return foodProps.ToArray();

            for (int i = 0; i < contentStacks.Length; i++)
            {
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

                float? customSat = obj.Attributes?["saturationWhenInMeal"].AsFloat(-1);
                if (customSat != null && customSat >= 0)
                {
                    stackProps.Saturation = (float)customSat;
                }

                if (stackProps == null) continue;
                foodProps.Add(stackProps);
            }

            return foodProps.ToArray();
        }

        public FoodNutritionProperties[] GetContentNutritionProperties(IWorldAccessor world, ItemStack itemstack, Entity forEntity)
        {
            ItemStack[] stacks = GetContents(world, itemstack);
            if (stacks == null) return null;

            return GetContentNutritionProperties(world, stacks, forEntity);
        }


        public string GetContentNutritionFacts(IWorldAccessor world, ItemStack[] contentStacks, Entity forEntity)
        {
            FoodNutritionProperties[] props = GetContentNutritionProperties(world, contentStacks, forEntity);

            Dictionary<EnumFoodCategory, float> totalSaturation = new Dictionary<EnumFoodCategory, float>();
            float totalHealth = 0;

            for (int i = 0; i < props.Length; i++)
            {
                FoodNutritionProperties prop = props[i];
                if (prop == null) continue;

                float sat = 0;
                totalSaturation.TryGetValue(prop.FoodCategory, out sat);

                totalHealth += prop.Health;
                totalSaturation[prop.FoodCategory] = sat + prop.Saturation;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Nutrition Facts");

            foreach (var val in totalSaturation)
            {
                sb.AppendLine("- " + Lang.Get("" + val.Key) + ": " + val.Value + " sat.");
            }

            if (totalHealth != 0)
            {
                sb.AppendLine("- " + Lang.Get("Health: {0}{1} hp", totalHealth > 0 ? "+" : "", totalHealth));
            }

            return sb.ToString();
        }

        public string GetContentNutritionFacts(IWorldAccessor world, ItemStack itemstack, Entity forEntity)
        {
            return GetContentNutritionFacts(world, GetContents(world, itemstack), forEntity);
        }


        public void SetContents(string recipeCode, ItemStack containerStack, ItemStack[] stacks)
        {
            base.SetContents(containerStack, stacks);

            containerStack.Attributes.SetString("recipeCode", recipeCode);
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

            MeshRef meshref = meshCache.GetOrCreateMealMeshRef(this.Shape, GetCookingRecipe(capi.World, itemstack), GetContents(capi.World, itemstack));
            if (meshref != null) renderinfo.ModelRef = meshref;
        }



        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos);

            BlockEntityMeal bem = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMeal;

            if (bem != null)
            {
                SetContents(bem.RecipeCode, stack, bem.GetContentStacks());
            }

            return stack;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return new ItemStack[] { OnPickBlock(world, pos) };
        }

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            CookingRecipe recipe = GetCookingRecipe(world, stack);

            if (recipe != null)
            {
                dsc.AppendLine(recipe.GetOutputName(world, GetContents(world, stack)).UcFirst());
            }

            string facts = GetContentNutritionFacts(world, stack, null);

            if (facts != null)
            {
                dsc.Append(facts);
            }
        }


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            BlockEntityMeal bem = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMeal;
            if (bem == null) return base.GetRandomColor(capi, pos, facing);

            ItemStack[] stacks = bem.GetContentStacks(false);

            return GetRandomBlockColor(capi, stacks);
        }


        public int GetRandomBlockColor(ICoreClientAPI capi, ItemStack[] stacks)
        {
            ItemStack rndStack = stacks[capi.World.Rand.Next(stacks.Length)];
            return rndStack.Collectible.GetRandomColor(capi, rndStack);
        }
        
    }
}
