using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorRightClickPickup : BlockBehavior
    {
        bool dropsPickupMode = false;
        AssetLocation pickupSound;

        public BlockBehaviorRightClickPickup(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            dropsPickupMode = properties["dropsPickupMode"].AsBool(false);
            string strloc = properties["sound"].AsString();

            if (strloc == null) strloc = block.Attributes?["placeSound"].AsString();

            pickupSound = strloc == null ? null : AssetLocation.Create(strloc, block.Code.Domain);

        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            ItemStack[] stacks = new ItemStack[] { block.OnPickBlock(world, blockSel.Position) };

            if (dropsPickupMode)
            {
                float dropMul = 1f;

                if (block.Attributes?.IsTrue("forageStatAffected") == true)
                {
                    dropMul *= byPlayer.Entity.Stats.GetBlended("forageDropRate");
                }

                stacks = block.GetDrops(world, blockSel.Position, byPlayer, dropMul);
            }

            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return false;
            }

            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (!byPlayer.Entity.Controls.Sneak)
            {
                if (world.Side == EnumAppSide.Server
                    && (activeSlot.Empty || stacks.Length == 1 && activeSlot.Itemstack.Equals(world, stacks[0], API.Config.GlobalConstants.IgnoredStackAttributes))
                    && BlockBehaviorReinforcable.AllowRightClickPickup(world, blockSel.Position, byPlayer))
                {
                    bool blockToBreak = true;
                    foreach (var stack in stacks)
                    {
                        if (byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                        {
                            if (blockToBreak)
                            {
                                blockToBreak = false;
                                world.BlockAccessor.SetBlock(0, blockSel.Position);
                                world.BlockAccessor.TriggerNeighbourBlockUpdate(blockSel.Position);
                            }
                            world.PlaySoundAt(pickupSound ?? block.GetSounds(world.BlockAccessor, blockSel.Position).Place, byPlayer, null);
                            handling = EnumHandling.PreventDefault;
                            return true;
                        }
                    }
                }

                handling = EnumHandling.PreventDefault;

                return true;
            }

            return false;
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            return base.OnPickBlock(world, pos, ref handling);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handled)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-behavior-rightclickpickup",
                    MouseButton = EnumMouseButton.Right,
                    RequireFreeHand = true
                }
            };
        }

    }
}
