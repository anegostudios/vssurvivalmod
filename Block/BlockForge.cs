using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable enable

namespace Vintagestory.GameContent
{
    public class BlockForge : Block, IIgnitable
    {
        WorldInteraction[]? interactions;


        public List<ItemStack> coalStacklist = [];

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is not ICoreClientAPI capi) return;

            interactions = ObjectCacheUtil.GetOrCreate(api, "forgeBlockInteractions", () =>
            {
                List<ItemStack> heatableStacklist = [];
                List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(api, false);

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    string firstCodePart = obj.FirstCodePart();

                    if (firstCodePart == "ingot" || firstCodePart == "metalplate" || firstCodePart == "workitem")
                    {
                        if (obj.GetHandBookStacks(capi) is List<ItemStack> stacks) heatableStacklist.AddRange(stacks);
                    }
                    else
                    {
                        if (obj.CombustibleProps?.BurnTemperature > 1000)
                        {
                            List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                            if (stacks != null) coalStacklist.AddRange(stacks);
                        }
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-forge-addworkitem",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = heatableStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityForge bef && bef.WorkItemStack is ItemStack workStack)
                            {
                                return wi.Itemstacks.Where(stack => stack.Equals(api.World, workStack, GlobalConstants.IgnoredStackAttributes)).ToArray();
                            }
                            return wi.Itemstacks;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-forge-takeworkitem",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = heatableStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityForge bef && bef.WorkItemStack is ItemStack workStack)
                            {
                                return [ workStack ];
                            }
                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-forge-fuel",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = coalStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityForge bef && bef.FuelLevel < 5/16f)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-forge-ignite",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = canIgniteStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityForge bef && bef.CanIgnite && !bef.IsBurning)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    }
                };
            });
        }


        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (world.Rand.NextDouble() < 0.05 && GetBlockEntity<BlockEntityForge>(pos)?.IsBurning == true)
            {
                entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = this, Type = EnumDamageType.Fire, SourcePos = pos.ToVec3d() }, 0.5f);
            }

            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            if (!base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack)) return false;

            if (GetBlockEntity<BlockEntityForge>(blockSel.Position) is not BlockEntityForge bef) return true;

            BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
            double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
            double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
            float angleHor = (float)Math.Atan2(dx, dz);

            float deg90 = GameMath.PIHALF;
            bef.MeshAngleRad = ((int)Math.Round(angleHor / deg90)) * deg90;

            return true;
        }

        EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
        {
            if (byEntity.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityForge bef && bef.IsBurning)
            {
                return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
            }

            return EnumIgniteState.NotIgnitable;
        }

        public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            if (byEntity.World.BlockAccessor.GetBlockEntity(pos) is not BlockEntityForge bef || !bef.CanIgnite)
            {
                return EnumIgniteState.NotIgnitablePreventDefault;
            }

            if (secondsIgniting > 0.25f && (int)(30 * secondsIgniting) % 9 == 1)
            {
                Random rand = byEntity.World.Rand;
                Vec3d dpos = new Vec3d(pos.X + 2 / 8f + 4 / 8f * rand.NextDouble(), pos.InternalY + 7 / 8f, pos.Z + 2 / 8f + 4 / 8f * rand.NextDouble());

                if (byEntity.World.GetBlock(new AssetLocation("fire")) is Block blockFire)
                {

                    AdvancedParticleProperties props = blockFire.ParticleProperties[blockFire.ParticleProperties.Length - 1];
                    props.basePos = dpos;
                    props.Quantity.avg = 1;

                    byEntity.World.SpawnParticles(props, byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID));

                    props.Quantity.avg = 0;
                }
            }

            if (secondsIgniting >= 1.5f)
            {
                return EnumIgniteState.IgniteNow;
            }

            return EnumIgniteState.Ignitable;
        }

        public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            if (secondsIgniting < 1.45f) return;

            handling = EnumHandling.PreventDefault;

            if (byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID) == null) return;

            (byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityForge)?.TryIgnite();
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityForge bef)
            {
                return bef.OnPlayerInteract(world, byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
