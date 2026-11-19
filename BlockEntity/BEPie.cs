using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumPiePartType
    {
        Crust, Filling, Topping
    }

    /// <summary>
    /// Defines the type of ingredient (crust, filling, topping), food category
    /// for mixing, and what mixing codes it can be used with, if any.
    /// </summary>
    [DocumentAsJson]
    public class InPieProperties
    {
        [DocumentAsJson("Required")]
        public required AssetLocation Texture;

        /// <summary>
        /// The shape to use if this is a topping.
        ///
        /// Toppings must have this property to be used.
        /// </summary>
        [DocumentAsJson("Optional")]
        public string? ToppingShapeElement = null;

        /// <summary>
        /// Is this filling allowed to mix with other ingredients?
        /// For toppings, set this to false to require mixing codes.
        /// Crusts always ignore this
        /// <br/><br/>
        /// For fillings MixingCodes has no effect when this is false
        /// because this ingredient cannot be combined with anything else.
        ///  Don't add mixing codes to fillings if this is disabled.
        /// </summary>
        [DocumentAsJson("Optional", "true")]
        public bool AllowMixing = true;

        /// <summary>
        /// Is this a crust, filling, or topping?
        /// <br/><br/>
        /// * Crust is used as the pie's base. The ingredient used to
        ///   create a pie must be a crust. Crust can also be used
        ///   as a topping and is not restricted by mixing codes.
        /// <br/>
        /// * Filling is what's inside the pie. AllowMixing determines
        ///   if it can only be used for single-ingredient pies. If it
        ///   can be mixed, only ingredients with at least one matching
        ///   mixing code can be added.
        ///
        ///   Pies with only one filling are always named after that ingredient.
        /// <br/>
        /// * Topping is the pie's last layer. Like filling, it is
        ///   restricted by mixing codes. Any crust may be used in place
        ///   of a topping without being restricted by mixing codes.
        /// </summary>
        [DocumentAsJson("Required")]
        public required EnumPiePartType PartType;

        /// <summary>
        /// The food category of the ingredient when used in a pie. This does
        /// not affect the actual nutrition when consumed. It is only used
        /// to determine which category mixing code to prepend in the case
        /// that the code is not already present.
        /// <br/><br/>
        /// An ingredient of the NoNutrition category cannot be added to pies unless
        /// it has at least one explicit mixing code or mixing has been disabled.
        /// NoNutrition may be used explicitly, skipping the process below.
        /// <br/><br/>
        /// Checks in order, stopping once a value is found:
        /// <br/><br/>
        ///   1. inPieProperties["foodCategory"]
        /// <br/>
        ///   2. If stack has WaterTightContainableProps:
        /// <br/>
        ///   - 2a. NutritionPropsPerLitreWhenInMeal.FoodCategory
        /// <br/>
        ///   - 2b. NutritionPropsPerLitre.FoodCategory
        /// <br/>
        ///   3. If stack has NutritionProps:
        /// <br/>
        ///   - 3a. NutritionPropsWhenInMeal.FoodCategory
        /// <br/>
        ///   - 3b. NutritionProps.FoodCategory
        /// <br/>
        ///   4. Use NoNutrition. If no nutrition props are present,
        ///      the InPieProperties are always null.
        /// </summary>
        [DocumentAsJson("Optional")]
        public EnumFoodCategory FoodCategory = EnumFoodCategory.NoNutrition;

        /// <summary>
        /// A list of mixing codes that are allowed for this ingredient. When
        /// checking for mixing codes, the first mixing code present in all
        /// ingredients is used for the pie type. Mixing codes do not apply
        /// to crust ingredients and will be ignored.
        /// <br/><br/>
        /// If the ingredient's food category is not NoNutrition and the category
        /// code is not already present, it will be prepended. This means that by
        /// default, mixing codes are of a lower priority than the food category.
        /// <br/>
        /// [ "potpie" ] -> [ "vegetable", "potpie" ]
        /// <br/><br/>
        /// By including the food category in the list of mixing codes, other codes
        /// can be given a higher priority. For example, the following would create
        /// a "mushroom" mixing code that would take precedence over "vegetable".
        /// <br/>
        /// [ "mushroom", "vegetable", "potpie" ]
        /// <br/><br/>
        /// An ingredient may have multiple food category mixing codes. It will be
        /// allowed in any of those mixed pies. Other mixing codes will not be
        /// affected.
        /// <br/><br/>
        /// If MixingCodes is empty, the food category code will always be added.
        /// It is an error for a NoNutrition ingredient to have no mixing codes
        /// unless it cannot be mixed.
        /// </summary>
        [DocumentAsJson("Optional", "[]")]
        public string[] MixingCodes = [];

        /// <summary>
        /// For items; The number of items in the stack used for one layer.
        /// </summary>
        [DocumentAsJson("Optional", "2")]
        public int PortionSize = 2;

        /// <summary>
        /// For liquids; The number of liters used for one layer.
        /// </summary>
        [DocumentAsJson("Optional", "0.1")]
        public float PortionSizeLitres = 0.1f;

        public bool IsLiquid = false;

        /// <summary>
        /// The appropriate portion size based on whether the ingredient is an item or liquid.
        /// </summary>
        public float GetPortionSize() => IsLiquid ? PortionSizeLitres : PortionSize;

        public float ItemsPerLitre = 100f;

        /// <summary>
        /// The number of actual content items per portion.
        /// </summary>
        public int ItemsPerPortion() => IsLiquid ? (int)(PortionSizeLitres * ItemsPerLitre) : PortionSize;

        /// <summary>
        /// Read pie properties from Attributes.
        /// </summary>
        /// <returns>Null if "inPieProperties" is malformed or does not exist.</returns>
        public static InPieProperties? ReadFrom(CollectibleObject? obj, out string? errMessage)
        {
            errMessage = null;

            if (obj?.Attributes == null) return null;
            var props = obj.Attributes["inPieProperties"].AsObject<InPieProperties>(null, obj.Code.Domain);
            if (props == null) return null;

            WaterTightContainableProps? liquidProps = BlockLiquidContainerBase.GetContainableProps(new ItemStack(obj));
            FoodNutritionProperties? nutriProps = obj.GetNutritionProperties(null, null, null);
            FoodNutritionProperties? nutriPropsInMeal = obj.Attributes["nutritionPropsWhenInMeal"].AsObject<FoodNutritionProperties>();

            // This ingredient has no nutrition properties at all, so it would do nothing in a pie.
            if (nutriProps == null && nutriPropsInMeal == null && (liquidProps == null || (liquidProps.NutritionPropsPerLitre == null && liquidProps.NutritionPropsPerLitreWhenInMeal == null)))
            {
                errMessage = $"{obj.Code} has inPieProperties, but no nutrition properties. It cannot be used in pies.";
                return null;
            }

            // Do not attempt to overwrite NoNutrition if it was set manually, only if it was defaulted to.
            if (!obj.Attributes["inPieProperties"]["foodCategory"].Exists)
            {
                // Get the food category manually. It doesn't need to be present in the pie properties.
                // We do this handling here to avoid making the field unnecessarily nullable.

                EnumFoodCategory? foodCat = null;

                if (liquidProps != null)
                {
                    foodCat ??= liquidProps.NutritionPropsPerLitreWhenInMeal?.FoodCategory;
                    foodCat ??= liquidProps.NutritionPropsPerLitre?.FoodCategory;
                }

                foodCat ??= nutriPropsInMeal?.FoodCategory;
                foodCat ??= nutriProps?.FoodCategory;

                // This ingredient has no food category at all, so it would do nothing in a pie.
                if (foodCat == null)
                {
                    errMessage = $"{obj.Code} has inPieProperties and nutrition properties, but no food category. It cannot be used in pies.";
                    return null;
                }

                props.FoodCategory = foodCat.Value;
            }

            if (liquidProps != null)
            {
                props.IsLiquid = true;
                props.ItemsPerLitre = liquidProps.ItemsPerLitre;
            }

            props.MixingCodes = props.MixingCodes.Select(code => code.ToLowerInvariant()).ToArray();

            // Never add the code for NoNutrition.
            if (props.FoodCategory != EnumFoodCategory.NoNutrition)
            {
                // Add the food category code if there are no mixing codes or if it wasn't explicitly added.
                string foodCatCode = props.FoodCategory.ToString().ToLowerInvariant();
                if (props.MixingCodes.Length == 0 || !HasMixingCode(props, foodCatCode))
                {
                    props.MixingCodes = props.MixingCodes.Prepend(foodCatCode).ToArray();
                }
            }

            // Nonsensical quantity.
            if (props.GetPortionSize() <= 0)
            {
                errMessage = $"inPieProperties for {obj.Code} has a portion size of 0 or less. It cannot be used in pies.";
                return null;
            }

            // Filling can be mixed, but nothing is compatible.
            if (props.PartType == EnumPiePartType.Filling && props.AllowMixing && props.MixingCodes.Length == 0 && props.FoodCategory == EnumFoodCategory.NoNutrition)
            {
                errMessage = $"InPieProperties for filling {obj.Code} has no mixing codes and the food category NoNutrition. It cannot be added to pies unless you explicitly disable mixing.";
                return null;
            }

            // Ingredient can be mixed, but nothing is compatible.
            if (props.PartType == EnumPiePartType.Topping && !props.AllowMixing && props.MixingCodes.Length == 0 && props.FoodCategory == EnumFoodCategory.NoNutrition)
            {
                errMessage = $"InPieProperties for topping {obj.Code} has no mixing codes and the food category NoNutrition. It cannot be added to pies unless you explicitly enable mixing.";
                return null;
            }

            // Crusts cannot be liquid
            if (props.PartType == EnumPiePartType.Crust && props.IsLiquid)
            {
                errMessage = $"Pie ingredient {obj.Code} is a liquid, but it is marked as crust. Ignoring invalid dough.";
                return null;
            }

            if (props.PartType == EnumPiePartType.Topping && props.ToppingShapeElement == null)
            {
                errMessage = $"InPieProperties for pie topping {obj.Code} does not have a configured toppingShapeElement. It must have a shape.";
                return null;
            }

            return props;
        }

        /// <summary>
        /// Read pie properties from Attributes.
        /// </summary>
        /// <returns>Null if the ingredient is unusable or "inPieProperties" does not exist.</returns>
        public static InPieProperties? ReadFrom(CollectibleObject? obj)
        {
            return obj != null ? ReadFrom(obj, out _) : null;
        }

        /// <summary>
        /// Read pie properties from ItemAttributes.
        /// </summary>
        /// <returns>Null if the ingredient is unusable or "inPieProperties" does not exist.</returns>
        public static InPieProperties? ReadFrom(ItemStack? stack)
        {
            return ReadFrom(stack?.Collectible);
        }

        public static bool HasMixingCode(ItemStack? stack, string? mixCode)
        {
            if (stack == null) return false;
            return HasMixingCode(ReadFrom(stack.Collectible), mixCode);
        }

        public static bool HasMixingCode(InPieProperties? pieProps, string? mixCode)
        {
            return pieProps?.MixingCodes.Contains(mixCode) ?? false;
        }

        public static string[] GetSharedMixingCodes(ItemStack? stack, string[] mixCodes)
        {
            if (stack == null) return [];
            return GetSharedMixingCodes(ReadFrom(stack.Collectible), mixCodes);
        }

        public static string[] GetSharedMixingCodes(InPieProperties? pieProps, string[] mixCodes)
        {
            return [..pieProps?.MixingCodes.Intersect(mixCodes) ?? []];
        }
    }

    /// <summary>
    /// A single-slot inventory that hold a <see cref="BlockPie" /> ItemStack. The pie itemstack
    /// is a container with 6 slots. This makes it easy to convert it to a normal pie ItemStack.
    ///
    /// GetContents() must always return an ItemStack[] of length 6. Unused or empty slots are represented as null.
    ///
    /// [0]: Base dough
    /// [1-4]: Filling
    /// [5]: Crust dough
    /// </summary>
    public class BlockEntityPie : BlockEntityContainer
    {
        InventoryGeneric inv = new(1, null, null);
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "pie";

        public BlockPie? PieBlock => inv[0].Itemstack?.Block as BlockPie;

        public ItemStack?[]? PieContents => PieBlock?.GetContents(Api.World, inv[0].Itemstack);

        public bool HasAnyFilling
        {
            get
            {
                ItemStack?[]? cStacks = PieContents;
                if (cStacks == null) return false;
                return cStacks[1] != null || cStacks[2] != null || cStacks[3] != null || cStacks[4] != null;
            }
        }

        public bool HasAllFilling
        {
            get
            {
                ItemStack?[]? cStacks = PieContents;
                if (cStacks == null) return false;
                return cStacks[1] != null && cStacks[2] != null && cStacks[3] != null && cStacks[4] != null;
            }
        }

        /// <summary>
        /// If this pie's topping exists, is it a Topping or a Crust?
        /// </summary>
        public EnumPiePartType? ToppingType => InPieProperties.ReadFrom(PieContents?[5])?.PartType;

        public string? State => PieBlock?.State;



        MealMeshCache? ms;
        MeshData? mesh;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            var contents = PieContents;
            if (contents != null)
            {
                // Make sure all our ingredients are time frozen because pies themselves spoil rather than their ingredients
                foreach (var stack in contents) stack?.Attributes.SetBool("timeFrozen", true);
                PieBlock?.SetContents(inv[0].Itemstack, contents);
            }

            if (api.Side != EnumAppSide.Client) return;

            ms = api.ModLoader.GetModSystem<MealMeshCache>() ?? throw new Exception("Expected the meal mesh cache mod system to be available");
            loadMesh();
        }

        protected override void OnTick(float dt)
        {
            base.OnTick(dt);

            if (inv[0].Itemstack?.Collectible.Code.Path == "rot")
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
                Api.World.SpawnItemEntity(inv[0].Itemstack, Pos.ToVec3d().Add(0.5, 0.1, 0.5));
            }
        }

        public override void OnBlockPlaced(ItemStack? byItemStack = null)
        {
            if (byItemStack != null)
            {
                inv[0].Itemstack = byItemStack.Clone();
                inv[0].Itemstack!.StackSize = 1;
            }
        }

        public int SlicesLeft => inv[0].Itemstack?.Attributes.GetAsInt("pieSize") ?? 0;

        /// <summary>
        /// Take a slice from the pie and set pieSize and quantityServings.
        ///
        /// Once sliced, a pie is no longer bakeable.
        /// </summary>
        public ItemStack? TakeSlice()
        {
            var stack = inv[0].Itemstack?.Clone();
            if (stack == null) return null;

            ItemStack? outStack = BlockPie.TakeSlice(ref stack);
            if (stack != null) inv[0].Itemstack = stack;
            else Api.World.BlockAccessor.SetBlock(0, Pos);
            loadMesh();
            MarkDirty(true);

            return outStack;
        }

        public void OnPlaced(IPlayer? byPlayer)
        {
            if (byPlayer == null) return;
            ItemSlot? hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (hotbarSlot?.Itemstack == null) return;
            ItemStack doughStack = hotbarSlot.Itemstack.Clone();
            InPieProperties? pieProps = InPieProperties.ReadFrom(doughStack);
            if (pieProps?.PartType != EnumPiePartType.Crust) return;

            doughStack.StackSize = pieProps.PortionSize;

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                hotbarSlot.TakeOut(pieProps.PortionSize);
            }

            ItemStack pie = new(Block);

            // Copy over the transition states so that we don't make a completely fresh pie from spoiling dough
            var doughStates = doughStack.Collectible.UpdateAndGetTransitionStates(byPlayer.Entity.World, new DummySlot(doughStack));
            var pieStates = pie.Collectible.UpdateAndGetTransitionStates(byPlayer.Entity.World, new DummySlot(pie));
            if (doughStates != null && pieStates != null)
            {
                for (int i = 0; i < doughStates.Length; i++)
                {
                    if (doughStates[i].TransitionLevel > 0)
                    {
                        float scaledHours = pieStates[i].FreshHours + pieStates[i].TransitionHours * doughStates[i].TransitionLevel;

                        if (Api.Side.IsServer()) Api.Logger.Debug($"Scaled spoiling dough lifetime to pie; {pieStates[i].FreshHours} + {pieStates[i].TransitionHours} * {doughStates[i].TransitionLevel}");

                        pie.Collectible.SetTransitionState(pie, doughStates[i].Props.Type, scaledHours);
                    }
                    else
                    {
                        float scaledHours = doughStates[i].TransitionedHours / (pieStates[i].TransitionHours / doughStates[i].TransitionHours);

                        if (Api.Side.IsServer()) Api.Logger.Debug($"Scaled fresh dough lifetime to pie; {doughStates[i].TransitionedHours} / ({pieStates[i].TransitionHours} / {doughStates[i].TransitionHours}) = {scaledHours}");

                        pie.Collectible.SetTransitionState(pie, doughStates[i].Props.Type, scaledHours);
                    }
                }
            }

            pie.Attributes.SetInt("pieSize", 4);
            pie.Attributes.SetBool("bakeable", false);
            if (State != "raw" && !pie.Attributes.HasAttribute("quantityServings"))
            {
                pie.Attributes.SetFloat("quantityServings", pie.Attributes.GetAsInt("pieSize") * 0.25f);
            }
            doughStack.Attributes.SetBool("timeFrozen", true);
            (pie.Block as BlockPie)?.SetContents(pie, [doughStack, null, null, null, null, null]);
            inv[0].Itemstack = pie;

            loadMesh();
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            if (PieBlock == null) return false;
            ItemStack pieStack = inv[0].Itemstack!; // If PieBlock isn't null this won't be either

            var invManager = byPlayer.InventoryManager;
            var hotbarSlot = invManager.ActiveHotbarSlot;
            var playerEntity = byPlayer.Entity;
            if (hotbarSlot == null || playerEntity == null) return false;
            var world = playerEntity.World;

            if (invManager.ActiveTool is EnumTool.Knife or EnumTool.Sword)
            {
                if (PieBlock.State != "raw")
                {
                    if (world.Side == EnumAppSide.Client)
                    {
                        MarkDirty(true);
                        return true;
                    }

                    ItemStack? sliceStack = TakeSlice();
                    if (sliceStack != null)
                    {
                        hotbarSlot.Itemstack?.Collectible.DamageItem(world, playerEntity, hotbarSlot);
                        if (!invManager.TryGiveItemstack(sliceStack))
                        {
                            world.SpawnItemEntity(sliceStack, Pos);
                        }
                        world.Logger.Audit("{0} Took 1x{1} slice from Pie at {2}.",
                            byPlayer.PlayerName,
                            sliceStack.Collectible.Code,
                            Pos
                        );
                    }

                    MarkDirty(true);
                }
                else if (ToppingType == EnumPiePartType.Crust)
                {
                    // Cycle top crust type
                    inv[0].Itemstack = BlockPie.CycleTopCrustType(pieStack);
                    loadMesh();
                    MarkDirty(true);
                }

                return true;
            }

            // Filling rules:
            // 1. Get inPieProperties
            // 2. If pie is empty, add it.
            // 3. If pie is full, stop.
            // 3. If pie is partially full, must
            //    a.) Have props.AllowMixing set to true
            //    b.) Have at least one matching mixing code

            // If the pie can be picked up into the current hotbar slot,
            // skip trying to add the held stack as filling. Prevents
            // the "cannot be added to pies" error message.
            if (!hotbarSlot.Empty && PieBlock.State == "raw" && pieStack.Collectible.GetMergableQuantity(hotbarSlot.Itemstack, pieStack, EnumMergePriority.DirectMerge) <= 0)
            {
                if (ToppingType == EnumPiePartType.Crust && InPieProperties.ReadFrom(hotbarSlot.Itemstack)?.PartType == EnumPiePartType.Crust)
                {
                    inv[0].Itemstack = BlockPie.CycleTopCrustType(pieStack);
                }
                if (!TryAddIngredientFrom(hotbarSlot, byPlayer)) return false;

                loadMesh();
                inv[0].Itemstack?.Attributes.SetBool("bakeable", HasAllFilling);
                MarkDirty(true);
                return true;
            }

            if (playerEntity.Controls.ShiftKey) return false;

            if (world.Side == EnumAppSide.Client) return true;

            if (SlicesLeft == 1 && !pieStack.Attributes.HasAttribute("quantityServings"))
            {
                pieStack.Attributes.SetBool("bakeable", false);
                pieStack.Attributes.SetFloat("quantityServings", 0.25f);
            }

            if (!invManager.TryGiveItemstack(pieStack))
            {
                world.SpawnItemEntity(pieStack, Pos.ToVec3d().Add(0.5, 0.25, 0.5));
            }
            world.Logger.Audit("{0} Took 1x{1} at {2}.",
                byPlayer.PlayerName,
                pieStack.Collectible.Code,
                Pos
            );
            inv[0].Itemstack = null;

            world.BlockAccessor.SetBlock(0, Pos);
            MarkDirty(true);

            return true;
        }

        /// <summary>
        /// CanAddIngredient without error handling for a raw "yes or no" answer
        /// </summary>
        /// <param name="stack"></param>
        /// <returns></returns>
        public bool CanAddIngredient(ItemStack stack)
        {
            return CanAddIngredient(stack, out _, out _, out _);
        }

        /// <summary>
        /// Check if the given ItemStack can be added to this pie.
        /// <br/><br/>
        /// Does not add the ingredient. See <see cref="TryAddIngredientFrom" />
        /// </summary>
        /// <param name="stack">The item to add to the pie.</param>
        /// <param name="emptySlotIndex">If the ingredient can be added, the slot into which it would be placed.</param>
        /// <param name="errCode">If the ingredient cannot be added, the error code describing what went wrong.</param>
        /// <param name="errMessage">If the ingredient cannot be added, the localized error message to display.</param>
        /// <returns>True if the stack can be added to the pie. Note that if the action should succeed, but no stack
        /// is actually added, such as changing crust type, it will return true but leave emptySlotIndex null.</returns>
        public bool CanAddIngredient(ItemStack stack, out int emptySlotIndex, out string? errCode, out string? errMessage)
        {
            errCode = null;
            errMessage = null;
            emptySlotIndex = -1;

            ILiquidSource? liquidSource = stack.Collectible.GetCollectibleInterface<ILiquidSource>();
            ItemStack inputStack = stack;
            if (liquidSource != null)
            {
                var cStack = liquidSource.GetContent(stack);
                if (!liquidSource.AllowHeldLiquidTransfer || cStack == null)
                {
                    errCode = "notpieable";
                    errMessage = Lang.Get("piemaking-unabletoaddingredient");
                    return false;
                }

                inputStack = cStack;
            }

            var inputPieProps = InPieProperties.ReadFrom(inputStack);
            if (inputPieProps == null)
            {
                errCode = "notpieable";
                errMessage = Lang.Get("piemaking-unabletoaddingredient");
                return false;
            }

            float totalPortions = inputStack.StackSize / (float)inputPieProps.ItemsPerPortion();
            if (totalPortions < 1)
            {
                errCode = "notenoughingredients";
                errMessage = Lang.Get(liquidSource != null ? "piemaking-notenoughliquid" : "piemaking-notenoughitems", inputPieProps.GetPortionSize(), inputStack.GetName());
                return false;
            }

            if (PieBlock == null) return false;

            ItemStack?[] cStacks = PieContents!; // If PieBlock isn't null this won't be either

            if (cStacks[5] != null)
            {
                errCode = "piefinished";
                errMessage = Lang.Get("piemaking-alreadycomplete");
                return false;
            }

            switch (HasAllFilling)
            {
                case true when inputPieProps.PartType == EnumPiePartType.Filling:
                    errCode = "piefullfilling";
                    errMessage = Lang.Get("piemaking-hasallfillings");
                    return false;
                case true when inputPieProps.PartType == EnumPiePartType.Crust:
                    emptySlotIndex = 5;
                    return true;
                case false when inputPieProps.PartType != EnumPiePartType.Filling:
                    errCode = "pieneedsfilling";
                    errMessage = Lang.Get("piemaking-mustaddfillingnext");
                    return false;
            }

            if (!HasAnyFilling)
            {
                emptySlotIndex = 1;
                return true;
            }

            InPieProperties?[] stackPieProps = cStacks.Select(cStack =>
            {
                InPieProperties? pieProps = InPieProperties.ReadFrom(cStack);
                if (cStack != null && pieProps == null)
                {
                    // An ingredient already in a pie is missing its inPieProperties
                    BlockPie.ReportMissingPieProps(Api.Logger, cStack.Collectible.Code);
                }
                return pieProps;
            }).ToArray();

            bool singleIngredient = true;
            bool allowMixing = inputPieProps.AllowMixing;
            string[] mixCodes = inputPieProps.MixingCodes;

            // Note that we check the topping slot here because non-crust toppings are restricted by mixing codes.
            for (int i = 1; i < cStacks.Length; i++)
            {
                if (cStacks[i] == null) break;

                singleIngredient &= cStacks[i]!.Equals(Api.World, stack, GlobalConstants.IgnoredStackAttributes);

                if (stackPieProps[i] != null)
                {
                    allowMixing &= stackPieProps[i]!.AllowMixing || inputPieProps.PartType == EnumPiePartType.Topping;
                    mixCodes = InPieProperties.GetSharedMixingCodes(stackPieProps[i], mixCodes);
                }

                if (!singleIngredient && mixCodes.Length == 0) break;
            }


            if (!singleIngredient)
            {
                if (!allowMixing)
                {
                    errCode = "pienonmixable";
                    errMessage = Lang.Get("piemaking-mixingnotallowed");
                    return false;
                }
                if (mixCodes.Length == 0)
                {
                    switch (inputPieProps.PartType)
                    {
                        case EnumPiePartType.Filling:
                            errCode = "piemismatchedmix";
                            errMessage = Lang.Get("piemaking-unabletomixingredient");
                            return false;
                        case EnumPiePartType.Topping when !inputPieProps.AllowMixing:
                            errCode = "piemismatchedtopping";
                            errMessage = Lang.Get("piemaking-unabletoaddtopping");
                            return false;
                    }
                }
            }

            if (cStacks[4] != null) emptySlotIndex = 5;
            else if (cStacks[3] != null) emptySlotIndex = 4;
            else if (cStacks[2] != null) emptySlotIndex = 3;
            else emptySlotIndex = 2;

            return true;
        }

        /// <summary>
        /// Try to actually add an ingredient. Calls CanAddIngredient and emits error information in-game.
        /// </summary>
        /// <param name="slot">The player's held item slot.</param>
        /// <param name="byPlayer">The player adding the item.</param>
        /// <returns>True if the ingredient was added successfully. False if it failed.</returns>
        private bool TryAddIngredientFrom(ItemSlot slot, IPlayer byPlayer)
        {
            ICoreClientAPI? capi = Api as ICoreClientAPI;
            ItemSlot takeFromSlot = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative ? new DummySlot(slot.Itemstack?.Clone()) : slot;

            if (PieBlock == null || takeFromSlot.Itemstack == null) return false;

            if (!CanAddIngredient(takeFromSlot.Itemstack, out int emptySlotIndex, out string? errCode, out string? errMessage))
            {
                capi?.TriggerIngameError(this, errCode, errMessage);
                return false;
            }

            ILiquidSource? liquidSource = takeFromSlot.Itemstack.Collectible.GetCollectibleInterface<ILiquidSource>();
            ItemStack inputStack = takeFromSlot.Itemstack;
            ItemStack? cStack = liquidSource?.GetContent(takeFromSlot.Itemstack);
            if (cStack != null) inputStack = cStack;

            // CanAddIngredient already made sure the pie props are valid
            InPieProperties inputPieProps = InPieProperties.ReadFrom(inputStack)!;

            if (liquidSource != null)
            {
                var taken = liquidSource.TryTakeContent(takeFromSlot.Itemstack, inputPieProps.ItemsPerPortion());
                if (taken == null || taken.StackSize < inputPieProps.ItemsPerPortion())
                {
                    Api.Logger.Error($"BEPie.TryAddIngredientFrom expected at least {inputPieProps.ItemsPerPortion()} liquid items, but there were only {takeFromSlot.Itemstack.StackSize}. There is likely a bug either here or in CanAddIngredient.");
                    return false;
                }

                inputStack = taken;
            }
            else inputStack = takeFromSlot.TakeOut(inputPieProps.ItemsPerPortion());
            takeFromSlot.MarkDirty();

            var cStacks = PieContents!; // If PieBlock isn't null this won't be either
            int ingredientCount = cStacks.Count(stack => stack != null);

            // Average transition states before adding
            var ingStates = inputStack.Collectible.UpdateAndGetTransitionStates(byPlayer.Entity.World, new DummySlot(inputStack));
            var pieStates = PieBlock.UpdateAndGetTransitionStates(byPlayer.Entity.World, inv[0]);
            if (ingStates != null && pieStates != null)
            {
                float t = (float)1 / (1 + ingredientCount);
                for (int i = 0; i < ingStates.Length; i++)
                {
                    float totalIngHours = ingStates[i].FreshHours + ingStates[i].TransitionHours;
                    float totalPieHours = pieStates[i].FreshHours + pieStates[i].TransitionHours;
                    float scaledIngTransitionedHours = ingStates[i].TransitionedHours / (totalIngHours / totalPieHours);

                    var avgTransitionedHours = scaledIngTransitionedHours * t + pieStates[i].TransitionedHours * (1 - t);
                    PieBlock.SetTransitionState(inv[0].Itemstack, ingStates[i].Props.Type, avgTransitionedHours);
                }
            }

            inputStack.Attributes.SetBool("timeFrozen", true);
            cStacks[emptySlotIndex] = inputStack;
            PieBlock.SetContents(inv[0].Itemstack, cStacks);
            if (inputPieProps.PartType == EnumPiePartType.Crust && emptySlotIndex == 5)
            {
                // Crust attribute must exist to stack together
                inv[0].Itemstack!.Attributes.SetString("topCrustType", "full");
            }

            return true;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (inv[0].Empty || mesh == null) return true;
            mesher.AddMeshData(mesh);
            return true;
        }

        void loadMesh()
        {
            if (Api?.Side != EnumAppSide.Client) return;
            mesh = ms!.GetPieMesh(inv[0].Itemstack);
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
                loadMesh();
                MarkDirty(true);
            }
        }

        public override void OnBlockBroken(IPlayer? byPlayer = null)
        {
            //base.OnBlockBroken(); - dont drop inventory contents, the GetDrops() method already handles pie dropping
        }
    }
}
