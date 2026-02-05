using Cairo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

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

        protected TagSet fluxTag = TagSet.Empty;
        protected const string fluxTagName = "flux";

        public ExtraHandbookSection[] ExtraHandBookSections = null;
        ICoreAPI Api;

        InventorySmelting dummySmeltingInv;


        public CollectibleBehaviorHandbookTextAndExtraInfo(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.Api = api;

            fluxTag = api.TagsManager.GetGeneralTagSet(fluxTagName);

            dummySmeltingInv = new InventorySmelting("smelting-handbook", api);

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

            addGeneralInfo(inSlot, capi, stack, components, out float marginTop, out float marginBottom);

            List<ItemStack> breakBlocks = [];
            List<ItemStack> containers = [];
            List<ItemStack> fuels = [];
            List<ItemStack> molds = [];
            List<ItemStack> anvils = [];

            foreach (var aStack in allStacks)
            {
                if (aStack.ItemAttributes?.KeyExists("cookingContainerSlots") == true) containers.Add(aStack);

                var combustibleProps = aStack?.Collectible?.GetCombustibleProperties(Api.World, aStack, null);

                if (combustibleProps?.BurnDuration != null || combustibleProps?.BurnTemperature != null) fuels.Add(aStack);
                if (aStack.Collectible is BlockToolMold or BlockIngotMold) molds.Add(aStack);
                if (aStack.Collectible is BlockAnvil) anvils.Add(aStack);

                if (aStack.Block == null) continue;

                BlockDropItemStack[] droppedStacks = aStack.Block.GetDropsForHandbook(aStack, capi.World.Player);
                if (droppedStacks == null) continue;

                for (int i = 0; i < droppedStacks.Length; i++)
                {
                    BlockDropItemStack dstack = droppedStacks[i];
                    ItemStack droppedStack = droppedStacks[i].ResolvedItemstack;

                    if (droppedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                    {
                        breakBlocks.Add(aStack);
                    }
                }
            }

            addDropsInfo(capi, openDetailPageFor, stack, components, marginTop, breakBlocks);
            bool haveText = addObtainedThroughInfo(capi, allStacks, openDetailPageFor, stack, components, marginTop, breakBlocks);
            haveText = addFoundInInfo(capi, openDetailPageFor, stack, components, marginTop, haveText);
            haveText = addAlloyForInfo(capi, openDetailPageFor, stack, components, marginTop, containers, fuels, haveText);
            haveText = addAlloyedFromInfo(capi, allStacks, openDetailPageFor, stack, components, marginTop, containers, fuels, haveText);
            haveText = addProcessesIntoInfo(capi, openDetailPageFor, stack, components, marginTop, marginBottom, containers, fuels, haveText);
            haveText = addProcessorForInfo(capi, allStacks, openDetailPageFor, stack, components, marginTop, marginBottom, containers, fuels, molds, anvils, haveText);
            haveText = addIngredientForInfo(capi, allStacks, openDetailPageFor, stack, components, marginTop, containers, fuels, molds, haveText);
            haveText = addCreatedByInfo(capi, allStacks, openDetailPageFor, stack, components, marginTop, containers, fuels, molds, anvils, haveText);
            addExtraSections(capi, stack, components, marginTop);
            addEatenByInfo(capi, stack, components, marginTop);
            addStorableInfo(capi, allStacks, openDetailPageFor, stack, components, marginTop);
            addStoredInInfo(capi, allStacks, openDetailPageFor, stack, components, marginTop);

            collObj.GetCollectibleInterface<ICustomHandbookPageContent>()?.OnHandbookPageComposed(components, inSlot, capi, allStacks, openDetailPageFor);

            return components.ToArray();
        }

        protected void addGeneralInfo(ItemSlot inSlot, ICoreClientAPI capi, ItemStack stack, List<RichTextComponentBase> components, out float marginTop, out float marginBottom)
        {
            components.Add(new ItemstackTextComponent(capi, stack, 100, 10, EnumFloat.Left));
            components.AddRange(VtmlUtil.Richtextify(capi, stack.GetName() + "\n", CairoFont.WhiteSmallishText()));
            if (capi.Settings.Bool["extendedDebugInfo"] == true)
            {
                var font = CairoFont.WhiteDetailText();
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
                Dictionary<int, List<ItemStack>> dropsStacksByTool = [];

                if (stack.Block.GetDropsForHandbook(stack, capi.World.Player) is { } blockDropStacks)
                {
                    foreach (var val in blockDropStacks)
                    {
                        int tool = val.Tool == null ? -1 : (int)val.Tool;

                        if (dropsStacksByTool.TryGetValue(tool, out var dropsStacks))
                        {
                            dropsStacks.Add(val.ResolvedItemstack);
                        }
                        else dropsStacksByTool.Add(tool, [val.ResolvedItemstack]);
                    }
                }

                if (dropsStacksByTool.Count > 0)
                {
                    for (int j = 0; j < breakBlocks.Count; j++)
                    {
                        if (dropsStacksByTool.Any(e => e.Value.Any(dstack => dstack.Equals(capi.World, breakBlocks[j], GlobalConstants.IgnoredStackAttributes))))
                        {
                            // No need to display the same info twice
                            breakBlocks.RemoveAt(j);
                        }
                    }

                    bool haveText = components.Count > 0;
                    AddHeading(components, capi, "Drops when broken", ref haveText);
                    components.Add(new ClearFloatTextComponent(capi, TinyPadding));
                    foreach ((int toolInt, var dropsStacks) in dropsStacksByTool)
                    {
                        EnumTool? tool = toolInt == -1 ? null : (EnumTool)toolInt;
                        var toolStacks = tool == null ? null : ObjectCacheUtil.GetToolStacks(capi, (EnumTool)tool);
                        while (dropsStacks.Count > 0)
                        {
                            ItemStack dstack = dropsStacks[0];
                            dropsStacks.RemoveAt(0);

                            if (dstack == null) continue;

                            SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, dropsStacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                            if (toolStacks != null)
                            {
                                comp.ExtraTooltipText = "\n\n<font color=\"orange\">" + Lang.Get("break-requires-tool-" + tool.ToString()!.ToLowerInvariant()) + "</font>";
                            }

                            components.Add(comp);

                            if (toolStacks == null) continue;

                            components.Add(new SlideshowItemstackTextComponent(capi, toolStacks, 24, EnumFloat.Left, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)))
                            {
                                renderOffset = { X = -(float)GuiElement.scaled(17), Z = 100 },
                                ShowTooltip = false
                            });
                        }
                    }

                    components.Add(new ClearFloatTextComponent(capi, TinyPadding));
                }

                List<ItemStack> harvestedStacks = [];
                EnumTool? harvestTool = null;
                if (stack.Block.GetBehavior<BlockBehaviorHarvestable>() is { harvestedStacks: { } harvestDropStacks } bhh)
                {
                    harvestTool = bhh.Tool;
                    harvestedStacks = [.. harvestDropStacks.Select(val => val.ResolvedItemstack).Where(hstack => hstack != null)];
                }

                if (harvestedStacks.Count > 0)
                {
                    bool haveText = components.Count > 0;
                    AddHeading(components, capi, "handbook-dropswhen-harvested", ref haveText);
                    components.Add(new ClearFloatTextComponent(capi, TinyPadding));
                    var toolStacks = harvestTool == null ? null : ObjectCacheUtil.GetToolStacks(capi, (EnumTool)harvestTool);
                    while (harvestedStacks.Count > 0)
                    {
                        ItemStack hstack = harvestedStacks[0];
                        harvestedStacks.RemoveAt(0);

                        if (hstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, hstack, harvestedStacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        if (toolStacks != null)
                        {
                            comp.ExtraTooltipText = "\n\n<font color=\"orange\">" + Lang.Get("harvest-requires-tool-" + harvestTool.ToString()!.ToLowerInvariant()) + "</font>";
                        }

                        components.Add(comp);

                        if (toolStacks == null) continue;

                        components.Add(new SlideshowItemstackTextComponent(capi, toolStacks, 24, EnumFloat.Left, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)))
                        {
                            renderOffset = { X = -(float)GuiElement.scaled(17), Z = 100 },
                            ShowTooltip = false
                        });
                    }

                    components.Add(new ClearFloatTextComponent(capi, TinyPadding));
                }
            }
        }

        protected bool addObtainedThroughInfo(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, List<ItemStack> breakBlocks)
        {
            List<ItemStack> harvestBlocks = [];
            List<string> killCreatures = [];
            List<string> harvestCreatures = [];
            HashSet<string> harvestCreatureCodes = [];

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

            foreach (var aStack in allStacks)
            {
                if (aStack.Block?.GetBehavior<BlockBehaviorHarvestable>()?.harvestedStacks is not BlockDropItemStack[] harvestedStacks) continue;

                if (harvestedStacks.Any(hStack => hStack?.ResolvedItemstack?.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) == true))
                {
                    harvestBlocks.Add(aStack);
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
                AddHeading(components, capi, "handbook-obtainedby-killing-harvesting", ref haveText);
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

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, breakBlocks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    components.Add(comp);
                }

                components.Add(new ClearFloatTextComponent(capi, TinyPadding));
            }

            if (harvestBlocks.Count > 0)
            {
                AddHeading(components, capi, "handbook-obtainedby-block-harvesting", ref haveText);
                components.Add(new ClearFloatTextComponent(capi, TinyPadding));

                while (harvestBlocks.Count > 0)
                {
                    ItemStack hstack = harvestBlocks[0];
                    harvestBlocks.RemoveAt(0);
                    if (hstack == null) continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, hstack, harvestBlocks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
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

                API.Datastructures.OrderedDictionary<string, List<ItemStack>> blocks = new();

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
                    var comp = new SlideshowItemstackTextComponent(capi, val.Value.ToArray(), 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    comp.PaddingLeft = firstPadding;
                    firstPadding = 0;
                    components.Add(comp);
                }
            }


            if (collObj.Attributes?["hostRock"].Exists == true)
            {
                AddHeading(components, capi, "Occurs in host rock", ref haveText);

                ushort[] blockids = collObj.Attributes?["hostRock"].AsArray<ushort>();

                API.Datastructures.OrderedDictionary<string, List<ItemStack>> blocks = new();

                for (int i = 0; i < blockids.Length; i++)
                {
                    Block block = capi.World.Blocks[blockids[i]];

                    string key = block.Code.FirstCodePart();
                    if (block.Attributes?["handbook"]["groupBy"].Exists == true)
                    {
                        key = block.Attributes["handbook"]["groupBy"].AsArray<string>()[0];
                    }

                    if (block.Attributes?["handbook"]?["exclude"]?.AsBool() ?? false) continue;

                    if (!blocks.ContainsKey(key))
                    {
                        blocks[key] = new List<ItemStack>();
                    }

                    blocks[key].Add(new ItemStack(block));
                }

                int firstPadding = TinyIndent;
                foreach (var val in blocks)
                {
                    var comp = new SlideshowItemstackTextComponent(capi, val.Value.ToArray(), 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    comp.PaddingLeft = firstPadding;
                    firstPadding = 0;
                    components.Add(comp);
                }
            }

            return haveText;
        }

        protected bool addAlloyForInfo(ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, List<ItemStack> containers, List<ItemStack> fuels, bool haveText)
        {
            CombustibleProperties combustibleProps = stack.Collectible.GetCombustibleProperties(Api.World, stack, null);
            Dictionary<AssetLocation, ItemStack> alloyables = new Dictionary<AssetLocation, ItemStack>();
            foreach (var val in capi.GetMetalAlloys())
            {
                foreach (var ing in val.Ingredients)
                {
                    if (ing.ResolvedItemStack.Equals(capi.World, combustibleProps?.SmeltedStack?.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes) && getCanContainerSmelt(capi, containers, fuels, stack))
                    {
                        var outputColl = val.Output.ResolvedItemStack.Collectible;
                        var bitCode = new AssetLocation(outputColl.Code.Domain, "metalbit-" + outputColl.Variant["metal"]);
                        alloyables[bitCode] = new ItemStack(capi.World.GetItem(bitCode));
                    }
                }
            }

            if (alloyables.Count > 0)
            {
                AddHeading(components, capi, "Alloy for", ref haveText);

                int firstPadding = TinyIndent;
                foreach (var val in alloyables)
                {
                    var comp = new ItemstackTextComponent(capi, val.Value, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    comp.PaddingLeft = firstPadding;
                    firstPadding = 0;
                    components.Add(comp);
                }

                haveText = true;
            }

            return haveText;
        }

        protected bool addAlloyedFromInfo(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, List<ItemStack> containers, List<ItemStack> fuels, bool haveText)
        {
            CombustibleProperties combustibleProps = stack.Collectible.GetCombustibleProperties(Api.World, stack, null);
            var ingotStack = combustibleProps?.SmeltedStack?.ResolvedItemStack;
            if (stack.Collectible.FirstCodePart() != "metalbit" || ingotStack == null) return haveText;

            Dictionary<AssetLocation, MetalAlloyIngredient[]> alloyableFrom = new Dictionary<AssetLocation, MetalAlloyIngredient[]>();
            foreach (var val in capi.GetMetalAlloys())
            {
                if (val.Output.ResolvedItemStack.Equals(capi.World, ingotStack, GlobalConstants.IgnoredStackAttributes))
                {
                    List<MetalAlloyIngredient> ingreds = [.. val.Ingredients];
                    alloyableFrom[val.Output.ResolvedItemStack.Collectible.Code] = ingreds.ToArray();
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

                        ItemStack[] ores = allStacks.Where(aStack => ingred.ResolvedItemStack.Equals(capi.World, aStack.Collectible.GetCombustibleProperties(Api.World, aStack, null)?.SmeltedStack?.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes) && getCanContainerSmelt(capi, containers, fuels, aStack)).ToArray();
                        if (ores?.Length == 0) continue;
                        ItemstackComponentBase comp = new SlideshowItemstackTextComponent(capi, ores, 30, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
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

        protected bool addProcessesIntoInfo(ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, float marginBottom, List<ItemStack> containers, List<ItemStack> fuels, bool haveText)
        {
            // Bakes into
            var combustibleProps = collObj.GetCombustibleProperties(Api.World, stack, null);

            if (collObj.Attributes?["bakingProperties"]?.AsObject<BakingProperties>() is BakingProperties bp && bp.ResultCode != null)
            {
                var item = capi.World.GetItem(new AssetLocation(bp.ResultCode));
                if (item != null)
                {
                    AddHeading(components, capi, "smeltdesc-bake-title", ref haveText);

                    var cmp = new ItemstackTextComponent(capi, new ItemStack(item), 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    cmp.ShowStacksize = true;
                    cmp.PaddingLeft = TinyIndent;
                    components.Add(cmp);
                    components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
                }
            }
            else
            // Smelts into

            if (combustibleProps?.SmeltedStack?.ResolvedItemStack != null && !combustibleProps.SmeltedStack.ResolvedItemStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                if (getCanSmelt(fuels, stack) || getCanBloomerySmelt(stack))
                {
                    string smelttype = combustibleProps.SmeltingType.ToString().ToLowerInvariant();
                    AddHeading(components, capi, "game:smeltdesc-" + smelttype + "-title", ref haveText);

                    var cmp = new ItemstackTextComponent(capi, combustibleProps.SmeltedStack.ResolvedItemStack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    cmp.ShowStacksize = true;
                    cmp.PaddingLeft = TinyIndent;
                    components.Add(cmp);
                    components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
                }
            }

            // Fires into in beehive kiln

            if (collObj.Attributes?["beehivekiln"].Exists == true)
            {
                Dictionary<string, JsonItemStack> beehivekilnProps = collObj.Attributes["beehivekiln"].AsObject<Dictionary<string, JsonItemStack>>();

                AddHeading(components, capi, "smeltdesc-beehivekiln-title", ref haveText);

                if (beehivekilnProps.DistinctBy(e => e.Value.Code).Count() == 1)
                {
                    var firesIntoStack = beehivekilnProps.FirstOrDefault().Value;
                    if (firesIntoStack?.Resolve(capi.World, "beehivekiln-burn") == true)
                    {
                        components.Add(new ItemstackTextComponent(capi, firesIntoStack.ResolvedItemStack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs))));
                        components.Add(new ClearFloatTextComponent(capi, marginBottom));
                    }
                }
                else
                {
                    foreach ((string doorOpen, JsonItemStack firesIntoStack) in beehivekilnProps)
                    {
                        if (firesIntoStack?.Resolve(capi.World, "beehivekiln-burn") == true)
                        {
                            components.Add(new ItemstackTextComponent(capi, firesIntoStack.ResolvedItemStack, 40, 2, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs))));
                            components.Add(new RichTextComponent(capi, Lang.Get("smeltdesc-beehivekiln-opendoors", doorOpen), CairoFont.WhiteDetailText()) { VerticalAlign = EnumVerticalAlign.Middle, PaddingRight = -5 });
                            components.Add(new ItemstackTextComponent(capi, new ItemStack(capi.World.GetBlock("cokeovendoor-closed-north")), 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs))));
                            components.Add(new ClearFloatTextComponent(capi, marginBottom));
                        }
                    }
                }
            }


            // Carburizes into
            if (collObj.Attributes?["carburizableProps"]?["carburizedOutput"]?.Exists == true)
            {
                JsonItemStack carburizedJStack = stack.ItemAttributes["carburizableProps"]["carburizedOutput"].AsObject<JsonItemStack>(null, stack.Collectible.Code.Domain);
                if (carburizedJStack?.Resolve(Api.World, "carburizable handbook") == true && !carburizedJStack.ResolvedItemStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    AddHeading(components, capi, "carburizesdesc-title", ref haveText);

                    var cmp = new ItemstackTextComponent(capi, carburizedJStack.ResolvedItemStack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    cmp.ShowStacksize = true;
                    cmp.PaddingLeft = TinyIndent;
                    components.Add(cmp);
                    components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
                }
            }


            // Pulverizes into
            var crushingProps = stack.Collectible.GetCrushingProperties(Api.World, stack);

            if (crushingProps?.CrushedStack?.ResolvedItemStack != null && !crushingProps.CrushedStack.ResolvedItemStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                AddHeading(components, capi, "pulverizesdesc-title", ref haveText);

                var cmp = new ItemstackTextComponent(capi, crushingProps.CrushedStack.ResolvedItemStack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                cmp.ShowStacksize = true;
                cmp.PaddingLeft = TinyIndent;
                components.Add(cmp);
                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }


            // Grinds into
            var grindingProps = collObj.GetGrindingProperties(Api.World, stack);

            if (grindingProps?.GroundStack?.ResolvedItemStack != null && !grindingProps.GroundStack.ResolvedItemStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                AddHeading(components, capi, "Grinds into", ref haveText);

                var cmp = new ItemstackTextComponent(capi, grindingProps.GroundStack.ResolvedItemStack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                cmp.ShowStacksize = true;
                cmp.PaddingLeft = TinyIndent;
                components.Add(cmp);
                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }

            // Juices into
            JuiceableProperties jprops = getjuiceableProps(stack);
            var litresLeft = stack.Attributes?.GetDouble("juiceableLitresLeft") ?? 0;
            if (jprops != null && (jprops.LitresPerItem != null || litresLeft > 0))
            {
                AddHeading(components, capi, "Juices into", ref haveText);

                var jstack = jprops.LiquidStack.ResolvedItemStack.Clone();
                if (jprops.LitresPerItem != null)
                {
                    jstack.StackSize = (int)(100 * jprops.LitresPerItem);
                }
                if (litresLeft > 0)
                {
                    jstack.StackSize = (int)(100 * litresLeft);
                }
                var cmp = new ItemstackTextComponent(capi, jstack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                cmp.ShowStacksize = jprops.LitresPerItem != null;
                cmp.PaddingLeft = TinyIndent;
                components.Add(cmp);

                if (jprops.ReturnStack?.ResolvedItemStack == null)
                {
                    if (!stack.Equals(capi.World, jprops.PressedStack.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes))
                    {
                        var pstack = jprops.PressedStack.ResolvedItemStack.Clone();
                        cmp = new ItemstackTextComponent(capi, pstack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        cmp.PaddingLeft = 0;
                        components.Add(cmp);
                    }
                }
                else
                {
                    var rstack = jprops.ReturnStack.ResolvedItemStack.Clone();
                    if (jprops.LitresPerItem != null)
                    {
                        rstack.StackSize /= (int)(1 / jprops.LitresPerItem);
                    }
                    cmp = new ItemstackTextComponent(capi, rstack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    cmp.ShowStacksize = jprops.LitresPerItem != null;
                    cmp.PaddingLeft = 0;
                    components.Add(cmp);
                }

                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }

            // Squeezes into
            if (stack.Collectible.GetCollectibleBehavior<CollectibleBehaviorSqueezable>(true) is CollectibleBehaviorSqueezable cbs)
            {
                AddHeading(components, capi, "squeezesdesc-title", ref haveText);
                var indent = TinyIndent;

                if (cbs.SqueezedLiquid != null)
                {
                    var lstack = new ItemStack(cbs.SqueezedLiquid);
                    lstack.StackSize = (int)(100 * cbs.SqueezedLitres);
                    var cmp = new ItemstackTextComponent(capi, lstack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    cmp.ShowStacksize = true;
                    cmp.PaddingLeft = indent;
                    components.Add(cmp);
                    indent = 0;
                }

                foreach (var returnStack in cbs.ReturnStacks)
                {
                    if (returnStack?.ResolvedItemStack == null) continue;
                    if (!stack.Equals(capi.World, returnStack.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes))
                    {
                        var cmp = new ItemstackTextComponent(capi, returnStack.ResolvedItemStack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        cmp.ShowStacksize = true;
                        cmp.PaddingLeft = indent;
                        components.Add(cmp);
                        indent = 0;
                    }
                }

                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }

            // Ground processes into
            if (stack.Collectible.GetCollectibleBehavior<CollectibleBehaviorGroundStoredProcessable>(true) is { } gsp)
            {
                List<ItemStack> processedStacks = [..gsp.ProcessedStacks?.Select(pstack => pstack.ResolvedItemstack) ?? []];
                if (gsp.RemainingItem?.ResolvedItemStack is { } rStack) processedStacks.Insert(0, rStack);
                string extraTooltipText = null;
                ItemStack[] toolStacks = null;
                if (gsp.Tool is { } tool)
                {
                    extraTooltipText = "\n\n<font color=\"orange\">" + Lang.Get("processing-requires-tool-" + tool.ToString()!.ToLowerInvariant()) + "</font>";
                    toolStacks = ObjectCacheUtil.GetToolStacks(capi, tool);
                }

                if (processedStacks.Count > 0)
                {
                    AddHeading(components, capi, gsp.HandbookProcessIntoTitle, ref haveText);
                    var indent = TinyIndent;

                    while (processedStacks.Count > 0)
                    {
                        ItemStack pstack = processedStacks[0];
                        processedStacks.RemoveAt(0);

                        if (pstack == null) continue;

                        components.Add(new SlideshowItemstackTextComponent(capi, pstack, processedStacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)))
                        {
                            ShowStackSize = true,
                            PaddingLeft = indent,
                            ExtraTooltipText = extraTooltipText
                        });
                        indent = 0;

                        if (toolStacks == null) continue;

                        components.Add(new SlideshowItemstackTextComponent(capi, toolStacks, 24, EnumFloat.Left, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)))
                        {
                            renderOffset = { X = -(float)GuiElement.scaled(17), Z = 100 },
                            ShowTooltip = false
                        });
                    }

                    components.Add(new ClearFloatTextComponent(capi, marginBottom)); //nice margin below the item graphic
                }
            }

            // Ore smashes into
            if (stack.Collectible is ItemOre)
            {
                int units = stack.ItemAttributes["metalUnits"].AsInt(5);
                string type = stack.Collectible.Variant["ore"].Replace("quartz_", "").Replace("galena_", "");

                if (capi.World.GetItem(new AssetLocation("nugget-" + type)) is { } item)
                {
                    AddHeading(components, capi, "oresmasheddesc-title", ref haveText);

                    components.Add(new ItemstackTextComponent(capi, new(item, Math.Max(1, units / 5)), 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)))
                    {
                        ShowStacksize = true,
                        PaddingLeft = TinyIndent
                    });
                    components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
                }
            }

            // Distills into
            DistillationProps dprops = getDistillationProps(stack);
            if (dprops != null)
            {
                AddHeading(components, capi, "squeezesdesc-title", ref haveText);

                var dstack = dprops.DistilledStack?.ResolvedItemStack.Clone();
                if (dprops.Ratio != 0)
                {
                    dstack.StackSize = (int)(100 * stack.StackSize * dprops.Ratio);
                }
                var cmp = new ItemstackTextComponent(capi, dstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                cmp.ShowStacksize = dprops.Ratio != 0;
                cmp.PaddingLeft = TinyIndent;
                components.Add(cmp);
                components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }


            TransitionableProperties[] props = collObj.GetTransitionableProperties(capi.World, stack, null);

            if (props != null)
            {
                haveText = true;
                var verticalSpace = new ClearFloatTextComponent(capi, MediumPadding);

                foreach (var prop in props)
                {
                    if (prop.Type is EnumTransitionType.None) continue;

                    float total = prop.FreshHours.avg + prop.TransitionHours.avg;
                    string displayTime = "-hours";
                    if (Math.Round(total, 0, MidpointRounding.AwayFromZero) >= capi.World.Calendar.HoursPerDay)
                    {
                        total /= capi.World.Calendar.HoursPerDay;
                        total = Math.Max(1, total);
                        displayTime = "-days";

                        if (Math.Round(total, 0, MidpointRounding.AwayFromZero) >= capi.World.Calendar.DaysPerYear)
                        {
                            total /= capi.World.Calendar.DaysPerYear;
                            total = Math.Max(1, total);
                            displayTime = "-years";
                        }
                        else total = (float)Math.Round(total, 0, MidpointRounding.AwayFromZero);
                    }
                    else if (total > 1f) total = (float)Math.Round(total, 0, MidpointRounding.AwayFromZero);

                    components.Add(verticalSpace);
                    components.Add(new RichTextComponent(capi, Lang.Get("handbook-processesinto-transition-" + prop.Type.ToString().ToLowerInvariant() + displayTime, total) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                    components.Add(new ItemstackTextComponent(capi, prop.TransitionedStack.ResolvedItemStack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs))) { PaddingLeft = TinyIndent });
                }
                if (props.Length > 0) components.Add(new ClearFloatTextComponent(capi, marginBottom));  //nice margin below the item graphic
            }

            return haveText;
        }

        public bool addProcessorForInfo(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, float marginBottom, List<ItemStack> containers, List<ItemStack> fuels, List<ItemStack> molds, List<ItemStack> anvils, bool haveText)
        {
            if (stack.Block is BlockToolMold toolMold)
            {
                List<ItemStack> outputs = [];
                var metalsByCode = capi.ModLoader.GetModSystem<SurvivalCoreSystem>().metalsByCode;

                foreach (var metal in metalsByCode.Keys)
                {
                    if (capi.World.GetItem("metalbit-" + metal) is not Item bitObj) continue;
                    ItemStack bitStack = new(bitObj);
                    if (!getCanContainerSmelt(capi, containers, fuels, bitStack)) continue;
                    outputs.AddRange(getMoldedOutput(stack, [bitStack]));
                }

                if (outputs.Count > 0)
                {
                    AddHeading(components, capi, "handbook-processorfor-metalmolding-title", ref haveText);

                    int firstPadding = TinyPadding;
                    while (outputs.Count > 0)
                    {
                        ItemStack dstack = outputs[0];
                        outputs.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, outputs, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }
            }

            if (stack.Block is BlockIngotMold)
            {
                List<ItemStack> outputs = [];

                foreach (var val in allStacks)
                {
                    if (val.Collectible is not ItemIngot) continue;
                    if (capi.World.GetItem("metalbit-" + val.Collectible.LastCodePart()) is not Item bitObj) continue;
                    if (!getCanContainerSmelt(capi, containers, fuels, new(bitObj))) continue;
                    outputs.Add(val);
                }

                if (outputs.Count > 0)
                {
                    AddHeading(components, capi, "handbook-processorfor-metalmolding-title", ref haveText);

                    int firstPadding = TinyPadding;
                    while (outputs.Count > 0)
                    {
                        ItemStack dstack = outputs[0];
                        outputs.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, outputs, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }
            }

            if (stack.Block is BlockFruitPress)
            {
                List<ItemStack> outputs = [];

                foreach (var val in allStacks)
                {
                    if (getjuiceableProps(val) is not JuiceableProperties jprops) continue;
                    var litresLeft = val.Attributes?.GetDouble("juiceableLitresLeft") ?? 0;
                    var jstack = jprops.LiquidStack.ResolvedItemStack.Clone();
                    if (jprops.LitresPerItem != null)
                    {
                        jstack.StackSize = (int)(100 * jprops.LitresPerItem);
                    }
                    if (litresLeft > 0)
                    {
                        jstack.StackSize = (int)(100 * litresLeft);
                    }

                    if (!val.Equals(capi.World, jstack, GlobalConstants.IgnoredStackAttributes) && !outputs.Any(s => s.Equals(capi.World, jstack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        outputs.Add(jstack);
                    }

                    if (jprops.ReturnStack?.ResolvedItemStack == null)
                    {
                        if (!val.Equals(capi.World, jprops.PressedStack.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes))
                        {
                            var pstack = jprops.PressedStack.ResolvedItemStack.Clone();
                            if (!val.Equals(capi.World, jstack, GlobalConstants.IgnoredStackAttributes) && !outputs.Any(s => s.Equals(capi.World, pstack, GlobalConstants.IgnoredStackAttributes)))
                            {
                                outputs.Add(pstack);
                            }
                        }
                    }
                    else
                    {
                        var rstack = jprops.ReturnStack.ResolvedItemStack.Clone();
                        if (jprops.LitresPerItem != null)
                        {
                            rstack.StackSize /= (int)(1 / jprops.LitresPerItem);
                        }

                        if (!val.Equals(capi.World, jstack, GlobalConstants.IgnoredStackAttributes) && !outputs.Any(s => s.Equals(capi.World, rstack, GlobalConstants.IgnoredStackAttributes)))
                        {
                            outputs.Add(rstack);
                        }

                        var pstack = jprops.PressedStack.ResolvedItemStack.Clone();
                        pstack.Attributes.SetDouble("juiceableLitresLeft", 1);

                        if (!val.Equals(capi.World, pstack, GlobalConstants.IgnoredStackAttributes) && !outputs.Any(s => s.Equals(capi.World, pstack, GlobalConstants.IgnoredStackAttributes)))
                        {
                            outputs.Add(pstack);
                        }
                    }
                }

                if (outputs.Count > 0)
                {
                    AddHeading(components, capi, "handbook-processorfor-juicing-title", ref haveText);

                    int firstPadding = TinyPadding;
                    while (outputs.Count > 0)
                    {
                        ItemStack dstack = outputs[0];
                        outputs.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, outputs, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }
            }

            if (stack.Block is BlockAnvil anvil)
            {
                List<ItemStack> outputs = [];

                foreach (var val in capi.GetSmithingRecipes())
                {
                    if (!outputs.Any(s => s.Equals(capi.World, val.Output.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        var workable = val.Ingredient.ResolvedItemStack.Collectible.GetCollectibleInterface<IAnvilWorkable>();
                        if (workable?.GetRequiredAnvilTier(val.Ingredient.ResolvedItemStack) > anvil.MetalTier) continue;

                        outputs.Add(val.Output.ResolvedItemStack);
                    }
                }

                if (outputs.Count > 0)
                {
                    AddHeading(components, capi, "handbook-processorfor-smithing-title", ref haveText);

                    int firstPadding = TinyPadding;
                    while (outputs.Count > 0)
                    {
                        ItemStack dstack = outputs[0];
                        outputs.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, outputs, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }
            }

            return haveText;
        }


        protected bool addIngredientForInfo(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, List<ItemStack> containers, List<ItemStack> fuels, List<ItemStack> molds, bool haveText)
        {
            ItemStack maxstack = stack.Clone();
            maxstack.StackSize = maxstack.Collectible.MaxStackSize * 10; // because SatisfiesAsIngredient() tests for stacksize. Times 10 because liquid portion oddities

            List<ItemStack> recipestacks = new List<ItemStack>();


            foreach (var recval in capi.World.GridRecipes)
            {
                foreach (var val in recval.ResolvedIngredients)
                {
                    CraftingRecipeIngredient ingred = val;

                    if (ingred != null && ingred.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, recval.RecipeOutput.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        DummySlot outSlot = new DummySlot();
                        DummySlot[] inSlots = new DummySlot[recval.Width * recval.Height];

                        for (int x = 0; x < recval.Width; x++)
                        {
                            for (int y = 0; y < recval.Height; y++)
                            {
                                CraftingRecipeIngredient inIngred = recval.GetElementInGrid(y, x, recval.ResolvedIngredients, recval.Width);
                                ItemStack ingredStack = inIngred?.ResolvedItemStack?.Clone();
                                if (inIngred == val) ingredStack = maxstack;

                                inSlots[y * recval.Width + x] = new DummySlot(ingredStack);
                            }
                        }

                        recval.GenerateOutputStack(inSlots, outSlot);
                        recipestacks.Add(outSlot.Itemstack);
                    }

                    ItemStack returnedStack = ingred?.ReturnedStack?.ResolvedItemStack;
                    if (returnedStack?.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) != false) continue;
                    if (!recipestacks.Any(s => s.Equals(capi.World, returnedStack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        if (recval.ResolvedIngredients.Any(ingred => ingred?.SatisfiesAsIngredient(maxstack) == true)) recipestacks.Add(returnedStack);
                    }
                }
            }


            foreach (var val in capi.GetSmithingRecipes())
            {
                if (val.Ingredient.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, val.Output.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes)))
                {
                    if (!getIsAnvilWorkable(stack, fuels)) continue;

                    recipestacks.Add(val.Output.ResolvedItemStack);
                }
            }


            foreach (var val in capi.GetClayformingRecipes())
            {
                if (val.Ingredient.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, val.Output.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes)))
                {
                    recipestacks.Add(val.Output.ResolvedItemStack);
                }
            }


            foreach (var val in capi.GetKnappingRecipes())
            {
                if (val.Ingredient.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, val.Output.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes)))
                {
                    recipestacks.Add(val.Output.ResolvedItemStack);
                }
            }


            foreach (var recipe in capi.GetBarrelRecipes())
            {
                foreach (var ingred in recipe.Ingredients)
                {
                    if (ingred.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, recipe.RecipeOutput.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        recipestacks.Add(recipe.RecipeOutput.ResolvedItemStack);
                    }
                }
            }

            foreach (var recipe in capi.GetCookingRecipes())
            {
                if (recipe.CooksInto?.ResolvedItemStack == null) continue;
                foreach (var ingred in recipe.Ingredients)
                {
                    if (ingred.GetMatchingStack(stack) != null)
                    {
                        recipestacks.Add(recipe.CooksInto.ResolvedItemStack);
                    }
                }
            }

            if (stack.Collectible is BlockAnvilPart)
            {
                recipestacks.Add(new ItemStack(Api.World.GetBlock(new AssetLocation("anvil-" + stack.Collectible.Variant["metal"]))));
            }

            if (stack.ItemAttributes?.IsTrue("isFlux") == true || stack?.Collectible?.GetTags(stack).IsSupersetOf(fluxTag) == true)
            {
                foreach (var val in capi.World.Blocks.Where(block => block is BlockAnvilPart))
                {
                    var anvil = new ItemStack(Api.World.GetBlock(new AssetLocation("anvil-" + val.Variant["metal"])));
                    if (!recipestacks.Any(s => s.Equals(capi.World, anvil, GlobalConstants.IgnoredStackAttributes)))
                    {
                        recipestacks.Add(anvil);
                    }
                }
            }

            CombustibleProperties combustibleProps = stack.Collectible.GetCombustibleProperties(Api.World, stack, null);
            if (combustibleProps?.SmeltedStack?.ResolvedItemStack.Collectible is ItemIngot ingot)
            {
                foreach (var mold in molds)
                {
                    if (!getCanContainerSmelt(capi, containers, fuels, stack)) break;

                    var smeltedStack = combustibleProps?.SmeltedStack?.ResolvedItemStack;
                    if (smeltedStack != null && mold.Collectible is BlockIngotMold && !recipestacks.Any(s => s.Equals(capi.World, smeltedStack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        recipestacks.Add(smeltedStack);
                        continue;
                    }

                    string metaltype = ingot.LastCodePart();
                    if (mold.ItemAttributes?["drop"]?.AsObject<JsonItemStack>(null, mold.Collectible.Code.Domain) is JsonItemStack dropStack)
                    {
                        dropStack.Code.Path = dropStack.Code.Path.Replace("{metal}", metaltype);

                        dropStack.Resolve(capi.World, "handbookmolds", false);
                        if (dropStack.ResolvedItemStack != null && !recipestacks.Any(s => s.Equals(capi.World, dropStack.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes)))
                        {
                            recipestacks.Add(dropStack.ResolvedItemStack);
                        }
                    }
                    else if (mold.ItemAttributes?["drops"].AsObject<JsonItemStack[]>(null, mold.Collectible.Code.Domain) is JsonItemStack[] dropStacks)
                    {
                        foreach (var dstack in dropStacks)
                        {
                            dstack.Code.Path = dstack.Code.Path.Replace("{metal}", metaltype);

                            dstack.Resolve(capi.World, "handbookmolds", false);
                            if (dstack.ResolvedItemStack != null && !recipestacks.Any(s => s.Equals(capi.World, dstack.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes)))
                            {
                                recipestacks.Add(dstack.ResolvedItemStack);
                            }
                        }
                    }
                }
            }

            if (getjuiceableProps(stack) is JuiceableProperties jprops)
            {
                var pstack = jprops.PressedStack.ResolvedItemStack.Clone();
                pstack.Attributes.SetDouble("juiceableLitresLeft", 1);

                if (jprops.ReturnStack != null && !stack.Equals(capi.World, pstack, GlobalConstants.IgnoredStackAttributes) && !recipestacks.Any(s => s.Equals(capi.World, pstack, GlobalConstants.IgnoredStackAttributes)))
                {
                    recipestacks.Add(pstack);
                }
            }


            List<CookingRecipe> cookingrecipes = new List<CookingRecipe>();
            foreach (CookingRecipe recipe in capi.GetCookingRecipes())
            {
                if (recipe.CooksInto?.ResolvedItemStack != null) continue;
                foreach (var ingred in recipe.Ingredients)
                {
                    if (!cookingrecipes.Contains(recipe) && ingred.GetMatchingStack(stack) != null)
                    {
                        cookingrecipes.Add(recipe);
                    }
                }
            }

            List<CookingRecipe> pierecipes = new List<CookingRecipe>();

            foreach (CookingRecipe recipe in BlockPie.GetHandbookRecipes(capi, allStacks))
            {
                if (recipe.CooksInto?.ResolvedItemStack != null) continue;
                foreach (var ingred in recipe.Ingredients)
                {
                    if (!pierecipes.Contains(recipe) && ingred.GetMatchingStack(stack) != null)
                    {
                        pierecipes.Add(recipe);
                    }
                }
            }

            if (recipestacks.Count > 0 || cookingrecipes.Count > 0 || pierecipes.Count > 0)
            {
                AddHeading(components, capi, "Ingredient for", ref haveText);
                components.Add(new ClearFloatTextComponent(capi, TinyPadding));

                while (recipestacks.Count > 0)
                {
                    ItemStack dstack = recipestacks[0];
                    recipestacks.RemoveAt(0);
                    if (dstack == null) continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, recipestacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    components.Add(comp);
                }

                while (cookingrecipes.Count > 0)
                {
                    CookingRecipe recipe = cookingrecipes[0];
                    cookingrecipes.RemoveAt(0);
                    if (recipe == null) continue;
                    MealstackTextComponent comp = new(capi, new(BlockMeal.RandomMealBowl(capi)), recipe, 40, EnumFloat.Inline, (cs) => openDetailPageFor("handbook-mealrecipe-" + recipe.Code), 4, false, maxstack);
                    components.Add(comp);
                }

                while (pierecipes.Count > 0)
                {
                    CookingRecipe recipe = pierecipes[0];
                    pierecipes.RemoveAt(0);
                    if (recipe == null) continue;

                    ItemStack mealBlock = new(capi.World.BlockAccessor.GetBlock("pie-perfect"));
                    mealBlock.Attributes.SetInt("pieSize", 4);
                    mealBlock.Attributes.SetString("topCrustType", BlockPie.TopCrustTypes[capi.World.Rand.Next(BlockPie.TopCrustTypes.Length)].Code);
                    mealBlock.Attributes.SetInt("bakeLevel", 2);
                    MealstackTextComponent comp = new(capi, mealBlock, recipe, 40, EnumFloat.Inline, (cs) => openDetailPageFor("handbook-mealrecipe-" + recipe.Code + "-pie"), 6, true, maxstack);
                    components.Add(comp);
                }

                components.Add(new ClearFloatTextComponent(capi, MarginBottom));
            }

            return haveText;
        }

        protected bool addCreatedByInfo(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, List<ItemStack> containers, List<ItemStack> fuels, List<ItemStack> molds, List<ItemStack> anvils, bool haveText)
        {
            bool anvilweldable = false;

            List<KnappingRecipe> knaprecipes = new List<KnappingRecipe>();
            foreach (var val in capi.GetKnappingRecipes())
            {
                if (val.Output.ResolvedItemStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    knaprecipes.Add(val);
                }
            }


            List<ClayFormingRecipe> clayrecipes = new List<ClayFormingRecipe>();
            foreach (var val in capi.GetClayformingRecipes())
            {
                if (val.Output.ResolvedItemStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    clayrecipes.Add(val);
                }
            }
            clayrecipes = clayrecipes.OrderBy(recipe => recipe.Output.ResolvedItemStack?.StackSize).ToList();


            if (stack.Collectible is BlockAnvil && capi.World.GetBlock(new AssetLocation("anvilpart-base-" + stack.Collectible.Variant["metal"])) != null)
            {
                anvilweldable = true;
            }


            List<GridRecipe> grecipes = new List<GridRecipe>();
            foreach (var recipe in capi.World.GridRecipes)
            {
                if (!recipe.ShowInCreatedBy) continue;


                if (recipe.RecipeOutput.ResolvedItemStack?.Satisfies(stack) == true)
                {
                    grecipes.Add(recipe);
                    continue;
                }

                CraftingRecipeIngredient[] gRIngred = [.. recipe.ResolvedIngredients];
                foreach (var ingred in gRIngred)
                {
                    if (ingred?.ReturnedStack?.ResolvedItemStack is not ItemStack rstack) continue;

                    if (rstack.Satisfies(stack) && ingred.ResolvedItemStack?.Satisfies(stack) == false)
                    {
                        grecipes.Add(recipe);
                        break;
                    }
                }
            }


            List<CookingRecipe> cookrecipes = new List<CookingRecipe>();
            foreach (var val in capi.GetCookingRecipes())
            {
                if (val.CooksInto?.ResolvedItemStack?.Satisfies(stack) ?? false)
                {
                    cookrecipes.Add(val);
                }
            }

            List<SmithingRecipe> smithingrecipes = new List<SmithingRecipe>();
            foreach (var val in capi.GetSmithingRecipes())
            {
                if (val.Output.ResolvedItemStack?.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) ?? false)
                {
                    if (!getIsAnvilWorkable(val.Ingredient.ResolvedItemStack, fuels)) continue;

                    smithingrecipes.Add(val);
                }
            }
            smithingrecipes = smithingrecipes.OrderBy(recipe => recipe.Output.ResolvedItemStack?.StackSize).ToList();

            List<ItemStack> moldStacks = new List<ItemStack>();
            foreach (var mold in molds)
            {
                string metaltype = stack.Collectible.Variant["metal"];

                if (mold.Collectible is BlockIngotMold && stack.Collectible is ItemIngot)
                {
                    moldStacks.Add(mold);
                    continue;
                }

                if (mold.ItemAttributes?["drop"]?.AsObject<JsonItemStack>(null, mold.Collectible.Code.Domain) is JsonItemStack dropStack)
                {
                    metaltype = stack.Collectible.LastCodePart();
                    dropStack.Code.Path = dropStack.Code.Path.Replace("{metal}", metaltype);

                    dropStack.Resolve(capi.World, "handbookmolds", false);
                    if (dropStack.ResolvedItemStack?.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) ?? false)
                    {
                        moldStacks.Add(mold);
                    }
                }
                else if (mold.ItemAttributes?["drops"].AsObject<JsonItemStack[]>(null, mold.Collectible.Code.Domain) is JsonItemStack[] dropStacks)
                {
                    foreach (var dstack in dropStacks)
                    {
                        metaltype = stack.Collectible.LastCodePart();
                        dstack.Code.Path = dstack.Code.Path.Replace("{metal}", metaltype);

                        dstack.Resolve(capi.World, "handbookmolds", false);
                        if (dstack.ResolvedItemStack?.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) ?? false)
                        {
                            moldStacks.Add(mold);
                        }
                    }
                }
            }


            List<ItemStack> knappables = [];
            List<ItemStack> clayformables = [];
            List<ItemStack> metalworkables = [];
            List<ItemStack> metalmoldables = [];
            List<ItemStack> bakables = [];
            List<ItemStack> bloomeryables = [];
            List<ItemStack> kilnables = [];
            List<ItemStack> carburizables = [];
            List<ItemStack> grindables = [];
            List<ItemStack> crushables = [];
            List<ItemStack> juiceables = [];
            List<ItemStack> squeezables = [];
            Dictionary<string, List<ItemStack>> groundstoredprocessables = [];
            List<ItemStack> distillables = [];
            List<ItemStack> smashables = [];
            List<ItemStack> fluxes = [];
            Dictionary<EnumTransitionType, List<ItemStack>> transitionables = [];
            List<ItemStack> validanvils = anvils;
            List<ItemStack> fruitpresses = [];

            foreach (var val in allStacks)
            {
                if (val.Collectible is BlockFruitPress && !fruitpresses.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                {
                    fruitpresses.Add(val);
                }

                CombustibleProperties combustibleProps = val.Collectible.GetCombustibleProperties(Api.World, val, null);
                ItemStack smeltedStack = combustibleProps?.SmeltedStack?.ResolvedItemStack;
                if (smeltedStack != null && smeltedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !val.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    if (getCanBloomerySmelt(val) && !bloomeryables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                    {
                        var bloomeryStack = val.Clone();
                        bloomeryStack.StackSize = combustibleProps.SmeltedRatio;
                        bloomeryables.Add(bloomeryStack);
                    }

                    if (combustibleProps.SmeltingType == EnumSmeltType.Fire && !kilnables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                    {
                        kilnables.Add(val);
                    }
                    else if (getCanSmelt(fuels, val) && !bakables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                    {
                        var bakeStack = val.Clone();
                        bakeStack.StackSize = combustibleProps.SmeltedRatio;
                        bakables.Add(bakeStack);
                    }
                }

                if (moldStacks.Count > 0 && smeltedStack?.Collectible is ItemIngot && smeltedStack?.Collectible.Variant["metal"] == stack.Collectible.LastCodePart() && getCanContainerSmelt(capi, containers, fuels, val))
                {
                    metalmoldables.Add(val);
                }

                if (smithingrecipes.Count > 0 && !val.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    var workable = val.Collectible.GetCollectibleInterface<IAnvilWorkable>();
                    if (workable?.GetMatchingRecipes(val).Intersect(smithingrecipes).Any() == true)
                    {
                        metalworkables.Add(val);
                        validanvils = [.. validanvils.Where(anvil => workable.GetRequiredAnvilTier(val) <= (anvil.Block as BlockAnvil).MetalTier)];
                    }
                }

                if (knaprecipes.Count > 0 && !val.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    if (knaprecipes.Where(recipe => recipe.Ingredient.SatisfiesAsIngredient(val, false)).FirstOrDefault() != null)
                    {
                        knappables.Add(val);
                    }
                }

                if (clayrecipes.Count > 0 && !val.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    var recipe = clayrecipes.Where(recipe => recipe.Ingredient.SatisfiesAsIngredient(val, false)).FirstOrDefault();
                    if (recipe != null)
                    {
                        var clayStack = val.Clone();
                        clayStack.StackSize = (int)Math.Ceiling(GameMath.Max(1, (recipe.Voxels.Cast<bool>().Count(voxel => voxel) - 64) / 25f));
                        clayformables.Add(clayStack);
                    }
                }

                var beehiveKilnProps = val.ItemAttributes?["beehivekiln"].AsObject<Dictionary<string, JsonItemStack>>();
                if (beehiveKilnProps != null)
                {
                    foreach (var beehiveStack in beehiveKilnProps.Values)
                    {
                        if (beehiveStack?.Resolve(capi.World, "beehivekiln-burn") == true && beehiveStack.ResolvedItemStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !kilnables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                        {
                            kilnables.Add(val);
                            break;
                        }
                    }
                }

                JsonItemStack carburizedJStack = val.ItemAttributes?["carburizableProps"]?["carburizedOutput"]?.AsObject<JsonItemStack>(null, val.Collectible.Code.Domain);
                if (carburizedJStack?.Resolve(Api.World, "carburizable handbook") == true)
                {
                    ItemStack carburizedStack = carburizedJStack.ResolvedItemStack;
                    if (carburizedStack != null && carburizedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !carburizables.Any(s => s.Equals(capi.World, carburizedStack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        carburizables.Add(val);
                    }
                }

                var grindingProps = val.Collectible.GetGrindingProperties(Api.World, val);

                ItemStack groundStack = grindingProps?.GroundStack.ResolvedItemStack;
                if (groundStack != null && groundStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !grindables.Any(s => s.Equals(capi.World, groundStack, GlobalConstants.IgnoredStackAttributes)))
                {
                    grindables.Add(val);
                }

                var crushingProps = val.Collectible.GetCrushingProperties(Api.World, val);

                ItemStack crushedStack = crushingProps?.CrushedStack.ResolvedItemStack;
                if (crushedStack != null && crushedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !crushables.Any(s => s.Equals(capi.World, crushedStack, GlobalConstants.IgnoredStackAttributes)))
                {
                    crushables.Add(val);
                }

                if (val.Collectible is ItemOre)
                {
                    string type = val.Collectible.Variant["ore"].Replace("quartz_", "").Replace("galena_", "");

                    if (capi.World.GetItem(new AssetLocation("nugget-" + type)) is { } item)
                    {
                        ItemStack outStack = new(item);
                        if (outStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !smashables.Any(s => s.Equals(capi.World, outStack, GlobalConstants.IgnoredStackAttributes)))
                        {
                            smashables.Add(val);
                        }
                    }
                }

                if (val.ItemAttributes?["juiceableProperties"].Exists == true && getjuiceableProps(val) is JuiceableProperties fjprops)
                {
                    var juicedStack = fjprops.LiquidStack?.ResolvedItemStack;
                    var pressedStack = fjprops.PressedStack?.ResolvedItemStack;
                    var returnStack = fjprops.ReturnStack?.ResolvedItemStack;
                    float ratio = 5 / (fjprops.LitresPerItem ?? 5);
                    var juiceableStack = val.Clone();
                    if (val.Attributes?.GetDouble("juiceableLitresLeft") > 0)
                    {
                        juiceableStack.Attributes.SetDouble("juiceableLitresLeft", 5);
                    }
                    else juiceableStack.StackSize = (int)Math.Ceiling(ratio);
                    if (juicedStack != null && juicedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !juiceables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                    {
                        if (fjprops.LitresPerItem != null || val.Attributes?.GetDouble("juiceableLitresLeft") > 0) juiceables.Add(juiceableStack);
                    }
                    if (pressedStack != null && pressedStack.Equals(capi.World, stack, [.. GlobalConstants.IgnoredStackAttributes, "juiceableLitresLeft"]) && !juiceables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                    {
                        if (!val.Equals(capi.World, pressedStack, [.. GlobalConstants.IgnoredStackAttributes, "juiceableLitresLeft"])) juiceables.Add(juiceableStack);
                    }
                    if (returnStack != null && returnStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !juiceables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                    {
                        juiceables.Add(juiceableStack);
                    }
                }

                if (val.Collectible.GetCollectibleBehavior<CollectibleBehaviorSqueezable>(true) is CollectibleBehaviorSqueezable cbs && cbs.ReturnStacks != null)
                {
                    foreach (var returnStack in cbs.ReturnStacks)
                    {
                        if (returnStack?.ResolvedItemStack == null) continue;
                        if (stack.Equals(capi.World, returnStack.ResolvedItemStack, GlobalConstants.IgnoredStackAttributes) && !squeezables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                        {
                            squeezables.Add(val);
                        }
                    }
                    if (cbs.SqueezedLiquid != null)
                    {
                        var liquidStack = new ItemStack(cbs.SqueezedLiquid);
                        if (stack.Equals(capi.World, liquidStack, GlobalConstants.IgnoredStackAttributes) && !squeezables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                        {
                            squeezables.Add(val);
                        }
                    }
                }

                if (val.Collectible.GetCollectibleBehavior<CollectibleBehaviorGroundStoredProcessable>(true) is { } gsp)
                {
                    if (gsp.ProcessedStacks != null)
                    {
                        foreach (var processedStack in gsp.ProcessedStacks)
                        {
                            if (processedStack?.ResolvedItemstack is not { } pStack ||
                                !stack.Equals(capi.World, pStack, GlobalConstants.IgnoredStackAttributes))
                            {
                                continue;
                            }

                            if (groundstoredprocessables.TryGetValue(gsp.HandbookCreatedByTitle, out var gspList))
                            {
                                if (!gspList.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                                {
                                    gspList.Add(val);
                                }
                            }
                            else groundstoredprocessables[gsp.HandbookCreatedByTitle] = [val];
                        }
                    }

                    if (gsp.RemainingItem?.ResolvedItemStack is { } rStack)
                    {
                        if (stack.Equals(capi.World, rStack, GlobalConstants.IgnoredStackAttributes))
                        {
                            if (groundstoredprocessables.TryGetValue(gsp.HandbookCreatedByTitle, out var gspList))
                            {
                                if (!gspList.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                                {
                                    gspList.Add(val);
                                }
                            }
                            else groundstoredprocessables[gsp.HandbookCreatedByTitle] = [val];
                        }
                    }
                }

                if (val.ItemAttributes?["distillationProps"].Exists == true)
                {
                    var dsprops = getDistillationProps(val);
                    var distilledStack = dsprops?.DistilledStack?.ResolvedItemStack;
                    if (distilledStack != null && distilledStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !distillables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                    {
                        distillables.Add(val);
                    }
                }

                if ((val?.ItemAttributes?.IsTrue("isFlux") == true || val?.Collectible?.GetTags(stack).IsSupersetOf(fluxTag) == true) && !fluxes.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes)))
                {
                    fluxes.Add(val);
                }

                TransitionableProperties[] oprops = val.Collectible.GetTransitionableProperties(capi.World, val, null);
                if (oprops == null) continue;

                foreach (var prop in oprops)
                {
                    if (prop.Type is EnumTransitionType.None) continue;

                    ItemStack transitionedStack = prop.TransitionedStack?.ResolvedItemStack;
                    if (transitionedStack?.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) != true) continue;

                    if (transitionables.TryGetValue(prop.Type, out var transitionable))
                    {
                        if (!transitionable.Any(s => s.Equals(capi.World, transitionedStack, GlobalConstants.IgnoredStackAttributes)))
                        {
                            transitionable.Add(val);
                        }
                    }
                    else transitionables[prop.Type] = [val];
                }
            }


            List<RichTextComponentBase> barrelRecipestext = BuildBarrelRecipesText(capi, stack, openDetailPageFor);


            string customCreatedBy = stack.Collectible.Attributes?["handbook"]?["createdBy"]?.AsString(null);
            string bakingInitialIngredient = collObj.Attributes?["bakingProperties"]?.AsObject<BakingProperties>()?.InitialCode;

            if (grecipes.Count > 0 || cookrecipes.Count > 0 || (metalmoldables.Count > 0 && moldStacks.Count > 0) || (metalworkables.Count > 0 && validanvils.Count > 0) || knappables.Count > 0 || clayformables.Count > 0 || anvilweldable || customCreatedBy != null || bakables.Count > 0 || bloomeryables.Count > 0 || kilnables.Count > 0 || carburizables.Count > 0 || barrelRecipestext.Count > 0 || grindables.Count > 0 || transitionables.Count > 0 || crushables.Count > 0 || bakingInitialIngredient != null || (juiceables.Count > 0 && fruitpresses.Count > 0) || squeezables.Count > 0 || groundstoredprocessables.Count > 0 || distillables.Count > 0 || smashables.Count > 0)
            {
                AddHeading(components, capi, "Created by", ref haveText);

                var verticalSpaceSmall = new ClearFloatTextComponent(capi, SmallPadding);
                var verticalSpace = new ClearFloatTextComponent(capi, TinyPadding + 1);   // The first bullet point has a smaller space than any later ones

                if (customCreatedBy != null)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    var comp = new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText());
                    comp.PaddingLeft = TinyIndent;
                    components.Add(comp);
                    components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get(customCreatedBy) + "\n", CairoFont.WhiteSmallText()));
                }

                if (metalmoldables.Count > 0 && moldStacks.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "handbook-createdby-metalmolding", "craftinginfo-smelting");

                    int firstPadding = TinyPadding;
                    while (moldStacks.Count > 0)
                    {
                        ItemStack dstack = moldStacks[0];
                        moldStacks.RemoveAt(0);
                        if (dstack == null) continue;
                        ItemStack[] sizedMoldables = metalmoldables.Select(moldable =>
                        {
                            var sizedmoldable = moldable.Clone();
                            sizedmoldable.StackSize = (dstack.ItemAttributes?["requiredUnits"].AsInt(100) ?? 100) / 5;
                            return sizedmoldable;
                        }).ToArray();
                        ItemStack[] output = getMoldedOutput(dstack, sizedMoldables);

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, sizedMoldables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.ShowStackSize = true;
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                        RichTextComponent cmp = new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText());
                        cmp.VerticalAlign = EnumVerticalAlign.Middle;
                        components.Add(cmp);
                        SlideshowItemstackTextComponent mcomp = new SlideshowItemstackTextComponent(capi, dstack, moldStacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        mcomp.PaddingLeft = firstPadding;
                        components.Add(mcomp);
                        cmp = new RichTextComponent(capi, " = ", CairoFont.WhiteMediumText());
                        cmp.VerticalAlign = EnumVerticalAlign.Middle;
                        components.Add(cmp);
                        SlideshowItemstackTextComponent ocomp = new SlideshowItemstackTextComponent(capi, output, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        ocomp.ShowStackSize = true;
                        ocomp.PaddingLeft = firstPadding;
                        components.Add(ocomp);
                        components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                    }
                }
                if (metalworkables.Count > 0 && validanvils.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Smithing", "craftinginfo-smithing");

                    while (smithingrecipes.Count > 0)
                    {
                        SmithingRecipe recipe = smithingrecipes[0];
                        smithingrecipes.RemoveAt(0);
                        if (recipe == null) continue;
                        List<ItemStack> workableanvils = validanvils;
                        ItemStack[] sizedWorkables = metalworkables.Select(val =>
                        {
                            var workable = val.Collectible.GetCollectibleInterface<IAnvilWorkable>();
                            var smithStack = val.Clone();
                            smithStack.StackSize = (int)Math.Ceiling(recipe.Voxels.Cast<bool>().Count(voxel => voxel) / (double)workable.VoxelCountForHandbook(val));
                            workableanvils = [.. workableanvils.Where(anvil => workable.GetRequiredAnvilTier(val) <= (anvil.Block as BlockAnvil).MetalTier)];
                            return smithStack;
                        }).ToArray();

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, sizedWorkables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.ShowStackSize = true;
                        comp.PaddingLeft = TinyIndent;
                        components.Add(comp);
                        RichTextComponent cmp = new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText());
                        cmp.PaddingLeft = 10;
                        cmp.VerticalAlign = EnumVerticalAlign.Middle;
                        components.Add(cmp);
                        SlideshowItemstackTextComponent acomp = new SlideshowItemstackTextComponent(capi, [.. workableanvils], 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        acomp.PaddingLeft = 0;
                        components.Add(acomp);
                        cmp = new RichTextComponent(capi, " = ", CairoFont.WhiteMediumText());
                        cmp.PaddingLeft = 10;
                        cmp.VerticalAlign = EnumVerticalAlign.Middle;
                        components.Add(cmp);
                        ItemstackTextComponent ocomp = new ItemstackTextComponent(capi, recipe.Output.ResolvedItemStack, 40, 0, EnumFloat.Inline);
                        ocomp.ShowStacksize = true;
                        ocomp.PaddingLeft = 0;
                        components.Add(ocomp);
                        components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                    }
                }
                if (knappables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Knapping", "craftinginfo-knapping");

                    int firstPadding = TinyPadding;
                    while (knappables.Count > 0)
                    {
                        ItemStack dstack = knappables[0];
                        knappables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, knappables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.ShowStackSize = true;
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }
                if (clayformables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Clay forming", "craftinginfo-clayforming");

                    int firstPadding = TinyPadding;
                    while (clayformables.Count > 0)
                    {
                        ItemStack dstack = clayformables[0];
                        clayformables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, clayformables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.ShowStackSize = true;
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }
                if (anvilweldable)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "handbook-createdby-anvilwelding", "gamemechanicinfo-steelmaking");

                    var baseComp = new ItemstackTextComponent(capi, new ItemStack(capi.World.GetBlock(new AssetLocation("anvilpart-base-" + stack.Collectible.Variant["metal"]))), 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    baseComp.PaddingLeft = TinyIndent;
                    components.Add(baseComp);

                    RichTextComponent cmp = new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText());
                    cmp.PaddingLeft = 10;
                    cmp.VerticalAlign = EnumVerticalAlign.Middle;
                    components.Add(cmp);

                    var fluxComp = new SlideshowItemstackTextComponent(capi, fluxes.ToArray(), 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    fluxComp.PaddingLeft = 10;
                    components.Add(fluxComp);

                    cmp = new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText());
                    cmp.PaddingLeft = 10;
                    cmp.VerticalAlign = EnumVerticalAlign.Middle;
                    components.Add(cmp);

                    var topComp = new ItemstackTextComponent(capi, new ItemStack(capi.World.GetBlock(new AssetLocation("anvilpart-top-" + stack.Collectible.Variant["metal"]))), 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    topComp.PaddingLeft = 0;
                    components.Add(topComp);

                    cmp = new RichTextComponent(capi, " = ", CairoFont.WhiteMediumText());
                    cmp.PaddingLeft = 10;
                    cmp.VerticalAlign = EnumVerticalAlign.Middle;
                    components.Add(cmp);

                    var ocomp = new ItemstackTextComponent(capi, stack, 40);
                    ocomp.PaddingLeft = 0;
                    components.Add(ocomp);

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (carburizables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "handbook-createdby-carburizing", "gamemechanicinfo-steelmaking");

                    int firstPadding = TinyPadding;
                    while (carburizables.Count > 0)
                    {
                        ItemStack dstack = carburizables[0];
                        carburizables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, carburizables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.ShowStackSize = true;
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
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

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, grindables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
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

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, crushables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.ShowStackSize = true;
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }


                if (smashables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "handbook-createdby-smashing", null);

                    int firstPadding = TinyPadding;
                    while (smashables.Count > 0)
                    {
                        ItemStack dstack = smashables[0];
                        smashables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, smashables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.ShowStackSize = true;
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }


                foreach (var transitionable in transitionables)
                {
                    if (transitionable.Value.Count <= 0) continue;

                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "handbook-createdby-transition-" + transitionable.Key.ToString().ToLowerInvariant(), null);

                    int firstPadding = TinyPadding;
                    while (transitionable.Value.Count > 0)
                    {
                        ItemStack dstack = transitionable.Value[0];
                        transitionable.Value.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, transitionable.Value, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }
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

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, bakables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.ShowStackSize = true;
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (bloomeryables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "handbook-createdby-bloomerysmelting", null);

                    int firstPadding = TinyPadding;
                    while (bloomeryables.Count > 0)
                    {
                        ItemStack dstack = bloomeryables[0];
                        bloomeryables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, bloomeryables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.ShowStackSize = true;
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (kilnables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "handbook-createdby-kilnfiring", null);

                    int firstPadding = TinyPadding;
                    while (kilnables.Count > 0)
                    {
                        ItemStack dstack = kilnables[0];
                        kilnables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, bakables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (fruitpresses.Count > 0 && juiceables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Juicing", "block-fruitpress-ns");

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, [.. juiceables], 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    comp.ShowStackSize = true;
                    comp.PaddingLeft = TinyIndent;
                    components.Add(comp);
                    RichTextComponent cmp = new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText());
                    cmp.PaddingLeft = 10;
                    cmp.VerticalAlign = EnumVerticalAlign.Middle;
                    components.Add(cmp);
                    SlideshowItemstackTextComponent acomp = new SlideshowItemstackTextComponent(capi, [.. fruitpresses], 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    acomp.PaddingLeft = 0;
                    components.Add(acomp);
                    cmp = new RichTextComponent(capi, " = ", CairoFont.WhiteMediumText());
                    cmp.PaddingLeft = 10;
                    cmp.VerticalAlign = EnumVerticalAlign.Middle;
                    components.Add(cmp);

                    if (stack.Attributes?.GetDouble("juiceableLitresLeft") > 0)
                    {
                        var jprops = getjuiceableProps(juiceables[0]);
                        var pstack = jprops.PressedStack.ResolvedItemStack.Clone();
                        pstack.Attributes.SetDouble("juiceableLitresLeft", 5);
                        var ocomp = new ItemstackTextComponent(capi, pstack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        ocomp.ShowStacksize = true;
                        ocomp.PaddingLeft = 0;
                        components.Add(ocomp);
                    }
                    else
                    {
                        var jprops = getjuiceableProps(juiceables[0]);
                        var jstack = jprops.LiquidStack.ResolvedItemStack.Clone();
                        jstack.StackSize = (int)(BlockLiquidContainerBase.GetContainableProps(jstack).ItemsPerLitre * 5);
                        var ocomp = new ItemstackTextComponent(capi, jstack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        ocomp.ShowStacksize = jprops.LitresPerItem != null;
                        ocomp.PaddingLeft = TinyIndent;
                        components.Add(ocomp);

                        if (jprops.ReturnStack?.ResolvedItemStack == null)
                        {
                            var pstack = jprops.PressedStack.ResolvedItemStack.Clone();
                            pstack.StackSize = (int)(5 * jprops.PressedDryRatio);
                            ocomp = new ItemstackTextComponent(capi, pstack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                            ocomp.ShowStacksize = true;
                            ocomp.PaddingLeft = 0;
                            components.Add(ocomp);
                        }
                        else
                        {
                            var rstack = jprops.ReturnStack.ResolvedItemStack.Clone();
                            rstack.StackSize *= 5;
                            ocomp = new ItemstackTextComponent(capi, rstack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                            ocomp.ShowStacksize = true;
                            ocomp.PaddingLeft = 0;
                            components.Add(ocomp);
                        }
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                if (squeezables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "handbook-createdby-squeezing", null);

                    int firstPadding = TinyPadding;
                    while (squeezables.Count > 0)
                    {
                        ItemStack dstack = squeezables[0];
                        squeezables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, squeezables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                foreach ((string processTitle, var processables) in groundstoredprocessables)
                {
                    if (processables.Count <= 0) continue;

                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, processTitle, null);

                    int firstPadding = TinyPadding;
                    while (processables.Count > 0)
                    {
                        ItemStack dstack = processables[0];
                        processables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, processables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        comp.PaddingLeft = firstPadding;
                        firstPadding = 0;
                        components.Add(comp);
                    }
                }

                if (distillables.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Distillation", "craftinginfo-alcohol");

                    int firstPadding = TinyPadding;
                    while (distillables.Count > 0)
                    {
                        ItemStack dstack = distillables[0];
                        distillables.RemoveAt(0);
                        if (dstack == null) continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, distillables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
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

                    var comp = new ItemstackTextComponent(capi, new ItemStack(cobj), 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    comp.PaddingLeft = TinyIndent;
                    components.Add(comp);
                }

                if (cookrecipes.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "handbook-createdby-potcooking", null);

                    foreach (var rec in cookrecipes)
                    {
                        int firstIndent = TinyIndent;
                        for (int i = 0; i < rec.Ingredients.Length; i++)
                        {
                            var ingred = rec.Ingredients[i];
                            if (i > 0)
                            {
                                RichTextComponent cmp = new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText());
                                cmp.PaddingLeft = 10;
                                cmp.VerticalAlign = EnumVerticalAlign.Middle;
                                components.Add(cmp);
                            }

                            var stacks = ingred.ValidStacks.SelectMany(vs =>
                            {
                                var rs = vs.ResolvedItemStack?.Clone();
                                IEnumerable<ItemStack> found = rs != null ? [rs] : null;
                                if (vs.Type == EnumItemClass.Block)
                                {
                                    found ??= capi.World.SearchBlocks(vs.Code).Select(block => new ItemStack(block));
                                }
                                else
                                {
                                    found ??= capi.World.SearchItems(vs.Code).Select(item => new ItemStack(item));
                                }

                                foreach (var ls in found.Where(s => s.Collectible.Attributes?["waterTightContainerProps"].Exists == true))
                                {
                                    var props = BlockLiquidContainerBase.GetContainableProps(ls);
                                    rs.StackSize *= (int)((props?.ItemsPerLitre ?? 1) * ingred.PortionSizeLitres);
                                }



                                return found;
                            }).ToArray();

                            for (int j = 0; j < ingred.MinQuantity; j++)
                            {
                                if (j > 0)
                                {
                                    RichTextComponent cmp = new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText());
                                    cmp.PaddingLeft = 10;
                                    cmp.VerticalAlign = EnumVerticalAlign.Middle;
                                    components.Add(cmp);
                                }
                                var comp = new SlideshowItemstackTextComponent(capi, stacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                                comp.ShowStackSize = true;
                                comp.PaddingLeft = firstIndent;
                                components.Add(comp);
                                firstIndent = 0;
                            }
                        }

                        var eqcomp = new RichTextComponent(capi, " = ", CairoFont.WhiteMediumText());
                        eqcomp.PaddingLeft = 10;
                        eqcomp.VerticalAlign = EnumVerticalAlign.Middle;
                        components.Add(eqcomp);
                        var ocmp = new ItemstackTextComponent(capi, rec.CooksInto.ResolvedItemStack, 40, 0, EnumFloat.Inline);
                        ocmp.ShowStacksize = true;
                        components.Add(ocmp);
                        components.Add(new ClearFloatTextComponent(capi, -6));
                    }
                }

                if (grecipes.Count > 0)
                {
                    components.Add(verticalSpace);
                    verticalSpace = verticalSpaceSmall;
                    AddSubHeading(components, capi, openDetailPageFor, "Crafting", null);

                    API.Datastructures.OrderedDictionary<int, List<GridRecipe>> grouped = [];

                    ItemStack[] outputStacks = new ItemStack[grecipes.Count];
                    int i = 0;

                    foreach (var recipe in grecipes)
                    {
                        if (!grouped.TryGetValue(recipe.RecipeGroup, out List<GridRecipe> list))
                        {
                            grouped[recipe.RecipeGroup] = list = [];
                        }

                        if (recipe.ResolvedIngredients.Select(ingred => ingred?.ReturnedStack?.ResolvedItemStack).Where(stack => stack != null).ToArray() is ItemStack[] returnedStacks)
                        {
                            if (returnedStacks.Length > 0 && returnedStacks.Any(rstack => rstack.Satisfies(stack)))
                            {
                                list.Add(recipe);
                                outputStacks[i++] = returnedStacks.FirstOrDefault(rstack => rstack.Satisfies(stack));
                                continue;
                            }
                        }

                        if (recipe.CopyAttributesFrom != null && recipe.Ingredients.ContainsKey(recipe.CopyAttributesFrom))
                        {
                            var rec = recipe.Clone();

                            var ingred = rec.Ingredients[recipe.CopyAttributesFrom];
                            var cattr = stack.Attributes.Clone();
                            cattr.MergeTree(ingred.ResolvedItemStack.Attributes);
                            ingred.Attributes = new JsonObject(JToken.Parse(cattr.ToJsonToken()));
                            rec.Resolve(capi.World, "");

                            rec.Output.ResolvedItemStack.Attributes.MergeTree(stack.Attributes);

                            list.Add(rec);
                            outputStacks[i++] = rec.Output.ResolvedItemStack;
                            continue;
                        }

                        if (recipe.MergeAttributesFrom.Length > 0)
                        {
                            var rec = recipe.Clone();
                            var cattr = stack.Attributes.Clone();

                            foreach (var ingredientName in recipe.MergeAttributesFrom)
                            {
                                if (!rec.Ingredients.ContainsKey(ingredientName)) continue;

                                var ingred = rec.Ingredients[ingredientName];

                                cattr.MergeTree(ingred.ResolvedItemStack.Attributes);
                            }

                            foreach (var ingredientName in recipe.MergeAttributesFrom)
                            {
                                if (!rec.Ingredients.ContainsKey(ingredientName)) continue;

                                var ingred = rec.Ingredients[ingredientName];

                                ingred.Attributes = new JsonObject(JToken.Parse(cattr.ToJsonToken()));
                            }

                            rec.Resolve(capi.World, "");
                            rec.Output.ResolvedItemStack.Attributes.MergeTree(stack.Attributes);

                            list.Add(rec);
                            outputStacks[i++] = rec.Output.ResolvedItemStack;
                            continue;
                        }

                        if (recipe.CopyAttributesFrom != null && recipe.Ingredients.ContainsKey(recipe.CopyAttributesFrom) && recipe.MergeAttributesFrom.Length > 0)
                        {
                            var rec = (GridRecipe)recipe.Clone();
                            var cattr = stack.Attributes.Clone();

                            var ingred2 = rec.Ingredients[recipe.CopyAttributesFrom];
                            cattr.MergeTree(ingred2.ResolvedItemStack.Attributes);

                            foreach (var ingredientName in recipe.MergeAttributesFrom)
                            {
                                if (!rec.Ingredients.ContainsKey(ingredientName)) continue;

                                var ingred = rec.Ingredients[ingredientName];

                                cattr.MergeTree(ingred.ResolvedItemStack.Attributes);
                            }

                            foreach (var ingredientName in recipe.MergeAttributesFrom)
                            {
                                if (!rec.Ingredients.ContainsKey(ingredientName)) continue;

                                var ingred = rec.Ingredients[ingredientName];

                                ingred.Attributes = new JsonObject(JToken.Parse(cattr.ToJsonToken()));
                            }

                            ingred2.Attributes = new JsonObject(JToken.Parse(cattr.ToJsonToken()));

                            rec.Resolve(capi.World, "");
                            rec.Output.ResolvedItemStack.Attributes.MergeTree(stack.Attributes);

                            list.Add(rec);
                            outputStacks[i++] = rec.Output.ResolvedItemStack;
                            continue;
                        }

                        list.Add(recipe);
                        outputStacks[i++] = recipe.RecipeOutput.ResolvedItemStack;
                    }

                    int j = 0;
                    foreach (var val in grouped)
                    {
                        if (j++ % 2 == 0) components.Add(verticalSpaceSmall);    // Reset the component model with a small vertical element every 2nd grid recipe, otherwise horizontal aligment gets messed up

                        var comp = new SlideshowGridRecipeTextComponent(capi, [.. val.Value], 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)), allStacks);
                        comp.VerticalAlign = EnumVerticalAlign.Top;
                        comp.PaddingRight = 8;
                        comp.PaddingLeft = TinyPadding * 2 + (1 - j % 2) * 20;   // Add horizontal padding for every 2nd grid (i.e. the right hand side one when drawn)

                        components.Add(comp);

                        var ecomp = new RichTextComponent(capi, "=", CairoFont.WhiteMediumText());
                        ecomp.VerticalAlign = EnumVerticalAlign.Middle;
                        ecomp.PaddingRight = 5;
                        var ocomp = new SlideshowItemstackTextComponent(capi, outputStacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        ocomp.overrideCurrentItemStack = comp.GenerateCurrentVisibleOutputStack;
                        ocomp.VerticalAlign = EnumVerticalAlign.Middle;
                        ocomp.ShowStackSize = true;
                        var rcomp = new SlideshowItemstackTextComponent(capi, [null], 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                        rcomp.overrideCurrentItemStack = () => comp.CurrentVisibleRecipe.Recipe.ResolvedIngredients.Select(ingred => ingred?.ReturnedStack?.ResolvedItemStack).Where(stack => stack != null).FirstOrDefault();
                        rcomp.PaddingLeft = -40;
                        rcomp.ShowStackSize = true;

                        components.Add(ecomp);
                        components.Add(ocomp);
                        components.Add(rcomp);
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

        public static void AddHeading(List<RichTextComponentBase> components, ICoreClientAPI capi, string heading, ref bool haveText)
        {
            if (haveText) components.Add(new ClearFloatTextComponent(capi, MediumPadding));
            haveText = true;
            var headc = new RichTextComponent(capi, Lang.Get(heading) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold));
            components.Add(headc);
        }

        public static void AddSubHeading(List<RichTextComponentBase> components, ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, string subheading, string detailpage)
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
                ItemStack mixdStack = recipe.RecipeOutput.ResolvedItemStack;

                if (mixdStack != null && mixdStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {

                    if (!brecipesbyCode.TryGetValue(recipe.Code, out List<BarrelRecipe> tmp))
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

                        ingstacks[j][i] = recipes[i].Ingredients[j].ResolvedItemStack;
                    }

                    sealHours = recipes[i].SealHours;
                    outstacks[i] = recipes[i].RecipeOutput.ResolvedItemStack;
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

                    SlideshowItemstackTextComponent scmp = new SlideshowItemstackTextComponent(capi, ingstacks[i], 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
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

                string sealHoursText = (sealHours > capi.World.Calendar.HoursPerDay) ? " " + Lang.Get("{0} days", Math.Round(sealHours / (double)capi.World.Calendar.HoursPerDay, 1)) : Lang.Get("{0} hours", Math.Round(sealHours));
                var hoursCmp = new RichTextComponent(capi, sealHoursText, CairoFont.WhiteSmallText());
                hoursCmp.VerticalAlign = EnumVerticalAlign.Middle;
                barrelRecipesTexts.Add(hoursCmp);

                barrelRecipesTexts.Add(new ClearFloatTextComponent(capi, 10));
            }

            return barrelRecipesTexts;
        }

        protected void addStorableInfo(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop)
        {
            List<ItemStack> foodStorables = new List<ItemStack>();
            List<ItemStack> liquidStorables = new List<ItemStack>();
            List<ItemStack> displayStorables = new List<ItemStack>();
            List<ItemStack> animalHusbandryStorables = new List<ItemStack>();
            bool groundStorable = stack.Collectible.HasBehavior<CollectibleBehaviorGroundStorable>();
            var dummySlot = new DummySlot(stack);

            foreach (var val in allStacks)
            {
                string[] placementSurfaces = [..val.Block?.GetBehavior<BlockBehaviorDisplay>()?.PlacementSurfaces.Select(ps => ps.DisplayCategory) ?? []];
                if ((placementSurfaces.Length > 0 && placementSurfaces.Any(key => BlockBehaviorDisplay.GetDisplayableAttributes(new DummySlot(stack), key) != null)) ||
                    (val.Collectible is BlockShelf && BlockEntityShelf.GetShelvableLayout(stack) != null) ||
                    (val.Collectible is BlockToolRack && (stack.Collectible.GetTool(new DummySlot(stack)) != null || stack.ItemAttributes?["rackable"].AsBool() == true)) ||
                    (val.Collectible is BlockPlantContainer && getPlantContainerProps(val, stack) != null))
                {
                    if (!displayStorables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes))) displayStorables.Add(val);
                }

                var blockTrap = val.Block as BlockAnimalTrap;
                if ((ItemSlotTrough.getContentConfig(capi.World, (val.Block as BlockTroughBase)?.contentConfigs, dummySlot) != null) ||
                    (blockTrap != null && !val.Attributes.GetAsBool("destroyed") && blockTrap.IsAppetizingBait(capi, stack) && blockTrap.CanFitBait(capi, stack)))
                {
                    if (!animalHusbandryStorables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes))) animalHusbandryStorables.Add(val);
                }

                if (stack.ItemAttributes is not JsonObject attr) continue;

                if ((val.Collectible is BlockMoldRack && attr["moldrackable"].AsBool()) ||
                    (val.Collectible is BlockBookshelf && attr["bookshelveable"].AsBool()) ||
                    (val.Collectible is BlockScrollRack && attr["scrollrackable"].AsBool()) ||
                    (attr["displaycaseable"].AsBool() && (val.Collectible as BlockDisplayCase)?.height >= attr["displaycase"]["minHeight"].AsFloat(0.25f)) ||
                    (val.Collectible is BlockAntlerMount && attr["antlerMountable"].AsBool()) ||
                    (val.Collectible is BlockOmokTable && attr["omokpiece"].AsBool()))
                {
                    if (!displayStorables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes))) displayStorables.Add(val);
                }

                if (val.Collectible.GetCollectibleInterface<ILiquidInterface>() is ILiquidInterface contBlock && contBlock.GetCurrentLitres(val) <= 0 && attr["waterTightContainerProps"].Exists)
                {
                    if (!liquidStorables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes))) liquidStorables.Add(val);
                }

                if (val.Collectible is BlockCrock && attr["crockable"].AsBool())
                {
                    if (!foodStorables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes))) foodStorables.Add(val);
                }
            }

            if (!(foodStorables.Count > 0 || displayStorables.Count > 0 || liquidStorables.Count > 0 || animalHusbandryStorables.Count > 0 || groundStorable)) return;

            var verticalSpaceSmall = new ClearFloatTextComponent(capi, SmallPadding);
            var verticalSpace = new ClearFloatTextComponent(capi, TinyPadding + 1);   // The first bullet point has a smaller space than any later ones

            bool haveText = components.Count > 0;
            components.Add(verticalSpace);
            verticalSpace = verticalSpaceSmall;

            AddHeading(components, capi, "Storable in/on", ref haveText);

            if (groundStorable)
            {
                components.Add(verticalSpace);
                verticalSpace = verticalSpaceSmall;
                var comp = new RichTextComponent(capi, "", CairoFont.WhiteSmallText());
                comp.PaddingLeft = TinyIndent;
                components.Add(comp);
                components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-storable-ground") + "\n", CairoFont.WhiteSmallText()));
            }

            if (displayStorables.Count > 0)
            {
                components.Add(verticalSpace);
                verticalSpace = verticalSpaceSmall;
                AddSubHeading(components, capi, openDetailPageFor, "handbook-storable-displaycontainers", null);

                int firstPadding = TinyPadding;

                while (displayStorables.Count > 0)
                {
                    ItemStack dstack = displayStorables[0];
                    displayStorables.RemoveAt(0);
                    if (dstack == null) continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, displayStorables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    comp.PaddingLeft = firstPadding;
                    firstPadding = 0;
                    components.Add(comp);
                }

                components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
            }

            if (liquidStorables.Count > 0)
            {
                components.Add(verticalSpace);
                verticalSpace = verticalSpaceSmall;
                AddSubHeading(components, capi, openDetailPageFor, "handbook-storable-liquidcontainers", null);

                int firstPadding = TinyPadding;

                while (liquidStorables.Count > 0)
                {
                    ItemStack dstack = liquidStorables[0];
                    liquidStorables.RemoveAt(0);
                    if (dstack == null) continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, liquidStorables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    comp.PaddingLeft = firstPadding;
                    firstPadding = 0;
                    components.Add(comp);
                }

                components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
            }

            if (foodStorables.Count > 0)
            {
                components.Add(verticalSpace);
                verticalSpace = verticalSpaceSmall;
                AddSubHeading(components, capi, openDetailPageFor, "handbook-storable-foodcontainers", null);

                int firstPadding = TinyPadding;

                while (foodStorables.Count > 0)
                {
                    ItemStack dstack = foodStorables[0];
                    foodStorables.RemoveAt(0);
                    if (dstack == null) continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, foodStorables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    comp.PaddingLeft = firstPadding;
                    firstPadding = 0;
                    components.Add(comp);
                }

                components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
            }

            if (animalHusbandryStorables.Count > 0)
            {
                components.Add(verticalSpace);
                verticalSpace = verticalSpaceSmall;
                AddSubHeading(components, capi, openDetailPageFor, "handbook-storable-animalhusbandry", null);

                int firstPadding = TinyPadding;

                while (animalHusbandryStorables.Count > 0)
                {
                    ItemStack dstack = animalHusbandryStorables[0];
                    animalHusbandryStorables.RemoveAt(0);
                    if (dstack == null) continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, animalHusbandryStorables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    comp.PaddingLeft = firstPadding;
                    firstPadding = 0;
                    components.Add(comp);
                }

                components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
            }
        }

        protected void addStoredInInfo(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop)
        {
            List<ItemStack> storables = new List<ItemStack>();
            var blockTrap = stack.Block as BlockAnimalTrap;
            var dummySlot = new DummySlot();

            string[] placementSurfaces = [..stack.Block?.GetBehavior<BlockBehaviorDisplay>()?.PlacementSurfaces.Select(ps => ps.DisplayCategory) ?? []];
            foreach (var val in allStacks)
            {
                if ((placementSurfaces.Length > 0 && placementSurfaces.Any(key => BlockBehaviorDisplay.GetDisplayableAttributes(new DummySlot(val), key) != null)) ||
                    (stack.Collectible is BlockShelf && BlockEntityShelf.GetShelvableLayout(val) != null) ||
                    (stack.Collectible is BlockPlantContainer && getPlantContainerProps(stack, val) != null) ||
                    (stack.Collectible is BlockToolRack && (val.Collectible.GetTool(new DummySlot(val)) != null || val.ItemAttributes?["rackable"].AsBool() == true)) ||
                    (blockTrap != null && !stack.Attributes.GetAsBool("destroyed") && blockTrap.IsAppetizingBait(capi, val) && blockTrap.CanFitBait(capi, val)))
                {
                    if (!storables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes))) storables.Add(val);
                }

                dummySlot.Itemstack = val;
                if (ItemSlotTrough.getContentConfig(capi.World, (stack.Block as BlockTroughBase)?.contentConfigs, dummySlot) is ContentConfig contentConfig)
                {
                    ItemStack troughStack = val.GetEmptyClone();
                    troughStack.StackSize = contentConfig.QuantityPerFillLevel;
                    if (!storables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes))) storables.Add(troughStack);
                }

                if (val.ItemAttributes is not JsonObject attr) continue;

                if ((stack.Collectible is BlockMoldRack && attr["moldrackable"].AsBool()) ||
                    (stack.Collectible is BlockBookshelf && attr["bookshelveable"].AsBool()) ||
                    (stack.Collectible is BlockScrollRack && attr["scrollrackable"].AsBool()) ||
                    (attr["displaycaseable"].AsBool() && (stack.Collectible as BlockDisplayCase)?.height >= attr["displaycase"]["minHeight"].AsFloat(0.25f)) ||
                    (stack.Collectible is BlockAntlerMount && attr["antlerMountable"].AsBool()) ||
                    (stack.Collectible.GetCollectibleInterface<ILiquidInterface>() != null && BlockLiquidContainerBase.GetContainableProps(val) is WaterTightContainableProps liquidProps && liquidProps.WhenFilled == null) ||
                    (stack.Collectible is BlockCrock && attr["crockable"].AsBool()) ||
                    (stack.Collectible is BlockOmokTable && attr["omokpiece"].AsBool()))
                {
                    if (!storables.Any(s => s.Equals(capi.World, val, GlobalConstants.IgnoredStackAttributes))) storables.Add(val);
                }
            }

            if (storables.Count > 0)
            {
                bool haveText = components.Count > 0;
                components.Add(new ClearFloatTextComponent(capi, SmallPadding));
                AddHeading(components, capi, "handbook-storedin", ref haveText);

                int firstPadding = TinyPadding;

                while (storables.Count > 0)
                {
                    ItemStack dstack = storables[0];
                    storables.RemoveAt(0);
                    if (dstack == null) continue;

                    if (dstack.Collectible is BlockPie)
                    {

                        List<CookingRecipe> pierecipes = [.. BlockPie.GetHandbookRecipes(capi, allStacks)];

                        while (pierecipes.Count > 0)
                        {
                            CookingRecipe recipe = pierecipes[0];
                            pierecipes.RemoveAt(0);
                            if (recipe == null) continue;

                            ItemStack mealBlock = dstack.Clone();
                            mealBlock.Attributes.SetInt("pieSize", 4);
                            mealBlock.Attributes.SetString("topCrustType", BlockPie.TopCrustTypes[capi.World.Rand.Next(BlockPie.TopCrustTypes.Length)].Code);
                            mealBlock.Attributes.SetInt("bakeLevel", 2);
                            MealstackTextComponent mealComp = new MealstackTextComponent(capi, mealBlock, recipe, 40, EnumFloat.Inline, (cs) => openDetailPageFor("handbook-mealrecipe-" + recipe.Code + "-pie"), 6, true);
                            components.Add(mealComp);
                        }
                        continue;
                    }

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, storables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(getPageCodeForStack(capi, cs)));
                    if (BlockLiquidContainerBase.GetContainableProps(dstack) is not WaterTightContainableProps) comp.ShowStackSize = true;
                    comp.PaddingLeft = firstPadding;
                    firstPadding = 0;
                    components.Add(comp);
                }

                components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
            }
        }

        protected void addEatenByInfo(ICoreClientAPI capi, ItemStack stack, List<RichTextComponentBase> components, float marginTop)
        {
            HashSet<string> creatureNames = new HashSet<string>();
            foreach (var entityType in Api.World.EntityTypes)
            {
                var attr = entityType.Attributes;
                if (attr?["creatureDiet"].AsObject<CreatureDiet>()?.Matches(stack) != true) continue;

                string code = attr?["creatureDietGroup"].AsString() ?? attr?["handbook"]["groupcode"].AsString() ?? entityType.Code.Domain + ":item-creature-" + entityType.Code.Path;
                creatureNames.Add(Lang.Get(code));
            }

            bool haveText = components.Count > 0;

            if (creatureNames.Count > 0)
            {
                AddHeading(components, capi, "handbook-eatenby", ref haveText);
                components.Add(new ClearFloatTextComponent(capi, TinyPadding));
                var comp = new RichTextComponent(capi, string.Join(", ", creatureNames) + "\n", CairoFont.WhiteSmallText());
                comp.PaddingLeft = TinyIndent;
                components.Add(comp);
            }
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
                }
                else
                {
                    litres = (float)jprops.LitresPerItem * inSlot.Itemstack.StackSize;
                }

                if (litres > 0.01)
                {
                    dsc.AppendLine(Lang.Get("collectibleinfo-juicingproperties", litres, jprops.LiquidStack.ResolvedItemStack.GetName()));
                }
            }

        }

        public JuiceableProperties getjuiceableProps(ItemStack stack)
        {
            var props = stack?.ItemAttributes?["juiceableProperties"]?.AsObject<JuiceableProperties>(null, stack.Collectible.Code.Domain);
            props?.LiquidStack?.Resolve(Api.World, "juiceable properties liquidstack");
            props?.PressedStack?.Resolve(Api.World, "juiceable properties pressedstack");
            props?.ReturnStack?.Resolve(Api.World, "juiceable properties returnstack");

            return props;
        }

        public DistillationProps getDistillationProps(ItemStack stack)
        {
            var props = stack?.ItemAttributes?["distillationProps"]?.AsObject<DistillationProps>(null, stack.Collectible.Code.Domain);
            props?.DistilledStack?.Resolve(Api.World, "distillation props distilled stack");

            return props;
        }

        ItemStack[] getMoldedOutput(ItemStack mold, ItemStack[] metalmoldables)
        {
            CombustibleProperties combustibleProps = metalmoldables[0].Collectible.GetCombustibleProperties(Api.World, metalmoldables[0], null);
            var ingotStack = combustibleProps?.SmeltedStack?.ResolvedItemStack;

            if (mold.Block is BlockIngotMold) return [ingotStack];
            if (mold.Block is not BlockToolMold) return [];

            try
            {
                if (mold.Block.Attributes["drop"].Exists)
                {
                    JsonItemStack jstack = mold.Block.Attributes["drop"].AsObject<JsonItemStack>(null, mold.Block.Code.Domain);
                    ItemStack stack = moldedStackFromCode(mold.Block, jstack, ingotStack);
                    if (stack == null) return [];
                    return [stack];
                }
                else
                {
                    JsonItemStack[] jstacks = mold.Block.Attributes["drops"].AsObject<JsonItemStack[]>(null, mold.Block.Code.Domain);
                    return [.. jstacks.Select(jstack => moldedStackFromCode(mold.Block, jstack, ingotStack)).Where(stack => stack != null)];
                }
            }
            catch (JsonReaderException)
            {
                Api.World.Logger.Error("Failed getting molded stacks from tool mold of block {0}, probably unable to parse drop or drops attribute", mold.Block.Code);
                throw;
            }
        }

        ItemStack moldedStackFromCode(Block mold, JsonItemStack jstack, ItemStack fromMetal)
        {
            if (mold == null || jstack == null || fromMetal == null) return null;

            string metaltype = fromMetal.Collectible.LastCodePart();
            jstack.Code.Path = jstack.Code.Path.Replace("{metal}", metaltype);
            jstack.Resolve(Api.World, "tool mold drop for " + mold.Code, false);
            return jstack.ResolvedItemStack;
        }

        bool getIsAnvilWorkable(ItemStack stack, List<ItemStack> fuels)
        {
            var combustibleProps = stack?.Collectible?.GetCombustibleProperties(Api.World, stack, null);
            if (stack?.Collectible?.GetCollectibleInterface<IAnvilWorkable>() is not IAnvilWorkable || (combustibleProps == null && (!stack.Collectible.Attributes?["workableTemperature"].Exists ?? true))) return false;

            float maxFuelTemp = fuels.OrderBy(fuel => fuel.Collectible.GetCombustibleProperties(Api.World, fuel, null).BurnTemperature).Select(fuel => fuel.Collectible.GetCombustibleProperties(Api.World, fuel, null).BurnTemperature).LastOrDefault();
            float meltingPoint = combustibleProps?.MeltingPoint / 2 ?? 0;
            float workableTemp = stack.Collectible.Attributes?["workableTemperature"]?.AsFloat(meltingPoint) ?? meltingPoint;

            return workableTemp <= maxFuelTemp;
        }

        bool getCanBloomerySmelt(ItemStack stack)
        {
            var combustibleProps = stack.Collectible.GetCombustibleProperties(Api.World, stack, null);
            if (combustibleProps?.SmeltedStack?.ResolvedItemStack == null) return false;

            int stackMeltingPoint = combustibleProps?.MeltingPoint ?? 0;
            return stackMeltingPoint < BlockEntityBloomery.MaxTemp && stackMeltingPoint >= BlockEntityBloomery.MinTemp;
        }

        bool getCanContainerSmelt(ICoreClientAPI capi, List<ItemStack> containers, List<ItemStack> fuels, ItemStack stack)
        {
            int maxFuelTemp = fuels.OrderBy(fuel => fuel.Collectible.GetCombustibleProperties(capi.World, fuel, null).BurnTemperature).Select(fuel => fuel.Collectible.GetCombustibleProperties(capi.World, fuel, null).BurnTemperature).LastOrDefault();

            var combustibleProps = stack?.Collectible?.GetCombustibleProperties(Api.World, stack, null);
            if (combustibleProps == null || combustibleProps.MeltingPoint > maxFuelTemp) return false;

            ItemStack[] filteredContainers = containers.Where(container => container.ItemAttributes["maxContentDimensions"].AsObject<Size3f>(null)?.CanContain(stack.Collectible.Dimensions) ?? true).ToArray();
            ItemStack smeltedStack = stack.Clone();
            smeltedStack.StackSize = combustibleProps.SmeltedRatio;
            dummySmeltingInv.Slots[1].Itemstack = smeltedStack;

            return filteredContainers.Any(container => container.Collectible.CanSmelt(capi.World, dummySmeltingInv, container, null));
        }

        bool getCanSmelt(List<ItemStack> fuels, ItemStack stack)
        {
            int maxFuelTemp = fuels.OrderBy(fuel => fuel.Collectible.GetCombustibleProperties(Api.World, fuel, null).BurnTemperature).Select(fuel => fuel.Collectible.GetCombustibleProperties(Api.World, fuel, null).BurnTemperature).LastOrDefault();
            CombustibleProperties combustibleProps = stack.Collectible.GetCombustibleProperties(Api.World, stack, null);
            if (combustibleProps == null || combustibleProps.MeltingPoint > maxFuelTemp) return false;
            if (combustibleProps.RequiresContainer == false) return true;

            return false;
        }

        PlantContainerProps getPlantContainerProps(ItemStack containerStack, ItemStack plantStack)
        {
            string containerSize = containerStack?.ItemAttributes?["plantContainerSize"].AsString();
            if (containerSize == null || plantStack?.Collectible is not CollectibleObject plant) return null;
            return plant.Attributes?["plantContainable"]?[containerSize + "Container"]?.AsObject<PlantContainerProps>(null, plant.Code.Domain);
        }

        string getPageCodeForStack(ICoreClientAPI capi, ItemStack stack)
        {
            return stack.Collectible.GetCollectibleInterface<IHandBookPageCodeProvider>()?.HandbookPageCodeForStack(capi.World, stack)
                ?? GuiHandbookItemStackPage.PageCodeForStack(stack);
        }
    }
}
