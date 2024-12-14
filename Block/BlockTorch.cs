using System;
using System.Collections.Generic;
using System.Text;
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
        public bool IsExtinct => isExtinct;
        private bool isExtinct;
        private bool isLit;

        Dictionary<string, Cuboidi> attachmentAreas;

        WorldInteraction[] interactions;

        public Block ExtinctVariant { get; private set; }

        public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand)
        {
            if (forEntity.AnimManager.IsAnimationActive("startfire")) return null;

            return base.GetHeldTpIdleAnimation(activeHotbarSlot, forEntity, hand);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            HeldPriorityInteract = true;

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
                isExtinct = Variant["state"] == "extinct" || Variant["state"] == "burnedout";
                isLit = Variant["state"] == "lit";
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
                api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), byEntity.Pos.X + 0.5, byEntity.Pos.InternalY + 0.75, byEntity.Pos.Z + 0.5, null, false, 16);

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
                api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), entityItem.Pos.X + 0.5, entityItem.Pos.InternalY + 0.75, entityItem.Pos.Z + 0.5, null, false, 16);

                int q = entityItem.Itemstack.StackSize;
                entityItem.Itemstack = new ItemStack(ExtinctVariant);
                entityItem.Itemstack.StackSize = q;
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is IIgnitable ign)
            {
                if (isExtinct)
                {
                    var state = ign.OnTryIgniteStack(byEntity, blockSel.Position, slot, 0);
                    if (state == EnumIgniteState.Ignitable)
                    {
                        byEntity.World.PlaySoundAt(new AssetLocation("sounds/torch-ignite"), byEntity, (byEntity as EntityPlayer)?.Player, false, 16);
                        handling = EnumHandHandling.PreventDefault;
                    }
                }
                else
                {
                    // prevent placing of torch while pointing at another torch (after igniting)
                    handling = EnumHandHandling.Handled;
                }
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (isExtinct && blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is IIgnitable ign)
            {
                var state = ign.OnTryIgniteStack(byEntity, blockSel.Position, slot, secondsUsed);
                if (state == EnumIgniteState.Ignitable)
                {
                    if (byEntity.World is IClientWorldAccessor)
                    {
                        if (secondsUsed > 0.25f && (int)(30 * secondsUsed) % 2 == 1)
                        {
                            Random rand = byEntity.World.Rand;
                            Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition).Add(rand.NextDouble() * 0.25 - 0.125, rand.NextDouble() * 0.25 - 0.125, rand.NextDouble() * 0.25 - 0.125);

                            Block blockFire = byEntity.World.GetBlock(new AssetLocation("fire"));

                            AdvancedParticleProperties props = blockFire.ParticleProperties[blockFire.ParticleProperties.Length - 1].Clone();
                            props.basePos = pos;
                            props.Quantity.avg = 0.5f;

                            byEntity.World.SpawnParticles(props, null);

                            props.Quantity.avg = 0;
                        }
                    }

                    return true;
                }
                if (state == EnumIgniteState.IgniteNow)
                {
                    if (byEntity.World.Side == EnumAppSide.Client) return false;

                    var stack = new ItemStack(byEntity.World.GetBlock(CodeWithVariant("state", "lit")));

                    if (slot.StackSize == 1)
                    {
                        slot.Itemstack = stack;
                    }
                    else
                    {
                        slot.TakeOut(1);
                        if (!byEntity.TryGiveItemStack(stack))
                        {
                            byEntity.World.SpawnItemEntity(stack, byEntity.Pos.XYZ);
                        }
                    }

                    slot.MarkDirty();
                    return false;
                }
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
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
            if (Variant["state"] == "burnedout") return new WorldInteraction[0];
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer).Append(interactions);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (Attributes != null && isLit)
            {
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("Burns for {0} hours when placed.", Attributes["transientProps"]["inGameHours"].AsFloat()));
            }
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
            if (isLit) ReplaceWithBurnedOut(world.BlockAccessor, blockPos);
        }

        protected void ReplaceWithBurnedOut(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block liquid = blockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
            if (liquid.IsLiquid() && liquid.LiquidLevel > 3)
            {
                var block = api.World.GetBlock(CodeWithVariant("state", "burnedout"));
                if (block != null)
                {
                    api.World.BlockAccessor.SetBlock(block.Id, pos);
                }
            }
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldgenRandom, BlockPatchAttributes attributes = null)
        {
            if (!base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldgenRandom, attributes)) return false;
            if (isLit) ReplaceWithBurnedOut(blockAccessor, pos);
            return true;
        }

        EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
        {
            if (!IsExtinct) return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
            return EnumIgniteState.NotIgnitable;
        }
    }
}
