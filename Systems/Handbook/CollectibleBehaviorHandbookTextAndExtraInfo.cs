using Cairo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public interface ICustomHandbookPageContent
    {
        void OnHandbookPageComposed(List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor);
    }

    public class CollectibleBehaviorHandbookTextAndExtraInfo : CollectibleBehavior
    {
        protected const int TinyPadding = 2;   // Used to add tiny amounts of vertical padding after headings, so that things look less cramped
        protected const int TinyIndent = 2;    // Used to indent the page contents following headings - this subtly helps headings to stand out more
        protected const int MarginBottom = 3;  // Used following some (but not all) itemstack graphics
        protected const int SmallPadding = 7;  // Used to separate bullets in the Created By list
        protected const int MediumPadding = 14;  // Used before all headings

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

            float marginTop, marginBottom;
            addGeneralInfo(inSlot, capi, stack, components, out marginTop, out marginBottom);

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

            addDropsInfo(capi, openDetailPageFor, stack, components, marginTop, breakBlocks);
            bool haveText = addObtainedThroughInfo(capi, openDetailPageFor, stack, components, marginTop, breakBlocks);
            haveText = addFoundInInfo(capi, openDetailPageFor, stack, components, marginTop, haveText);
            haveText = addAlloyForInfo(capi, openDetailPageFor, stack, components, marginTop, haveText);
            haveText = addAlloyedFromInfo(capi, openDetailPageFor, stack, components, marginTop, haveText);
            haveText = addProcessesIntoInfo(inSlot, capi, openDetailPageFor, stack, components, marginTop, marginBottom, haveText);
            haveText = addIngredientForInfo(capi, openDetailPageFor, stack, components, marginTop, haveText);
            haveText = addCreatedByInfo(capi, allStacks, openDetailPageFor, stack, components, marginTop, haveText);
            addExtraSections(capi, stack, components, marginTop);
            addStorableInfo(capi, stack, components, marginTop);

            if (this.collObj is ICustomHandbookPageContent chp)
            {
                chp.OnHandbookPageComposed(components, inSlot, capi, allStacks, openDetailPageFor);
            }

            return components.ToArray();
        }

        protected void addGeneralInfo(ItemSlot inSlot, ICoreClientAPI capi, ItemStack stack, List<RichTextComponentBase> components, out float marginTop, out float marginBottom)
        {
            components.Add(new ItemstackTextComponent(capi, stack, 100, 10, EnumFloat.Left));
            components.AddRange(VtmlUtil.Richtextify(capi, stack.GetName() + "\n", CairoFont.WhiteSmallishText()));
            var font = CairoFont.WhiteDetailText();
            if (capi.Settings.Bool["extendedDebugInfo"] == true)
            {
                font.Color[3] = 0.5;
                components.AddRange(VtmlUtil.Richtextify(capi, "Page code:" + GuiHandbookItemStackPage.PageCodeForStack(stack) + "\n", font));
            }
            components.AddRange(VtmlUtil.Richtextify(capi, stack.GetDescription(capi.World, inSlot), CairoFont.WhiteSmallText()));

            marginTop = SmallPadding;
            marginBottom = MarginBottom;
        }

        protected void addDropsInfo(ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, List<ItemStack> breakBlocks)
        {
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
                        bool haveText = components.Count > 0;
                        AddHeading(components, capi, "Drops when broken", ref haveText);
                        components.Add(new ClearFloatTextComponent(capi, TinyPadding));
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

                        components.Add(new ClearFloatTextComponent(capi, TinyPadding));
                    }
                }
            }
        }

        protected bool addObtainedThroughInfo(ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, List<ItemStack> breakBlocks)
        {
            List<string> killCreatures = new List<string>();
            List<string> harvestCreatures = new List<string>();
            HashSet<string> harvestCreatureCodes = new HashSet<string>();

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

                var harvestableDrops = val.Attributes?["harvestableDrops"]?.AsArray<BlockDropItemStack>();

                if (harvestableDrops != null)
                {
                    foreach (var hstack in harvestableDrops)
                    {
                        hstack.Resolve(Api.World, "handbook info", new AssetLocation());

                        if (hstack.ResolvedItemstack != null && hstack.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                        {
                            string code = val.Code.Domain + ":item-creature-" + val.Code.Path;
                            if (val.Attributes?["handbook"]["groupcode"]?.Exists == true)
                            {
                                code = val.Attributes?["handbook"]["groupcode"].AsString();
                            }

                            if (!harvestCreatureCodes.Contains(code))
                            {
                                harvestCreatures.Add(Lang.Get(code));
                                harvestCreatureCodes.Add(code);
                            }
                            break;
                        }
                    }
                }
                   
                
            }


            bool haveText = components.Count > 0;

            if (killCreatures.Count > 0)
            {
                AddHeading(components, capi, "Obtained by killing", ref haveText);
                components.Add(new ClearFloatTextComponent(capi, TinyPadding));
                var comp = new RichTextComponent(capi, string.Join(", ", killCreatures) + "\n", CairoFont.WhiteSmallText());
                comp.PaddingLeft = TinyIndent;
                components.Add(comp);
            }

            if (harvestCreatures.Count > 0)
            {
                AddHeading(components, capi, "Obtained by killing & harvesting", ref haveText);
                components.Add(new ClearFloatTextComponent(capi, TinyPadding));
                var comp = new RichTextComponent(capi, string.Join(", ", harvestCreatures) + "\n", CairoFont.WhiteSmallText());
                comp.PaddingLeft = TinyIndent;
                components.Add(comp);
            }

            if (breakBlocks.Count > 0)
            {
                AddHeading(components, capi, "Obtained by breaking", ref haveText);
                components.Add(new ClearFloatTextComponent(capi, TinyPadding));

                while (breakBlocks.Count > 0)
                {
                    ItemStack dstack = breakBlocks[0];
                    breakBlocks.RemoveAt(0);
                    if (dstack == null) continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, breakBlocks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    components.Add(comp);
                }

                components.Add(new ClearFloatTextComponent(capi, TinyPadding));
            }

            return haveText;
        }

        protected bool addFoundInInfo(ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, bool haveText)
        {
            string customFoundIn = stack.Collectible.Attributes?["handbook"]?["foundIn"]?.AsString(null);
            if (customFoundIn != null)
            {
                AddHeading(components, capi, "Found in", ref haveText);
                var comp = new RichTextComponent(capi, Lang.Get(customFoundIn), CairoFont.WhiteSmallText());
                comp.PaddingLeft = TinyIndent;
                components.Add(comp);
            }

            if (collObj.Attributes?["hostRockFor"].Exists == true)
            {
                AddHeading(components, capi, "Host rock for", ref haveText);

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

                int firstPadding = TinyIndent;
                foreach (var val in blocks)
                {
                    var comp = new SlideshowItemstackTextComponent(capi, val.Value.ToArray(), 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    comp.PaddingLeft = firstPadding;
                    firstPadding = 0;
                    components.Add(comp);
                }
            }


            if (collObj.Attributes?["hostRock"].Exists == true)
            {
                AddHeading(components, capi, "Occurs in host rock", ref haveText);

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

                int firstPadding = TinyIndent;
                foreach (var val in blocks)
                {
                    var comp = new SlideshowItemstackTextComponent(capi, val.Value.ToArray(), 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    comp.PaddingLeft = firstPadding;
                    firstPadding = 0;
                    components.Add(comp);
                }
            }

            return haveText;
        }

        protected static bool addAlloyForInfo(ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, bool haveText)
        {
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
                AddHeading(components, capi, "Alloy for", ref haveText);

                int firstPadding = TinyIndent;
                foreach (var val in alloyables)
                {
                    var comp = new ItemstackTextComponent(capi, val.Value, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    comp.PaddingLeft = firstPadding;
                    firstPadding = 0;
                    components.Add(comp);
                }

                haveText = true;
            }

            return haveText;
        }

        protected bool addAlloyedFromInfo(ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, bool haveText)
        {
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
                AddHeading(components, capi, "Alloyed from", ref haveText);

                int firstPadding = TinyIndent;
                foreach (var val in alloyableFrom)
                {
                    foreach (var ingred in val.Value)
                    {
                        string ratio = " " + Lang.Get("alloy-ratio-from-to", (int)(ingred.MinRatio * 100), (int)(ingred.MaxRatio * 100));
                        components.Add(new RichTextComponent(capi, ratio, CairoFont.WhiteSmallText()));
                        ItemstackComponentBase comp = new ItemstackTextComponent(capi, ingred.ResolvedItemstack, 30, 5, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.offY = GuiElement.scaled(7);
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
            }

            return haveText;
        }

        protected bool addProcessesIntoInfo(ItemSlot inSlot, ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, float marginBottom, bool haveText)
        {
            // Bakes into
            if (collObj.Attributes?["bakingProperties"]?.AsObject<BakingProperties>() is BakingProperties bp && bp.ResultCode != null)
            {
                var item = capi.World.GetItem(new AssetLocation(bp.ResultCode));
                if (item != null)
                {
                    AddHeading(components, capi, "smeltdesc-bake-title", ref haveText);

                    var cmp = new ItemstackTextComponent(capi, new ItemStack(item), 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    cmp.ShowStacksize = true;
                    cmp.PaddingLeft = TinyIndent;
                    components.Add(cmp);
                    components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
                }
            }
            else
            // Smelts into
            if (collObj.CombustibleProps?.SmeltedStack?.ResolvedItemstack != null && !collObj.CombustibleProps.SmeltedStack.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                string smelttype = collObj.CombustibleProps.SmeltingType.ToString().ToLowerInvariant();
                AddHeading(components, capi, "game:smeltdesc-" + smelttype + "-title", ref haveText);

                var cmp = new ItemstackTextComponent(capi, collObj.CombustibleProps.SmeltedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                cmp.ShowStacksize = true;
                cmp.PaddingLeft = TinyIndent;
                components.Add(cmp);
                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }

            // Pulverizes into
            if (collObj.CrushingProps?.CrushedStack?.ResolvedItemstack != null && !collObj.CrushingProps.CrushedStack.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                AddHeading(components, capi, "pulverizesdesc-title", ref haveText);

                var cmp = new ItemstackTextComponent(capi, collObj.CrushingProps.CrushedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                cmp.ShowStacksize = true;
                cmp.PaddingLeft = TinyIndent;
                components.Add(cmp);
                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }


            // Grinds into
            if (collObj.GrindingProps?.GroundStack?.ResolvedItemstack != null && !collObj.GrindingProps.GroundStack.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                AddHeading(components, capi, "Grinds into", ref haveText);

                var cmp = new ItemstackTextComponent(capi, collObj.GrindingProps.GroundStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                cmp.ShowStacksize = true;
                cmp.PaddingLeft = TinyIndent;
                components.Add(cmp);
                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }

            // Juices into
            JuiceableProperties jprops = getjuiceableProps(inSlot.Itemstack);
            if (jprops != null)
            {
                AddHeading(components, capi, "Juices into", ref haveText);

                var jstack = jprops.LiquidStack.ResolvedItemstack.Clone();
                if (jprops.LitresPerItem != null)
                {
                    jstack.StackSize = (int)(100 * jprops.LitresPerItem);
                }
                var cmp = new ItemstackTextComponent(capi, jstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                cmp.ShowStacksize = jprops.LitresPerItem != null;
                cmp.PaddingLeft = TinyIndent;
                components.Add(cmp);
                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }


            TransitionableProperties[] props = collObj.GetTransitionableProperties(capi.World, stack, null);

            if (props != null)
            {
                haveText = true;
                var verticalSpace = new ClearFloatTextComponent(capi, MediumPadding);

                bool addedItemStack = false;
                foreach (var prop in props)
                {
                    switch (prop.Type)
                    {
                        case EnumTransitionType.Cure:
                            components.Add(verticalSpace);
                            addedItemStack = true;
                            components.Add(new RichTextComponent(capi, Lang.Get("After {0} hours, cures into", prop.TransitionHours.avg) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                            var compc = new ItemstackTextComponent(capi, prop.TransitionedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                            compc.PaddingLeft = TinyIndent;
                            components.Add(compc);
                            break;

                        case EnumTransitionType.Ripen:
                            components.Add(verticalSpace);
                            addedItemStack = true;
                            components.Add(new RichTextComponent(capi, Lang.Get("After {0} hours of open storage, ripens into", prop.TransitionHours.avg) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                            var compr = new ItemstackTextComponent(capi, prop.TransitionedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                            compr.PaddingLeft = TinyIndent;
                            components.Add(compr);
                            break;

                        case EnumTransitionType.Dry:
                            components.Add(verticalSpace);
                            addedItemStack = true;
                            components.Add(new RichTextComponent(capi, Lang.Get("After {0} hours of open storage, dries into", prop.TransitionHours.avg) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                            var compd = new ItemstackTextComponent(capi, prop.TransitionedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                            compd.PaddingLeft = TinyIndent;
                            components.Add(compd);
                            break;

                        case EnumTransitionType.Melt:
                            components.Add(verticalSpace);
                            addedItemStack = true;
                            components.Add(new RichTextComponent(capi, Lang.Get("After {0} hours of open storage, melts into", prop.TransitionHours.avg) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                            var compm = new ItemstackTextComponent(capi, prop.TransitionedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                            compm.PaddingLeft = TinyIndent;
                            components.Add(compm);
                            break;

                        case EnumTransitionType.Convert:
                            break;

                        case EnumTransitionType.Perish:
                            break;

                    }
                }
                if (addedItemStack) components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }

            return haveText;
        }

        protected bool addIngredientForInfo(ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, bool haveText)
        {
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
                AddHeading(components, capi, "Ingredient for", ref haveText);
                components.Add(new ClearFloatTextComponent(capi, TinyPadding));

                while (recipestacks.Count > 0)
                {
                    ItemStack dstack = recipestacks[0];
                    recipestacks.RemoveAt(0);
                    if (dstack == null) continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, recipestacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    components.Add(comp);
                }

                components.Add(new ClearFloatTextComponent(capi, MarginBottom));
            }

            return haveText;
        }

        protected bool addCreatedByInfo(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, bool haveText)
        {
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
                if (!val.ShowInCreatedBy) continue;
                if (val.Output.ResolvedItemstack?.Satisfies(stack) ?? false)
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
            List<ItemStack> meltables = new List<ItemStack>();
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

                            case EnumTransitionType.Melt:
                                if (transitionedStack != null && transitionedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !curables.Any(s => s.Equals(capi.World, transitionedStack, GlobalConstants.IgnoredStackAttributes)))
                                {
                                    meltables.Add(val);
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


            List<RichTextComponentBase> barrelRecipestext = BuildBarrelRecipesText(capi, stack, openDetailPageFor);


            string customCreatedBy = stack.Collectible.Attributes?["handbook"]?["createdBy"]?.AsString(null);
            string bakingInitialIngredient = collObj.Attributes?["bakingProperties"]?.AsObject<BakingProperties>()?.InitialCode;

            if (grecipes.Count > 0 || smithable || knappable || clayformable || customCreatedBy != null || bakables.Count > 0 || barrelRecipestext.Count > 0 || grindables.Count > 0 || curables.Count > 0 || ripenables.Count > 0 || dryables.Count > 0 || meltables.Count > 0 || crushables.Count > 0 || bakingInitialIngredient != null || juiceables.Count > 0)
            {
                AddHeading(components, capi, "Created by", ref haveText);

                var verticalSpaceSmall = new ClearFloatTextComponent(capi, SmallPadding);
                var verticalSpace = new ClearFloatTextComponent(capi, TinyPadding + 1);   // The first bullet point has a smaller space than any later ones

                if (smithable)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Smithing", "craftinginfo-smithing");
                }
                if (knappable)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Knapping", "craftinginfo-knapping");
                }
                if (clayformable)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Clay forming", "craftinginfo-clayforming");
                }
                if (customCreatedBy != null)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    var comp = new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText());
                    comp.PaddingLeft = TinyIndent;
                    components.Add(comp);
                    components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get(customCreatedBy) + "\n", CairoFont.WhiteSmallText()));
                }

                if (grindables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Grinding", null);

                    int firstPadding = TinyPadding;
                    while (grindables.Count > 0)
                    {
                        ItemStack dstack = grindables[0];
                        grindables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, grindables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.ShowStackSize = true;
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (crushables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Crushing", null);

                    int firstPadding = TinyPadding;
                    while (crushables.Count > 0)
                    {
                        ItemStack dstack = crushables[0];
                        crushables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, crushables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.ShowStackSize = true;
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }


                if (curables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Curing", null);

                    int firstPadding = TinyPadding;
                    while (curables.Count > 0)
                    {
                        ItemStack dstack = curables[0];
                        curables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, curables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }
                }



                if (ripenables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Ripening", null);

                    int firstPadding = TinyPadding;
                    while (ripenables.Count > 0)
                    {
                        ItemStack dstack = ripenables[0];
                        ripenables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, ripenables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (dryables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Drying", null);

                    int firstPadding = TinyPadding;
                    while (dryables.Count > 0)
                    {
                        ItemStack dstack = dryables[0];
                        dryables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, dryables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (meltables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Melting", null);

                    int firstPadding = TinyPadding;
                    while (meltables.Count > 0)
                    {
                        ItemStack dstack = meltables[0];
                        meltables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, meltables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (bakables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Cooking/Smelting/Baking", null);

                    int firstPadding = TinyPadding;
                    while (bakables.Count > 0)
                    {
                        ItemStack dstack = bakables[0];
                        bakables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, bakables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        //comp.ShowStackSize = true;   // We don't want to show the stacksize, as the bakables were collected from allStacks and each has stackSize == maxStackSize
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (juiceables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Juicing", null);

                    int firstPadding = TinyPadding;
                    while (juiceables.Count > 0)
                    {
                        ItemStack dstack = juiceables[0];
                        juiceables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, bakables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }


                if (bakingInitialIngredient != null)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Baking (in oven)", null);
                    CollectibleObject cobj = capi.World.GetItem(new AssetLocation(bakingInitialIngredient));
                    if (cobj == null) cobj = capi.World.GetBlock(new AssetLocation(bakingInitialIngredient));

                    var comp = new ItemstackTextComponent(capi, new ItemStack(cobj), 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    comp.PaddingLeft = TinyIndent;
                    components.Add(comp);
                }

                if (grecipes.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Crafting", null);

                    OrderedDictionary<int, List<GridRecipe>> grouped = new OrderedDictionary<int, List<GridRecipe>>();

                    ItemStack[] outputStacks = new ItemStack[grecipes.Count];
                    int i = 0;

                    foreach (var recipe in grecipes)
                    {
                        List<GridRecipe> list;
                        if (!grouped.TryGetValue(recipe.RecipeGroup, out list))
                        {
                            grouped[recipe.RecipeGroup] = list = new List<GridRecipe>();
                        }
                        
                        if (recipe.CopyAttributesFrom != null && recipe.Ingredients.ContainsKey(recipe.CopyAttributesFrom))
                        {
                            var rec = recipe.Clone();

                            var ingred = rec.Ingredients[recipe.CopyAttributesFrom];
                            var cattr = stack.Attributes.Clone();
                            cattr.MergeTree(ingred.ResolvedItemstack.Attributes);
                            ingred.Attributes = new JsonObject(JToken.Parse(cattr.ToJsonToken()));
                            rec.ResolveIngredients(capi.World);

                            rec.Output.ResolvedItemstack.Attributes.MergeTree(stack.Attributes);

                            list.Add(rec);
                            outputStacks[i++] = rec.Output.ResolvedItemstack;
                            continue;
                        }

                        list.Add(recipe);
                        outputStacks[i++] = recipe.Output.ResolvedItemstack;
                    }

                    int j = 0;
                    foreach (var val in grouped)
                    {
                        if (j++ % 2 == 0) components.Add(verticalSpaceSmall);    // Reset the component model with a small vertical element every 2nd grid recipe, otherwise horizontal aligment gets messed up

                        var comp = new SlideshowGridRecipeTextComponent(capi, val.Value.ToArray(), 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)), allStacks);
                        comp.VerticalAlign = EnumVerticalAlign.Top;
                        comp.PaddingRight = 8;
                        comp.UnscaledMarginTop = 8;
                        comp.PaddingLeft = TinyPadding * 2 + (1 - j % 2) * 20;   // Add horizontal padding for every 2nd grid (i.e. the right hand side one when drawn)

                        components.Add(comp);

                        var ecomp = new RichTextComponent(capi, "=", CairoFont.WhiteMediumText());
                        ecomp.VerticalAlign = EnumVerticalAlign.FixedOffset;
                        ecomp.UnscaledMarginTop = 51;
                        ecomp.PaddingRight = 5;
                        var ocomp = new SlideshowItemstackTextComponent(capi, outputStacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        ocomp.overrideCurrentItemStack = () => comp.CurrentVisibleRecipe.Recipe.Output.ResolvedItemstack;
                        ocomp.VerticalAlign = EnumVerticalAlign.FixedOffset;
                        ocomp.UnscaledMarginTop = 50;
                        ocomp.ShowStackSize = true;

                        components.Add(ecomp);
                        components.Add(ocomp);
                    }

                    components.Add(new ClearFloatTextComponent(capi, MarginBottom));  //nice margin below the grid graphic
                }

                if (barrelRecipestext.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Mixing (in Barrel)", null);
                    components.AddRange(barrelRecipestext);
                }
            }

            return haveText;
        }

        protected static void AddHeading(List<RichTextComponentBase> components, ICoreClientAPI capi, string heading, ref bool haveText)
        {
            if (haveText) components.Add(new ClearFloatTextComponent(capi, MediumPadding));
            haveText = true;
            var headc = new RichTextComponent(capi, Lang.Get(heading) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold));
            components.Add(headc);
        }

        protected void AddSubHeading(List<RichTextComponentBase> components, ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, string subheading, string detailpage)
        {
            if (detailpage == null)
            {
                var bullet = new RichTextComponent(capi, "• " + Lang.Get(subheading) + "\n", CairoFont.WhiteSmallText());
                bullet.PaddingLeft = TinyIndent;
                components.Add(bullet);
            }
            else
            {
                var bullet = new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText());
                bullet.PaddingLeft = TinyIndent;
                components.Add(bullet);
                components.Add(new LinkTextComponent(capi, Lang.Get(subheading) + "\n", CairoFont.WhiteSmallText(), (cs) => { openDetailPageFor(detailpage); }));
            }
        }

        protected void addExtraSections(ICoreClientAPI capi, ItemStack stack, List<RichTextComponentBase> components, float marginTop)
        {
            if (ExtraHandBookSections != null)   // For example, Sold by trader
            {
                bool haveText = true;
                for (int i = 0; i < ExtraHandBookSections.Length; i++)
                {
                    ExtraHandbookSection extraSection = ExtraHandBookSections[i];
                    AddHeading(components, capi, extraSection.Title, ref haveText);
                    components.Add(new ClearFloatTextComponent(capi, TinyPadding));

                    var spacer = new RichTextComponent(capi, "", CairoFont.WhiteSmallText());
                    spacer.PaddingLeft = TinyIndent;
                    components.Add(spacer);

                    if (extraSection.TextParts != null)
                    {
                        components.AddRange(VtmlUtil.Richtextify(capi, string.Join(", ", extraSection.TextParts) + "\n", CairoFont.WhiteSmallText()));
                    }
                    else
                    {
                        components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get(extraSection.Text) + "\n", CairoFont.WhiteSmallText()));
                    }

                }
            }

            string domainAndType = collObj.Code.Domain + ":" + stack.Class.Name();
            string code = collObj.Code.ToShortString();
            string langExtraSectionTitle = Lang.GetMatchingIfExists(domainAndType + "-handbooktitle-" + code);
            string langExtraSectionText = Lang.GetMatchingIfExists(domainAndType + "-handbooktext-" + code);

            if (langExtraSectionTitle != null || langExtraSectionText != null)
            {
                components.Add(new ClearFloatTextComponent(capi, MediumPadding));

                if (langExtraSectionTitle != null)
                {
                    components.Add(new RichTextComponent(capi, langExtraSectionTitle + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                    components.Add(new ClearFloatTextComponent(capi, TinyPadding));
                }
                if (langExtraSectionText != null)
                {
                    var spacer = new RichTextComponent(capi, "", CairoFont.WhiteSmallText());
                    spacer.PaddingLeft = TinyIndent;
                    components.Add(spacer);
                    components.AddRange(VtmlUtil.Richtextify(capi, langExtraSectionText + "\n", CairoFont.WhiteSmallText()));
                }
            }
        }

        protected List<RichTextComponentBase> BuildBarrelRecipesText(ICoreClientAPI capi, ItemStack stack, ActionConsumable<string> openDetailPageFor)
        {
            List<RichTextComponentBase> barrelRecipesTexts = new List<RichTextComponentBase>();

            List<BarrelRecipe> barrelRecipes = capi.GetBarrelRecipes();
            if (barrelRecipes.Count == 0) return barrelRecipesTexts;

            Dictionary<string, List<BarrelRecipe>> brecipesbyCode = new Dictionary<string, List<BarrelRecipe>>();
            foreach (var recipe in barrelRecipes)
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
                double sealHours = 0;

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

                    sealHours = recipes[i].SealHours;
                    outstacks[i] = recipes[i].Output.ResolvedItemstack;
                }

                int firstIndent = TinyIndent;
                for (int i = 0; i < ingredientsLen; i++)
                {
                    if (i > 0)
                    {
                        RichTextComponent cmp = new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText());
                        cmp.VerticalAlign = EnumVerticalAlign.Middle;
                        barrelRecipesTexts.Add(cmp);
                    }

                    SlideshowItemstackTextComponent scmp = new SlideshowItemstackTextComponent(capi, ingstacks[i], 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    scmp.ShowStackSize = true;
                    scmp.PaddingLeft = firstIndent;
                    firstIndent = 0;
                    barrelRecipesTexts.Add(scmp);
                }

                var eqcomp = new RichTextComponent(capi, " = ", CairoFont.WhiteMediumText());
                eqcomp.VerticalAlign = EnumVerticalAlign.Middle;
                barrelRecipesTexts.Add(eqcomp);
                var ocmp = new SlideshowItemstackTextComponent(capi, outstacks, 40, EnumFloat.Inline);

                ocmp.ShowStackSize = true;
                barrelRecipesTexts.Add(ocmp);

                string sealHoursText = (sealHours > 24.0) ? " " + Lang.Get("{0} days", Math.Round(sealHours / (double)capi.World.Calendar.HoursPerDay, 1)) : Lang.Get("{0} hours", Math.Round(sealHours));
                var hoursCmp = new RichTextComponent(capi, sealHoursText, CairoFont.WhiteSmallText());
                hoursCmp.VerticalAlign = EnumVerticalAlign.Middle;
                barrelRecipesTexts.Add(hoursCmp);

                barrelRecipesTexts.Add(new ClearFloatTextComponent(capi, 10));
            }

            return barrelRecipesTexts;
        }

        protected void addStorableInfo(ICoreClientAPI capi, ItemStack stack, List<RichTextComponentBase> components, float marginTop)
        {
            List<RichTextComponentBase> storableComps = new List<RichTextComponentBase>();


            if (stack.ItemAttributes?.IsTrue("moldrackable") == true)
            {
                AddPaddingAndRichText(storableComps, capi, "handbook-storable-moldrack");
            }
            if (stack.ItemAttributes?.IsTrue("shelvable") == true)
            {
                AddPaddingAndRichText(storableComps, capi, "handbook-storable-shelves");
            }
            if (stack.ItemAttributes?.IsTrue("displaycaseable") == true)
            {
                AddPaddingAndRichText(storableComps, capi, "handbook-storable-displaycase");
            }
            if (stack.Collectible.Tool != null || stack.ItemAttributes?["rackable"].AsBool() == true)
            {
                AddPaddingAndRichText(storableComps, capi, "handbook-storable-toolrack");
            }
            if (stack.Collectible.HasBehavior<CollectibleBehaviorGroundStorable>())
            {
                AddPaddingAndRichText(storableComps, capi, "handbook-storable-ground");
            }
            if (stack.ItemAttributes?["waterTightContainerProps"].Exists == true)
            {
                AddPaddingAndRichText(storableComps, capi, "handbook-storable-barrel");
            }

            if (storableComps.Count > 0)
            {
                bool haveText = components.Count > 0;
                AddHeading(components, capi, "Storable in/on", ref haveText);
                components.AddRange(storableComps);
            }
        }

        private void AddPaddingAndRichText(List<RichTextComponentBase> storableComps, ICoreClientAPI capi, string text)
        {
            storableComps.Add(new ClearFloatTextComponent(capi, TinyPadding));
            var spacer = new RichTextComponent(capi, "", CairoFont.WhiteSmallText());
            spacer.PaddingLeft = TinyIndent;
            storableComps.Add(spacer);
            storableComps.AddRange(VtmlUtil.Richtextify(capi, Lang.Get(text), CairoFont.WhiteSmallText()));
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
