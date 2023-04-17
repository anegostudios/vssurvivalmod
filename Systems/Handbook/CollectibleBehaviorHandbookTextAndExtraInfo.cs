using Cairo;
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
    public interface ICustomHandbookPageContent
    {
        void OnHandbookPageComposed(List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor);
    }

    public class CollectibleBehaviorHandbookTextAndExtraInfo : CollectibleBehavior
    {
        public ExtraHandbookSection[] ExtraHandBookSections = null;

        ICoreAPI Api;

        public CollectibleBehaviorHandbookTextAndExtraInfo(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.Api = api;

            JsonObject obj = collObj.Attributes?["handbook"]?["extraSections"];
            if (obj != null && obj.Exists)
            {
                ExtraHandBookSections = obj?.AsObject<ExtraHandbookSection[]>();
            }
        }



        /// <summary>
        /// Detailed information on this block/item to be displayed in the handbook
        /// </summary>
        /// <param name="inSlot"></param>
        /// <param name="capi"></param>
        /// <param name="allStacks">An itemstack for every block and item that should be considered during information display</param>
        /// <param name="openDetailPageFor">Callback when someone clicks a displayed itemstack</param>
        /// <returns></returns>
        public virtual RichTextComponentBase[] GetHandbookInfo(ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            ItemStack stack = inSlot.Itemstack;

            List<RichTextComponentBase> components = new List<RichTextComponentBase>();

            components.Add(new ItemstackTextComponent(capi, stack, 100, 10, EnumFloat.Left));
            components.AddRange(VtmlUtil.Richtextify(capi, stack.GetName() + "\n", CairoFont.WhiteSmallishText()));
            var font = CairoFont.WhiteDetailText();
            if (capi.Settings.Bool["extendedDebugInfo"] == true)
            {
                font.Color[3] = 0.5;
                components.AddRange(VtmlUtil.Richtextify(capi, "Page code:" + GuiHandbookItemStackPage.PageCodeForStack(stack) + "\n", font));
            }
            components.AddRange(VtmlUtil.Richtextify(capi, stack.GetDescription(capi.World, inSlot), CairoFont.WhiteSmallText()));

            float marginTop = 7;
            float marginBottom = 3;


            components.Add(new ClearFloatTextComponent(capi, marginTop));


            List<ItemStack> breakBlocks = new List<ItemStack>();

            foreach (var blockStack in allStacks)
            {
                if (blockStack.Block == null) continue;

                BlockDropItemStack[] droppedStacks = blockStack.Block.GetDropsForHandbook(blockStack, capi.World.Player);
                if (droppedStacks == null) continue;

                for (int i = 0; i < droppedStacks.Length; i++)
                {
                    BlockDropItemStack dstack = droppedStacks[i];
                    ItemStack droppedStack = droppedStacks[i].ResolvedItemstack;

                    if (droppedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                    {
                        breakBlocks.Add(blockStack);
                    }
                }
            }


            #region Drops when broken
            if (stack.Class == EnumItemClass.Block)
            {
                BlockDropItemStack[] blockdropStacks = stack.Block.GetDropsForHandbook(stack, capi.World.Player);
                List<ItemStack[]> breakBlocksWithToolStacks = new List<ItemStack[]>();
                List<EnumTool?> tools = new List<EnumTool?>();
                List<ItemStack> dropsStacks = new List<ItemStack>();

                if (blockdropStacks != null)
                {
                    foreach (var val in blockdropStacks)
                    {
                        dropsStacks.Add(val.ResolvedItemstack);

                        ItemStack[] toolStacks = val.Tool == null ? null : ObjectCacheUtil.GetOrCreate(capi, "blockhelp-collect-withtool-" + val.Tool, () =>
                        {
                            List<ItemStack> tstacks = new List<ItemStack>();
                            foreach (var colobj in capi.World.Collectibles)
                            {
                                if (colobj.Tool == val.Tool)
                                {
                                    tstacks.Add(new ItemStack(colobj));
                                }
                            }
                            return tstacks.ToArray();
                        });

                        tools.Add(val.Tool);
                        breakBlocksWithToolStacks.Add(toolStacks);
                    }
                }

                if (dropsStacks != null && dropsStacks.Count > 0)
                {
                    if (dropsStacks.Count == 1 && breakBlocks.Count == 1 && breakBlocks[0].Equals(capi.World, dropsStacks[0], GlobalConstants.IgnoredStackAttributes))
                    {
                        // No need to display the same info twice
                    }
                    else
                    {
                        components.Add(new RichTextComponent(capi, Lang.Get("Drops when broken") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                        int i = 0;
                        while (dropsStacks.Count > 0)
                        {
                            ItemStack dstack = dropsStacks[0];
                            EnumTool? tool = tools[i];
                            ItemStack[] toolStacks = breakBlocksWithToolStacks[i++];
                            dropsStacks.RemoveAt(0);

                            if (dstack == null) continue;

                            SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, dropsStacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                            if (toolStacks != null)
                            {
                                comp.ExtraTooltipText = "\n\n<font color=\"orange\">" + Lang.Get("break-requires-tool-" + tool.ToString().ToLowerInvariant()) + "</font>";
                            }
                            components.Add(comp);

                            if (toolStacks != null)
                            {
                                comp = new SlideshowItemstackTextComponent(capi, toolStacks, 24, EnumFloat.Left, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                                comp.renderOffset.X = -(float)GuiElement.scaled(17);
                                comp.renderOffset.Z = 100;

                                comp.ShowTooltip = false;
                                components.Add(comp);
                            }


                        }

                        components.Add(new ClearFloatTextComponent(capi, marginTop));
                    }
                }
            }
            #endregion

            #region Obtained through
            List<string> killCreatures = new List<string>();

            foreach (var val in capi.World.EntityTypes)
            {
                if (val.Drops == null) continue;

                for (int i = 0; i < val.Drops.Length; i++)
                {
                    if (val.Drops[i].ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                    {
                        killCreatures.Add(Lang.Get(val.Code.Domain + ":item-creature-" + val.Code.Path));
                    }
                }
            }


            bool haveText = false;            

            if (killCreatures.Count > 0)
            {
                components.Add(new RichTextComponent(capi, Lang.Get("Obtained by killing") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new RichTextComponent(capi, string.Join(", ", killCreatures) + "\n", CairoFont.WhiteSmallText()));
                haveText = true;
            }

            if (breakBlocks.Count > 0)
            {
                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("Obtained by breaking") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                while (breakBlocks.Count > 0)
                {
                    ItemStack dstack = breakBlocks[0];
                    breakBlocks.RemoveAt(0);
                    if (dstack == null) continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, breakBlocks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    components.Add(comp);
                }

                haveText = true;
            }

            
            #endregion

            #region Found In
            string customFoundIn = stack.Collectible.Attributes?["handbook"]?["foundIn"]?.AsString(null);
            if (customFoundIn != null)
            {
                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("Found in") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new RichTextComponent(capi, Lang.Get(customFoundIn), CairoFont.WhiteSmallText()));
                haveText = true;
            }

            if (collObj.Attributes?["hostRockFor"].Exists == true)
            {
                int[] blockids = collObj.Attributes?["hostRockFor"].AsArray<int>();

                OrderedDictionary<string, List<ItemStack>> blocks = new OrderedDictionary<string, List<ItemStack>>();

                for (int i = 0; i < blockids.Length; i++)
                {
                    Block block = capi.World.Blocks[blockids[i]];

                    string key = block.Code.ToString();
                    if (block.Attributes?["handbook"]["groupBy"].Exists == true)
                    {
                        key = block.Attributes["handbook"]["groupBy"].AsArray<string>()[0];
                    }

                    if (!blocks.ContainsKey(key))
                    {
                        blocks[key] = new List<ItemStack>();
                    }

                    blocks[key].Add(new ItemStack(block));
                }

                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("Host rock for") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                foreach (var val in blocks)
                {
                    components.Add(new SlideshowItemstackTextComponent(capi, val.Value.ToArray(), 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                }

                haveText = true;
            }


            if (collObj.Attributes?["hostRock"].Exists == true)
            {
                ushort[] blockids = collObj.Attributes?["hostRock"].AsArray<ushort>();

                OrderedDictionary<string, List<ItemStack>> blocks = new OrderedDictionary<string, List<ItemStack>>();

                for (int i = 0; i < blockids.Length; i++)
                {
                    Block block = capi.World.Blocks[blockids[i]];

                    string key = block.Code.ToString();
                    if (block.Attributes?["handbook"]["groupBy"].Exists == true)
                    {
                        key = block.Attributes["handbook"]["groupBy"].AsArray<string>()[0];
                    }

                    if (!blocks.ContainsKey(key))
                    {
                        blocks[key] = new List<ItemStack>();
                    }

                    blocks[key].Add(new ItemStack(block));
                }

                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("Occurs in host rock") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                foreach (var val in blocks)
                {
                    components.Add(new SlideshowItemstackTextComponent(capi, val.Value.ToArray(), 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                }

                haveText = true;
            }
            #endregion

            #region Alloy For
            Dictionary<AssetLocation, ItemStack> alloyables = new Dictionary<AssetLocation, ItemStack>();
            foreach (var val in capi.GetMetalAlloys())
            {
                foreach (var ing in val.Ingredients)
                {
                    if (ing.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                    {
                        alloyables[val.Output.ResolvedItemstack.Collectible.Code] = val.Output.ResolvedItemstack;
                    }
                }
            }

            if (alloyables.Count > 0)
            {
                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("Alloy for") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                foreach (var val in alloyables)
                {
                    components.Add(new ItemstackTextComponent(capi, val.Value, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                }

                haveText = true;
            }
            #endregion

            #region Alloyed from
            Dictionary<AssetLocation, MetalAlloyIngredient[]> alloyableFrom = new Dictionary<AssetLocation, MetalAlloyIngredient[]>();
            foreach (var val in capi.GetMetalAlloys())
            {
                if (val.Output.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    List<MetalAlloyIngredient> ingreds = new List<MetalAlloyIngredient>();
                    foreach (var ing in val.Ingredients) ingreds.Add(ing);
                    alloyableFrom[val.Output.ResolvedItemstack.Collectible.Code] = ingreds.ToArray();
                }
            }

            if (alloyableFrom.Count > 0)
            {
                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("Alloyed from") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                foreach (var val in alloyableFrom)
                {
                    foreach (var ingred in val.Value)
                    {
                        string ratio = " " + Lang.Get("alloy-ratio-from-to", (int)(ingred.MinRatio * 100), (int)(ingred.MaxRatio * 100));
                        components.Add(new RichTextComponent(capi, ratio, CairoFont.WhiteSmallText()));
                        ItemstackComponentBase comp = new ItemstackTextComponent(capi, ingred.ResolvedItemstack, 30, 5, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.offY = GuiElement.scaled(7);
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));

                haveText = true;
            }
            #endregion

            #region Bakes/Smelts/Pulverizes/Grinds/Ripens/Dries into
            // Bakes into
            if (collObj.Attributes?["bakingProperties"]?.AsObject<BakingProperties>() is BakingProperties bp && bp.ResultCode != null)
            {
                var item = capi.World.GetItem(new AssetLocation(bp.ResultCode));
                if (item != null)
                {
                    string title = Lang.Get("smeltdesc-bake-title");
                    if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, title + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                    var cmp = new ItemstackTextComponent(capi, new ItemStack(item), 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    cmp.ShowStacksize = true;
                    components.Add(cmp);
                    components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
                    haveText = true;
                }
            }
            else
            // Smelts into
            if (collObj.CombustibleProps?.SmeltedStack?.ResolvedItemstack != null && !collObj.CombustibleProps.SmeltedStack.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                string smelttype = collObj.CombustibleProps.SmeltingType.ToString().ToLowerInvariant();
                string title = Lang.Get("game:smeltdesc-" + smelttype + "-title");


                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, title + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                var cmp = new ItemstackTextComponent(capi, collObj.CombustibleProps.SmeltedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                cmp.ShowStacksize = true;
                components.Add(cmp);
                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
                haveText = true;
            }

            // Pulverizes into
            if (collObj.CrushingProps?.CrushedStack?.ResolvedItemstack != null && !collObj.CrushingProps.CrushedStack.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                string title = Lang.Get("pulverizesdesc-title");

                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, title + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                var cmp = new ItemstackTextComponent(capi, collObj.CrushingProps.CrushedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                cmp.ShowStacksize = true;
                components.Add(cmp);
                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
                haveText = true;
            }


            // Grinds into
            if (collObj.GrindingProps?.GroundStack?.ResolvedItemstack != null && !collObj.GrindingProps.GroundStack.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("Grinds into") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                var cmp = new ItemstackTextComponent(capi, collObj.GrindingProps.GroundStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                cmp.ShowStacksize = true;
                components.Add(cmp);
                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
                haveText = true;
            }

            // Juices into
            JuiceableProperties jprops = getjuiceableProps(inSlot.Itemstack);
            if (jprops != null)
            {
                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("Juices into") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                var jstack = jprops.LiquidStack.ResolvedItemstack.Clone();
                if (jprops.LitresPerItem != null)
                {
                    jstack.StackSize = (int)(100 * jprops.LitresPerItem);
                }
                var cmp = new ItemstackTextComponent(capi, jstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                cmp.ShowStacksize = jprops.LitresPerItem != null;
                components.Add(cmp);
                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
                haveText = true;
            }


            TransitionableProperties[] props = collObj.GetTransitionableProperties(capi.World, stack, null);

            if (props != null)
            {
                bool addedItemStack = false;
                foreach (var prop in props)
                {
                    switch (prop.Type)
                    {
                        case EnumTransitionType.Cure:
                            if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                            haveText = true;
                            addedItemStack = true;
                            components.Add(new RichTextComponent(capi, Lang.Get("After {0} hours, cures into", prop.TransitionHours.avg) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                            components.Add(new ItemstackTextComponent(capi, prop.TransitionedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                            break;

                        case EnumTransitionType.Ripen:
                            if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                            haveText = true;
                            addedItemStack = true;
                            components.Add(new RichTextComponent(capi, Lang.Get("After {0} hours of open storage, ripens into", prop.TransitionHours.avg) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                            components.Add(new ItemstackTextComponent(capi, prop.TransitionedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                            break;

                        case EnumTransitionType.Dry:
                            if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                            haveText = true;
                            addedItemStack = true;
                            components.Add(new RichTextComponent(capi, Lang.Get("After {0} hours of open storage, dries into", prop.TransitionHours.avg) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                            components.Add(new ItemstackTextComponent(capi, prop.TransitionedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                            break;

                        case EnumTransitionType.Convert:
                            break;

                        case EnumTransitionType.Perish:
                            break;

                    }
                }
                if (addedItemStack) components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }
            #endregion

            #region Ingredient for
            ItemStack maxstack = stack.Clone();
            maxstack.StackSize = maxstack.Collectible.MaxStackSize * 10; // because SatisfiesAsIngredient() tests for stacksize. Times 10 because liquid portion oddities

            List<ItemStack> recipestacks = new List<ItemStack>();


            foreach (var recval in capi.World.GridRecipes)
            {
                foreach (var val in recval.resolvedIngredients)
                {
                    CraftingRecipeIngredient ingred = val;

                    if (ingred != null && ingred.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, recval.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        DummySlot outSlot = new DummySlot();
                        DummySlot[] inSlots = new DummySlot[recval.Width * recval.Height];

                        for (int x = 0; x < recval.Width; x++)
                        {
                            for (int y = 0; y < recval.Height; y++)
                            {
                                CraftingRecipeIngredient inIngred = recval.GetElementInGrid(y, x, recval.resolvedIngredients, recval.Width);
                                ItemStack ingredStack = inIngred?.ResolvedItemstack?.Clone();
                                if (inIngred == val) ingredStack = maxstack;

                                inSlots[y * recval.Width + x] = new DummySlot(ingredStack);
                            }
                        }

                        recval.GenerateOutputStack(inSlots, outSlot);
                        recipestacks.Add(outSlot.Itemstack);
                    }
                }

            }


            foreach (var val in capi.GetSmithingRecipes())
            {
                if (val.Ingredient.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, val.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                {
                    recipestacks.Add(val.Output.ResolvedItemstack);
                }
            }


            foreach (var val in capi.GetClayformingRecipes())
            {
                if (val.Ingredient.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, val.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                {
                    recipestacks.Add(val.Output.ResolvedItemstack);
                }
            }


            foreach (var val in capi.GetKnappingRecipes())
            {
                if (val.Ingredient.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, val.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                {
                    recipestacks.Add(val.Output.ResolvedItemstack);
                }
            }


            foreach (var recipe in capi.GetBarrelRecipes())
            {
                foreach (var ingred in recipe.Ingredients)
                {
                    if (ingred.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, recipe.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        recipestacks.Add(recipe.Output.ResolvedItemstack);
                    }
                }
            }

            

            if (recipestacks.Count > 0)
            {
                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("Ingredient for") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                while (recipestacks.Count > 0)
                {
                    ItemStack dstack = recipestacks[0];
                    recipestacks.RemoveAt(0);
                    if (dstack == null) continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, recipestacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    components.Add(comp);
                }

                haveText = true;
            }
            #endregion

            #region Created by
            bool smithable = false;
            bool knappable = false;
            bool clayformable = false;

            foreach (var val in capi.GetSmithingRecipes())
            {
                if (val.Output.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    smithable = true;
                    break;
                }
            }

            foreach (var val in capi.GetKnappingRecipes())
            {
                if (val.Output.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    knappable = true;
                    break;
                }
            }


            foreach (var val in capi.GetClayformingRecipes())
            {
                if (val.Output.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    clayformable = true;
                    break;
                }
            }


            List<GridRecipe> grecipes = new List<GridRecipe>();

            foreach (var val in capi.World.GridRecipes)
            {
                if (val.ShowInCreatedBy && (val.Output.ResolvedItemstack?.Satisfies(stack) ?? false))
                {
                    grecipes.Add(val);
                }
            }


            List<ItemStack> bakables = new List<ItemStack>();
            List<ItemStack> grindables = new List<ItemStack>();
            List<ItemStack> crushables = new List<ItemStack>();
            List<ItemStack> curables = new List<ItemStack>();
            List<ItemStack> ripenables = new List<ItemStack>();
            List<ItemStack> dryables = new List<ItemStack>();
            List<ItemStack> juiceables = new List<ItemStack>();


            foreach (var val in allStacks)
            {
                ItemStack smeltedStack = val.Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;
                if (smeltedStack != null && smeltedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !bakables.Any(s => s.Equals(capi.World, smeltedStack, GlobalConstants.IgnoredStackAttributes)))
                {
                    bakables.Add(val);
                }

                ItemStack groundStack = val.Collectible.GrindingProps?.GroundStack.ResolvedItemstack;
                if (groundStack != null && groundStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !grindables.Any(s => s.Equals(capi.World, groundStack, GlobalConstants.IgnoredStackAttributes)))
                {
                    grindables.Add(val);
                }

                ItemStack crushedStack = val.Collectible.CrushingProps?.CrushedStack.ResolvedItemstack;
                if (crushedStack != null && crushedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !crushables.Any(s => s.Equals(capi.World, crushedStack, GlobalConstants.IgnoredStackAttributes)))
                {
                    crushables.Add(val);
                }

                if (val.ItemAttributes?["juiceableProperties"].Exists == true)
                {
                    var fjprops = getjuiceableProps(val);
                    var juicedStack = fjprops.LiquidStack?.ResolvedItemstack;
                    if (juicedStack != null && juicedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !juiceables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                    {
                        juiceables.Add(val);
                    }
                }

                TransitionableProperties[] oprops = val.Collectible.GetTransitionableProperties(capi.World, val, null);
                if (oprops != null)
                {
                    foreach (var prop in oprops)
                    {
                        ItemStack transitionedStack = prop.TransitionedStack?.ResolvedItemstack;

                        switch (prop.Type)
                        {
                            case EnumTransitionType.Cure:
                                if (transitionedStack != null && transitionedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !curables.Any(s => s.Equals(capi.World, transitionedStack, GlobalConstants.IgnoredStackAttributes)))
                                {
                                    curables.Add(val);
                                }
                                break;

                            case EnumTransitionType.Ripen:
                                if (transitionedStack != null && transitionedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !curables.Any(s => s.Equals(capi.World, transitionedStack, GlobalConstants.IgnoredStackAttributes)))
                                {
                                    ripenables.Add(val);
                                }
                                break;


                            case EnumTransitionType.Dry:
                                if (transitionedStack != null && transitionedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !curables.Any(s => s.Equals(capi.World, transitionedStack, GlobalConstants.IgnoredStackAttributes)))
                                {
                                    dryables.Add(val);
                                }
                                break;

                            case EnumTransitionType.Convert:
                                break;

                            case EnumTransitionType.Perish:
                                break;

                        }
                    }
                }

            }


            List<RichTextComponentBase> barrelRecipestext = new List<RichTextComponentBase>();
            Dictionary<string, List<BarrelRecipe>> brecipesbyCode = new Dictionary<string, List<BarrelRecipe>>();
            foreach (var recipe in capi.GetBarrelRecipes())
            {
                ItemStack mixdStack = recipe.Output.ResolvedItemstack;

                if (mixdStack != null && mixdStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    List<BarrelRecipe> tmp;

                    if (!brecipesbyCode.TryGetValue(recipe.Code, out tmp))
                    {
                        brecipesbyCode[recipe.Code] = tmp = new List<BarrelRecipe>();
                    }

                    tmp.Add(recipe);
                }
            }



            foreach (var recipes in brecipesbyCode.Values)
            {
                int ingredientsLen = recipes[0].Ingredients.Length;
                ItemStack[][] ingstacks = new ItemStack[ingredientsLen][];
                ItemStack[] outstacks = new ItemStack[recipes.Count];

                for (int i = 0; i < recipes.Count; i++)
                {
                    if (recipes[i].Ingredients.Length != ingredientsLen)
                    {
                        throw new Exception("Barrel recipe with same name but different ingredient count! Sorry, this is not supported right now. Please make sure you choose different barrel recipe names if you have different ingredient counts.");
                    }

                    for (int j = 0; j < ingredientsLen; j++)
                    {
                        if (i == 0)
                        {
                            ingstacks[j] = new ItemStack[recipes.Count];
                        }

                        ingstacks[j][i] = recipes[i].Ingredients[j].ResolvedItemstack;
                    }

                    outstacks[i] = recipes[i].Output.ResolvedItemstack;
                }

                for (int i = 0; i < ingredientsLen; i++)
                {
                    if (i > 0)
                    {
                        RichTextComponent cmp = new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText());
                        cmp.VerticalAlign = EnumVerticalAlign.Middle;
                        barrelRecipestext.Add(cmp);
                    }

                    SlideshowItemstackTextComponent scmp = new SlideshowItemstackTextComponent(capi, ingstacks[i], 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    scmp.ShowStackSize = true;
                    barrelRecipestext.Add(scmp);
                }

                var eqcomp = new RichTextComponent(capi, " = ", CairoFont.WhiteMediumText());
                eqcomp.VerticalAlign = EnumVerticalAlign.Middle;
                barrelRecipestext.Add(eqcomp);
                var ocmp = new SlideshowItemstackTextComponent(capi, outstacks, 40, EnumFloat.Inline);
                
                ocmp.ShowStackSize = true;
                barrelRecipestext.Add(ocmp);

                barrelRecipestext.Add(new ClearFloatTextComponent(capi, 10));
            }





            string customCreatedBy = stack.Collectible.Attributes?["handbook"]?["createdBy"]?.AsString(null);
            string bakingInitialIngredient = collObj.Attributes?["bakingProperties"]?.AsObject<BakingProperties>()?.InitialCode;

            if (grecipes.Count > 0 || smithable || knappable || clayformable || customCreatedBy != null || bakables.Count > 0 || barrelRecipestext.Count > 0 || grindables.Count > 0 || curables.Count > 0 || ripenables.Count > 0 || dryables.Count > 0 || crushables.Count > 0 || bakingInitialIngredient != null || juiceables.Count > 0)
            {
                if (haveText) components.Add(new ClearFloatTextComponent(capi, marginTop));
                haveText = true;
                components.Add(new RichTextComponent(capi, Lang.Get("Created by") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));


                if (smithable)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText()));
                    components.Add(new LinkTextComponent(capi, Lang.Get("Smithing") + "\n", CairoFont.WhiteSmallText(), (cs) => { openDetailPageFor("craftinginfo-smithing"); }));
                }
                if (knappable)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText()));
                    components.Add(new LinkTextComponent(capi, Lang.Get("Knapping") + "\n", CairoFont.WhiteSmallText(), (cs) => { openDetailPageFor("craftinginfo-knapping"); }));
                }
                if (clayformable)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText()));
                    components.Add(new LinkTextComponent(capi, Lang.Get("Clay forming") + "\n", CairoFont.WhiteSmallText(), (cs) => { openDetailPageFor("craftinginfo-clayforming"); }));
                }
                if (customCreatedBy != null)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText()));
                    components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get(customCreatedBy) + "\n", CairoFont.WhiteSmallText()));
                }

                if (grindables.Count > 0)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Grinding") + "\n", CairoFont.WhiteSmallText()));

                    while (grindables.Count > 0)
                    {
                        ItemStack dstack = grindables[0];
                        grindables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, grindables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.ShowStackSize = true;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                }

                if (crushables.Count > 0)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Crushing") + "\n", CairoFont.WhiteSmallText()));

                    while (crushables.Count > 0)
                    {
                        ItemStack dstack = crushables[0];
                        crushables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, crushables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.ShowStackSize = true;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                }


                if (curables.Count > 0)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Curing") + "\n", CairoFont.WhiteSmallText()));

                    while (curables.Count > 0)
                    {
                        ItemStack dstack = curables[0];
                        curables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, curables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        components.Add(comp);
                    }
                }



                if (ripenables.Count > 0)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Ripening") + "\n", CairoFont.WhiteSmallText()));

                    while (ripenables.Count > 0)
                    {
                        ItemStack dstack = ripenables[0];
                        ripenables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, ripenables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (dryables.Count > 0)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Drying") + "\n", CairoFont.WhiteSmallText()));

                    while (dryables.Count > 0)
                    {
                        ItemStack dstack = dryables[0];
                        dryables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, dryables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }


                if (bakables.Count > 0)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Cooking/Smelting/Baking") + "\n", CairoFont.WhiteSmallText()));

                    while (bakables.Count > 0)
                    {
                        ItemStack dstack = bakables[0];
                        bakables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, bakables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.ShowStackSize = true;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (juiceables.Count > 0)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Juicing") + "\n", CairoFont.WhiteSmallText()));

                    while (juiceables.Count > 0)
                    {
                        ItemStack dstack = juiceables[0];
                        juiceables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, bakables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }


                if (bakingInitialIngredient != null)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Baking (in oven)") + "\n", CairoFont.WhiteSmallText()));
                    CollectibleObject cobj = capi.World.GetItem(new AssetLocation(bakingInitialIngredient));
                    if (cobj == null) cobj = capi.World.GetBlock(new AssetLocation(bakingInitialIngredient));

                    components.Add(new ItemstackTextComponent(capi, new ItemStack(cobj), 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                }

                if (grecipes.Count > 0)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Crafting") + "\n", CairoFont.WhiteSmallText()));

                    OrderedDictionary<int, List<GridRecipe>> grouped = new OrderedDictionary<int, List<GridRecipe>>();

                    ItemStack[] outputStacks = new ItemStack[grecipes.Count];
                    int i= 0;

                    foreach (var recipe in grecipes)
                    {
                        List<GridRecipe> list;
                        if (!grouped.TryGetValue(recipe.RecipeGroup, out list))
                        {
                            grouped[recipe.RecipeGroup] = list = new List<GridRecipe>();
                        }
                        list.Add(recipe);
                        outputStacks[i++] = recipe.Output.ResolvedItemstack;
                    }

                    foreach (var val in grouped)
                    {
                        var comp = new SlideshowGridRecipeTextComponent(capi, val.Value.ToArray(), 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)), allStacks);
                        comp.VerticalAlign = EnumVerticalAlign.Top;
                        comp.PaddingRight = 8;   
                        comp.UnscaledMarginTop = 8;

                        components.Add(comp);

                        var ecomp = new RichTextComponent(capi, "=", CairoFont.WhiteMediumText());
                        ecomp.VerticalAlign = EnumVerticalAlign.Middle;
                        var ocomp = new SlideshowItemstackTextComponent(capi, outputStacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        ocomp.VerticalAlign = EnumVerticalAlign.Middle;
                        ocomp.ShowStackSize = true;

                        components.Add(ecomp);
                        components.Add(ocomp);
                    }
                }


                if (barrelRecipestext.Count > 0)
                {
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Mixing (in Barrel)") + "\n", CairoFont.WhiteSmallText()));
                    components.AddRange(barrelRecipestext);
                }
            }
            #endregion

            #region Extra Sections
            if (ExtraHandBookSections != null)
            {
                for (int i = 0; i < ExtraHandBookSections.Length; i++)
                {
                    components.Add(new ClearFloatTextComponent(capi, 16));
                    components.Add(new RichTextComponent(capi, Lang.Get(ExtraHandBookSections[i].Title) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                    if (ExtraHandBookSections[i].TextParts != null)
                    {
                        components.AddRange(VtmlUtil.Richtextify(capi, string.Join(", ", ExtraHandBookSections[i].TextParts) + "\n", CairoFont.WhiteSmallText()));
                    }
                    else
                    {
                        components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get(ExtraHandBookSections[i].Text) + "\n", CairoFont.WhiteSmallText()));
                    }

                }
            }

            string type = stack.Class.Name();
            string code = collObj.Code.ToShortString();
            string langExtraSectionTitle = Lang.GetMatchingIfExists(collObj.Code.Domain + ":" + type + "-handbooktitle-" + code);
            string langExtraSectionText = Lang.GetMatchingIfExists(collObj.Code.Domain + ":" + type + "-handbooktext-" + code);

            if (langExtraSectionTitle != null || langExtraSectionText != null)
            {
                components.Add(new ClearFloatTextComponent(capi, marginTop * 4));
                if (langExtraSectionTitle != null)
                {
                    components.Add(new RichTextComponent(capi, langExtraSectionTitle + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                    components.Add(new ClearFloatTextComponent(capi, marginTop));
                }
                if (langExtraSectionText != null)
                {
                    components.AddRange(VtmlUtil.Richtextify(capi, langExtraSectionText + "\n", CairoFont.WhiteSmallText()));
                }
            }

            #endregion

            #region Storable in

            List<RichTextComponentBase> storableComps = new List<RichTextComponentBase>();


            if (stack.ItemAttributes?.IsTrue("moldrackable") == true)
            {
                storableComps.Add(new ClearFloatTextComponent(capi, marginTop));
                storableComps.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-storable-moldrack"), CairoFont.WhiteSmallText()));
            }
            if (stack.ItemAttributes?.IsTrue("shelvable") == true)
            {
                storableComps.Add(new ClearFloatTextComponent(capi, marginTop));
                storableComps.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-storable-shelves"), CairoFont.WhiteSmallText()));
            }
            if (stack.ItemAttributes?.IsTrue("displaycaseable") == true)
            {
                storableComps.Add(new ClearFloatTextComponent(capi, marginTop));
                storableComps.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-storable-displaycase"), CairoFont.WhiteSmallText()));
            }
            if (stack.Collectible.Tool != null || stack.ItemAttributes?["rackable"].AsBool() == true)
            {
                storableComps.Add(new ClearFloatTextComponent(capi, marginTop));
                storableComps.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-storable-toolrack"), CairoFont.WhiteSmallText()));
            }
            if (stack.Collectible.HasBehavior<CollectibleBehaviorGroundStorable>())
            {
                storableComps.Add(new ClearFloatTextComponent(capi, marginTop));
                storableComps.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-storable-ground"), CairoFont.WhiteSmallText()));
            }
            if (stack.ItemAttributes?["waterTightContainerProps"].Exists == true)
            {
                storableComps.Add(new ClearFloatTextComponent(capi, marginTop));
                storableComps.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-storable-barrel"), CairoFont.WhiteSmallText()));
            }

            if (storableComps.Count > 0)
            {
                components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("Storable in/on") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.AddRange(storableComps);
            }

            #endregion

            
            if (this.collObj is ICustomHandbookPageContent chp)
            {
                chp.OnHandbookPageComposed(components, inSlot, capi, allStacks, openDetailPageFor);
            }


            return components.ToArray();
        }



        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (collObj.Attributes?["pigment"]?["color"].Exists == true)
            {
                dsc.AppendLine(Lang.Get("Pigment: {0}", Lang.Get(collObj.Attributes["pigment"]["name"].AsString())));
            }


            JsonObject obj = collObj.Attributes?["fertilizerProps"];
            if (obj != null && obj.Exists)
            {
                FertilizerProps fprops = obj.AsObject<FertilizerProps>();
                if (fprops != null)
                {
                    dsc.AppendLine(Lang.Get("Fertilizer: {0}% N, {1}% P, {2}% K", fprops.N, fprops.P, fprops.K));
                }
            }

            JuiceableProperties jprops = getjuiceableProps(inSlot.Itemstack);
            if (jprops != null)
            {
                float litres;
                if (jprops.LitresPerItem == null)
                {
                    litres = (float)inSlot.Itemstack.Attributes.GetDecimal("juiceableLitresLeft");
                } else
                {
                    litres = (float)jprops.LitresPerItem * inSlot.Itemstack.StackSize;
                }

                if (litres > 0.01)
                {
                    dsc.AppendLine(Lang.Get("collectibleinfo-juicingproperties", litres, jprops.LiquidStack.ResolvedItemstack.GetName()));
                }
            }

        }

        public JuiceableProperties getjuiceableProps(ItemStack stack)
        {
            var props = stack?.ItemAttributes?["juiceableProperties"].Exists == true ? stack.ItemAttributes["juiceableProperties"].AsObject<JuiceableProperties>(null, stack.Collectible.Code.Domain) : null;
            props?.LiquidStack?.Resolve(Api.World, "juiceable properties liquidstack");
            props?.PressedStack?.Resolve(Api.World, "juiceable properties pressedstack");

            return props;
        }
    }
}
