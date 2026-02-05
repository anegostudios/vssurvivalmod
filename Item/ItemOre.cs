using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemOre : ItemPileable, IContainedInteractable
    {
        public bool IsCoal => Variant["ore"] == "lignite" || Variant["ore"] == "bituminouscoal" || Variant["ore"] == "anthracite";
        public override bool IsPileable => IsCoal;
        protected override AssetLocation PileBlockCode => new AssetLocation("coalpile");

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            var combustibleProps = inSlot.Itemstack.Collectible.GetCombustibleProperties(world, inSlot.Itemstack, null);
            if (combustibleProps?.SmeltedStack?.ResolvedItemstack == null)
            {
                if (Attributes?["metalUnits"].Exists == true)
                {
                    float units = Attributes["metalUnits"].AsInt();

                    string orename = LastCodePart(1);
                    if (orename.Contains("_"))
                    {
                        orename = orename.Split('_')[1];
                    }
                    AssetLocation loc = new AssetLocation("nugget-" + orename);
                    Item item = api.World.GetItem(loc);

                    if (item != null)
                    {
                        ItemStack nuggetStack = new ItemStack(item);
                        CombustibleProperties nuggetCombustibleProps = nuggetStack.Collectible.GetCombustibleProperties(world, nuggetStack, null);
                        if (nuggetCombustibleProps?.SmeltedStack?.ResolvedItemstack != null)
                        {
                            string smelttype = nuggetCombustibleProps.SmeltingType.ToString().ToLowerInvariant();

                            string metal = nuggetCombustibleProps.SmeltedStack.ResolvedItemstack.Collectible?.Variant?["metal"];
                            string metalname = Lang.Get("material-" + metal);
                            if (metal == null) metalname = nuggetCombustibleProps.SmeltedStack.ResolvedItemstack.GetName();

                            dsc.AppendLine(Lang.Get("game:smeltdesc-" + smelttype + "ore-plural", units.ToString("0.#"), metalname));
                        }
                    }

                    dsc.AppendLine(Lang.Get("Parent Material: {0}", Lang.Get("rock-" + LastCodePart())));
                    dsc.AppendLine();
                    dsc.AppendLine(Lang.Get("Crush with hammer to extract nuggets"));
                }
            }
            else
            {

                base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

                if (combustibleProps.SmeltedStack.ResolvedItemstack.Collectible.FirstCodePart() == "ingot")
                {
                    string smelttype = combustibleProps.SmeltingType.ToString().ToLowerInvariant();
                    int instacksize = combustibleProps.SmeltedRatio;
                    int outstacksize = combustibleProps.SmeltedStack.ResolvedItemstack.StackSize;
                    float units = outstacksize * 100f / instacksize;

                    string metal = combustibleProps.SmeltedStack.ResolvedItemstack.Collectible?.Variant?["metal"];
                    string metalname = Lang.Get("material-" + metal);
                    if (metal == null) metalname = combustibleProps.SmeltedStack.ResolvedItemstack.GetName();

                    string str = Lang.Get("game:smeltdesc-" + smelttype + "ore-plural", units.ToString("0.#"), metalname);
                    dsc.AppendLine(str);
                }

                return;
            }


            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (Attributes?["metalUnits"].Exists == true)
            {
                string orename = LastCodePart(1);
                string rockname = LastCodePart(0);

                if (FirstCodePart() == "crystalizedore")
                {
                    return Lang.Get(LastCodePart(2) + "-crystallizedore-chunk", Lang.Get("ore-" + orename));

                }
                return Lang.Get(LastCodePart(2) + "-ore-chunk", Lang.Get("ore-" + orename));

            }

            return base.GetHeldItemName(itemStack);
        }

        bool CanBreakOnMaterial(EnumBlockMaterial blockMaterial)
        {
            return blockMaterial is EnumBlockMaterial.Stone or EnumBlockMaterial.Metal or EnumBlockMaterial.Mantle or EnumBlockMaterial.Brick or EnumBlockMaterial.Ore;
        }

        public bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Attributes?["metalUnits"].Exists != true ||
                byPlayer.InventoryManager.ActiveTool != EnumTool.Hammer ||
                !byPlayer.Entity.Controls.ShiftKey ||
                !be.Api.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            if (!CanBreakOnMaterial(be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy()).BlockMaterial))
            {
                (be.Api as ICoreClientAPI)?.TriggerIngameError(this, "needssolidsurface", Lang.Get("itemore-needssolid-error"));
                return false;
            }

            return true;
        }

        public bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Attributes?["metalUnits"].Exists != true ||
                !CanBreakOnMaterial(be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy()).BlockMaterial) ||
                byPlayer.InventoryManager.ActiveTool != EnumTool.Hammer ||
                !byPlayer.Entity.Controls.ShiftKey ||
                blockSel == null)
            {
                return false;
            }

            if (byPlayer is IClientPlayer) byPlayer.Entity.StartAnimation("hammerhit-fp");

            if (be.Api.World.Rand.NextDouble() < 0.1)
            {
                be.Api.World.PlaySoundAt("sounds/block/rock-hit-pickaxe", blockSel.Position, 0);
            }

            if (be.Api.World.Side == EnumAppSide.Client && be.Api.World.Rand.NextDouble() < 0.25)
            {
                be.Api.World.SpawnCubeParticles(blockSel.Position.ToVec3d().Add(blockSel.HitPosition), slot.Itemstack, 0.25f, 1, 0.5f, byPlayer, new Vec3f(0, 1, 0));
            }

            return be.Api.World.Side == EnumAppSide.Client || secondsUsed < 2f;
        }

        public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Attributes?["metalUnits"].Exists != true ||
                !CanBreakOnMaterial(be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy()).BlockMaterial) ||
                byPlayer.InventoryManager.ActiveTool != EnumTool.Hammer)
            {
                return;
            }

            if (secondsUsed > 2f - 0.05f && be.Api.World.Side == EnumAppSide.Server)
            {
                byPlayer.Entity.StopAnimation("hammerhit-fp");

                var toolSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                int units = slot.Itemstack.ItemAttributes["metalUnits"].AsInt(5);
                string type = slot.Itemstack.Collectible.Variant["ore"].Replace("quartz_", "").Replace("galena_", "");

                ItemStack outStack = new ItemStack(api.World.GetItem("nugget-" + type), Math.Max(1, units / 5));
                int smashed = 0;
                for (; smashed < slot.StackSize; smashed++)
                {
                    if (toolSlot.Empty || smashed >= 4) break;

                    for (int k = 0; k < outStack.StackSize; k++)
                    {
                        ItemStack stack = outStack.Clone();
                        stack.StackSize = 1;
                        be.Api.World.SpawnItemEntity(stack, blockSel.Position);
                    }
                    toolSlot.Itemstack.Collectible.DamageItem(be.Api.World, byPlayer.Entity, toolSlot);
                }

                if (slot.StackSize <= smashed) slot.Itemstack = null;
                else slot.Itemstack.StackSize -= smashed;
                be.MarkDirty(true);
                if (be.Inventory.Empty) be.Api.World.BlockAccessor.SetBlock(0, blockSel.Position);

                be.Api.World.PlaySoundAt("sounds/block/rock-break-pickaxe", blockSel.Position, 0);
            }
        }


        public WorldInteraction[] GetContainedInteractionHelp(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Attributes?["metalUnits"].Exists == true && CanBreakOnMaterial(be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy()).BlockMaterial))
            {
                bool notProtected = true;

                if (be.Api.World.Claims != null && be.Api.World is IClientWorldAccessor clientWorld && clientWorld.Player?.WorldData.CurrentGameMode == EnumGameMode.Survival)
                {
                    EnumWorldAccessResponse resp = clientWorld.Claims.TestAccess(clientWorld.Player, blockSel.Position, EnumBlockAccessFlags.Use);
                    if (resp != EnumWorldAccessResponse.Granted) notProtected = false;
                }

                if (notProtected) return
                [
                    new()
                    {
                        ActionLangCode = "itemhelp-ore-smash",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = ObjectCacheUtil.GetToolStacks(api, EnumTool.Hammer)
                    }
                ];
            }

            return [];
        }

    }
}
