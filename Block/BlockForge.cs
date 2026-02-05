using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockForge : Block, IIgnitable
    {
        WorldInteraction[] interactions;

        public List<ItemStack> coalStacklist = new List<ItemStack>();

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "forgeBlockInteractions", () =>
            {
                List<ItemStack> heatableStacklist = new List<ItemStack>();
                List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(api, false);

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    string firstCodePart = obj.FirstCodePart();

                    if (firstCodePart == "ingot" || firstCodePart == "metalplate" || firstCodePart == "workitem")
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) heatableStacklist.AddRange(stacks);
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
                            BlockEntityForge bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityForge;
                            if (bef!= null && bef.WorkItemStack != null)
                            {
                                return wi.Itemstacks.Where(stack => stack.Equals(api.World, bef.WorkItemStack, GlobalConstants.IgnoredStackAttributes)).ToArray();
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
                            BlockEntityForge bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityForge;
                            if (bef!= null && bef.WorkItemStack != null)
                            {
                                return new ItemStack[] { bef.WorkItemStack };
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
                            BlockEntityForge bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityForge;
                            if (bef!= null && bef.FuelLevel < 5/16f)
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
                            BlockEntityForge bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityForge;
                            if (bef!= null && bef.CanIgnite && !bef.IsBurning)
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

            var beforge = GetBlockEntity<BlockEntityForge>(blockSel.Position);
            if (beforge == null) return true;
            
            BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
            double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
            double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
            float angleHor = (float)Math.Atan2(dx, dz);

            float deg90 = GameMath.PIHALF;
            beforge.MeshAngleRad = ((int)Math.Round(angleHor / deg90)) * deg90;

            return true;
        }

        EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
        {
            BlockEntityForge bea = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityForge;
            if (bea.IsBurning) return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
            return EnumIgniteState.NotIgnitable;
        }

        public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            BlockEntityForge bea = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityForge;

            if (bea==null || !bea.CanIgnite)
            {
                return EnumIgniteState.NotIgnitablePreventDefault;
            }

            if (secondsIgniting > 0.25f && (int)(30 * secondsIgniting) % 9 == 1)
            {
                Random rand = byEntity.World.Rand;
                Vec3d dpos = new Vec3d(pos.X + 2 / 8f + 4 / 8f * rand.NextDouble(), pos.InternalY + 7 / 8f, pos.Z + 2 / 8f + 4 / 8f * rand.NextDouble());

                Block blockFire = byEntity.World.GetBlock(new AssetLocation("fire"));

                AdvancedParticleProperties props = blockFire.ParticleProperties[blockFire.ParticleProperties.Length - 1];
                props.basePos = dpos;
                props.Quantity.avg = 1;

                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                byEntity.World.SpawnParticles(props, byPlayer);

                props.Quantity.avg = 0;
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

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            BlockEntityForge bea = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityForge;
            bea?.TryIgnite();
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityForge bea = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityForge;
            if (bea != null)
            {
                return bea.OnPlayerInteract(world, byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
