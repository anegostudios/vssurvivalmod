using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

#nullable disable

namespace Vintagestory.GameContent
{

    public enum EnumClutterDropRule
    {
        NeverObtain = 0,
        AlwaysObtain = 1,
        Reparable = 2
    }



    /// <summary>
    /// Blocks which can be repaired using glue. If not repaired, they will shatter (dropping nothing) when broken. 
    /// Requires use of the ShapeFromAttributes block entity behavior.
    /// Uses the code "Reparable".
    /// </summary>
    /// <example> <code lang="json">
    ///"behaviors": [
	///	{ "name": "Reparable" }
	///]
    /// </code>
    /// </example>
    [DocumentAsJson]
    [AddDocumentationProperty("Reparability", "The amount of glue needed for a full repair (abstract units corresponding to 1 resin, PLUS ONE), " +
        "e.g. 5 resin is shown as 6.   0 means unspecified (we don't use the repair system), -1 means cannot be repaired will alway shatter.", "System.Int32", "Recommended", "0")]
    public class BlockBehaviorReparable : BlockBehavior
    {
        public BlockBehaviorReparable(Block block) : base(block)
        {
        }

        // Called from BEBehaviorShapeFromAttributes.Initialize
        public virtual void Initialize(string type, BEBehaviorShapeFromAttributes bec)
        {
            BlockShapeFromAttributes clutterBlock = bec.clutterBlock;
            var cprops = clutterBlock.GetTypeProps(type, null, bec);
            if (cprops != null)
            {
                int reparability = cprops.Reparability;
                if (reparability == 0) reparability = clutterBlock.Attributes["reparability"].AsInt();
                bec.reparability = reparability;
                if (reparability == 1) bec.repairState = 1.0f;
            }
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            var bec = block.GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);

            if (bec == null || ShatterWhenBroken(world, bec, GetRule(world)))
            {
                if (byPlayer is IServerPlayer splr)
                {
                    splr.SendLocalisedMessage(GlobalConstants.GeneralChatGroup, "clutter-didshatter", Lang.GetMatchingL(splr.LanguageCode, bec.GetFullCode()));
                }
                world.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), pos, 0, null, false, 12);
                return Array.Empty<ItemStack>();
            }

            var stack = block.OnPickBlock(world, pos);
            stack.Attributes.SetBool("collected", true);
            return new ItemStack[] { stack };
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BEBehaviorShapeFromAttributes bec = block.GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            if (bec == null || bec.Collected) return base.GetPlacedBlockInfo(world, pos, forPlayer);

            if (world.Claims.TestAccess(forPlayer, pos, EnumBlockAccessFlags.BuildOrBreak) != EnumWorldAccessResponse.Granted) return ""; 

            EnumClutterDropRule rule = GetRule(world);
            if (rule == EnumClutterDropRule.Reparable)
            {
                if (bec.reparability > 0)
                {
                    int repairLevel = GameMath.Clamp((int)(bec.repairState * 100.001f), 0, 100);
                    if (repairLevel < 100)
                    {
                        return Lang.Get("clutter-reparable") + "\n" + Lang.Get("{0}% repaired", repairLevel) + "\n";
                    }
                    else return Lang.Get("clutter-fullyrepaired", repairLevel) + "\n";
                }
                else if (bec.reparability < 0)
                {
                    return Lang.Get("clutter-willshatter") + "\n";
                }
            }
            if (rule == EnumClutterDropRule.NeverObtain) return Lang.Get("clutter-willshatter") + "\n";

            return "";
        }


        public virtual bool ShatterWhenBroken(IWorldAccessor world, BEBehaviorShapeFromAttributes bec, EnumClutterDropRule configRule)
        {
            if (bec.Collected) return false;    // Player-placed items never shatter

            switch (configRule)
            {
                case EnumClutterDropRule.NeverObtain: return true;
                case EnumClutterDropRule.AlwaysObtain: return false;
                case EnumClutterDropRule.Reparable: return world.Rand.NextDouble() > bec.repairState;
                default: return true;
            }
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                Entity byEntity = byPlayer.Entity;
                var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
                float repairQuantity = GetItemRepairAmount(world, slot);

                if (repairQuantity > 0f)   // It's a type of glue item
                {
                    var rule = GetRule(world);
                    string message = null;
                    string parameter = null;
                    if (rule == EnumClutterDropRule.Reparable)
                    {
                        BEBehaviorShapeFromAttributes bec = block.GetBEBehavior<BEBehaviorShapeFromAttributes>(blockSel.Position);
                        if (bec == null)
                        {
                            message = "clutter-error";
                        }
                        else if (bec.repairState < 1f && bec.reparability > 1)
                        {
                            if (repairQuantity < 0.001f)
                            {
                                message = "clutter-gluehardened";
                            }
                            else
                            {
                                bec.repairState += repairQuantity * 5 / (bec.reparability - 1);

                                if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                                {
                                    if (slot.Itemstack.Collectible is IBlockMealContainer mc)
                                    {
                                        float servingsLeft = mc.GetQuantityServings(world, slot.Itemstack) - 1f;
                                        mc.SetQuantityServings(world, slot.Itemstack, servingsLeft);
                                        if (servingsLeft <= 0f)
                                        {
                                            string emptyCode = slot.Itemstack.Collectible.Attributes["emptiedBlockCode"].AsString();
                                            if (emptyCode != null)
                                            {
                                                Block emptyPotBlock = world.GetBlock(new AssetLocation(emptyCode));
                                                if (emptyPotBlock != null) slot.Itemstack = new ItemStack(emptyPotBlock);
                                            }
                                        }

                                        slot.MarkDirty();
                                    }
                                    else if (slot.Itemstack.Collectible is BlockLiquidContainerBase cont)
                                    {
                                        var lStack = cont.GetContent(slot.Itemstack);
                                        float standardAmount = lStack.ItemAttributes["repairGain"].AsFloat(0.2f);
                                        float itemsPerLitre = cont.GetContentProps(lStack)?.ItemsPerLitre ?? 100;

                                        int moved = (int)(repairQuantity / standardAmount * itemsPerLitre);
                                        cont.SplitStackAndPerformAction(byPlayer.Entity, slot, (stack) =>
                                        {
                                            return cont.TryTakeContent(stack, moved)?.StackSize ?? 0;
                                        });

                                        slot.MarkDirty();
                                    }
                                    else slot.TakeOut(1);
                                }

                                message = "clutter-repaired";
                                parameter = bec.GetFullCode();

                                if (world.Side == EnumAppSide.Client)
                                {
                                    var sound = AssetLocation.Create("sounds/player/gluerepair");
                                    world.PlaySoundAt(sound, blockSel.Position, 0, byPlayer, true, 8);
                                }
                            }
                        }
                        else
                        {
                            message = "clutter-norepair";
                        }
                    }
                    else
                    {
                        message = rule == EnumClutterDropRule.AlwaysObtain ? "clutter-alwaysobtain" : "clutter-neverobtain";
                    }

                    if (byPlayer is IServerPlayer splr && message != null)
                    {
                        if (parameter == null) splr.SendLocalisedMessage(GlobalConstants.GeneralChatGroup, message);
                        else splr.SendLocalisedMessage(GlobalConstants.GeneralChatGroup, message, Lang.GetMatchingL(splr.LanguageCode, parameter));
                    }

                    handling = EnumHandling.Handled;
                    return true;
                }
            }

            handling = EnumHandling.PassThrough;
            return false;
        }

        private float GetItemRepairAmount(IWorldAccessor world, ItemSlot slot)
        {
            if (slot.Empty) return 0f;
            ItemStack stack = slot.Itemstack;
            if (stack.Collectible.Attributes?["repairGain"].Exists == true)
            {
                return stack.Collectible.Attributes["repairGain"].AsFloat(0.2f);
            }

            if (stack.Collectible is IBlockMealContainer mc)
            {
                ItemStack[] stacks = mc.GetNonEmptyContents(world, stack);
                if (stacks.Length > 0 && stacks[0] != null && stacks[0].Collectible.Code.PathStartsWith("glueportion")) return stacks[0].Collectible.Attributes["repairGain"].AsFloat(0.2f);

                var recipe = mc.GetRecipeCode(world, stack);
                if (recipe == null) return 0f;                      // Covers the case of pies
                Item outputItem = world.GetItem(new AssetLocation(recipe));
                if (outputItem != null && outputItem.Attributes?["repairGain"].Exists == true)
                {
                    float standardAmount = outputItem.Attributes["repairGain"].AsFloat(0.2f);
                    return standardAmount * Math.Min(1f, mc.GetQuantityServings(world, stack));
                }
            }

            if (stack.Collectible is BlockLiquidContainerBase cont)
            {
                ItemStack lStack = cont.GetContent(stack);

                if (lStack != null && lStack.ItemAttributes?["repairGain"].Exists == true)
                {
                    float standardAmount = lStack.ItemAttributes["repairGain"].AsFloat(0.2f);
                    return standardAmount * Math.Min(1f, lStack.StackSize / cont.GetContentProps(stack).ItemsPerLitre);
                }
            }

            return 0f;
        }

        protected EnumClutterDropRule GetRule(IWorldAccessor world)
        {
            string config = world.Config.GetString("clutterObtainable", "ifrepaired").ToLowerInvariant();
            if (config == "yes") return EnumClutterDropRule.AlwaysObtain;
            if (config == "no") return EnumClutterDropRule.NeverObtain;
            return EnumClutterDropRule.Reparable;
        }
    }


}
