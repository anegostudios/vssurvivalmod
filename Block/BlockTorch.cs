using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockTorch : BlockGroundAndSideAttachable, IIgnitable
    {
        public bool IsExtinct => Variant["state"] == "extinct";

        Dictionary<string, Cuboidi> attachmentAreas;

        WorldInteraction[] interactions;

        public Block ExtinctVariant { get; private set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            var areas = Attributes?["attachmentAreas"].AsObject<Dictionary<string, RotatableCube>>(null);
            if (areas != null)
            {
                attachmentAreas = new Dictionary<string, Cuboidi>();
                foreach (var val in areas)
                {
                    val.Value.Origin.Set(8, 8, 8);
                    attachmentAreas[val.Key] = val.Value.RotatedCopy().ConvertToCuboidi();
                }
            }

            if (Variant.ContainsKey("state"))
            {
                AssetLocation loc = CodeWithVariant("state", "extinct");
                ExtinctVariant = api.World.GetBlock(loc);
            }

            if (IsExtinct)
            {
                interactions = ObjectCacheUtil.GetOrCreate(api, "torchInteractions" + FirstCodePart(), () =>
                {
                    List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(api, true);

                    return new WorldInteraction[]
                    {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-firepit-ignite",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = canIgniteStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            return wi.Itemstacks;
                        }
                    }
                    };
                });
            }
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (api.World.Side == EnumAppSide.Server && byEntity.Swimming && !IsExtinct && ExtinctVariant != null)
            {
                api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), byEntity.Pos.X + 0.5, byEntity.Pos.Y + 0.75, byEntity.Pos.Z + 0.5, null, false, 16);
                
                int q = slot.Itemstack.StackSize;
                slot.Itemstack = new ItemStack(ExtinctVariant);
                slot.Itemstack.StackSize = q;
                slot.MarkDirty();
            }
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            if (!IsExtinct && entityItem.Swimming && ExtinctVariant != null)
            {
                api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), entityItem.Pos.X + 0.5, entityItem.Pos.Y + 0.75, entityItem.Pos.Z + 0.5, null, false, 16);

                int q = entityItem.Itemstack.StackSize;
                entityItem.Itemstack = new ItemStack(ExtinctVariant);
                entityItem.Itemstack.StackSize = q;
            }
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (Variant["state"] == "burnedout") return new ItemStack[0];

            Block block = world.BlockAccessor.GetBlock(CodeWithVariant("orientation", "up"));
            return new ItemStack[] { new ItemStack(block) };
        }


        public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            if (Variant["state"] == "burnedout") return EnumIgniteState.NotIgnitablePreventDefault;

            if (IsExtinct) return secondsIgniting > 1 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;

            return EnumIgniteState.NotIgnitable;
        }

        public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            var block = api.World.GetBlock(CodeWithVariant("state", "lit"));
            if (block != null)
            {
                api.World.BlockAccessor.SetBlock(block.Id, pos);
            }
        }

        public override void OnAttackingWith(IWorldAccessor world, Entity byEntity, Entity attackedEntity, ItemSlot itemslot)
        {
            base.OnAttackingWith(world, byEntity, attackedEntity, itemslot);

            float stormstr = api.ModLoader.GetModSystem<SystemTemporalStability>().StormStrength;

            if (!IsExtinct && attackedEntity != null && byEntity.World.Side == EnumAppSide.Server && api.World.Rand.NextDouble() < 0.1 + stormstr)
            {
                attackedEntity.Ignite();
            }
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer).Append(interactions);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (Attributes != null && Variant["state"] == "lit")
            {
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("Burns for {0} hours when placed.", Attributes["transientProps"]["inGameHours"].AsFloat()));
            }
        }

    }
}
