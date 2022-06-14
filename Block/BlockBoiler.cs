using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBoiler : BlockLiquidContainerBase
    {
        Block firepitBlock;
        WorldInteraction[] boilerinteractions;
        Cuboidf[] partCollBoxes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            firepitBlock = api.World.GetBlock(BlockEntityBoiler.firepitShapeBlockCodes[6]);

            partCollBoxes = (Cuboidf[])CollisionBoxes.Clone();
            partCollBoxes[0].Y1 = 7/16f;

            boilerinteractions = ObjectCacheUtil.GetOrCreate(api, "boilerInteractions", () =>
            {
                List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(api, true);

                List<ItemStack> tinderStacks = new List<ItemStack>();
                List<ItemStack> firewoodStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Items)
                {
                    if (obj is ItemDryGrass)
                    {
                        tinderStacks.Add(new ItemStack(obj));
                    }

                    if (obj.Attributes != null && obj.Attributes.IsTrue("isFirewood"))
                    {
                        firewoodStacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-firepit-ignite",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = canIgniteStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityBoiler bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityBoiler;
                            if (bef != null && !bef.IsBurning && bef.fuelHours > 0 && bef.firepitStage >= 5)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-boiler-addtinder",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = tinderStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityBoiler bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityBoiler;
                            if (bef != null && bef.firepitStage == 0) return wi.Itemstacks;
                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-boiler-addfuel",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = firewoodStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityBoiler bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityBoiler;
                            if (bef != null && bef.firepitStage > 0 && bef.fuelHours <= 6f) return wi.Itemstacks;
                            return null;
                        }
                    }
                };
            });
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(this, 1);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityBoiler be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBoiler;
            if (be != null)
            {
                bool handled = be.OnInteract(byPlayer, blockSel);
                if (handled) return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            if (pos == null) return base.GetLightHsv(blockAccessor, pos, stack);

            BlockEntityBoiler be = blockAccessor.GetBlockEntity(pos) as BlockEntityBoiler;
            if (be != null && be.firepitStage == 6) return firepitBlock.LightHsv;

            return base.GetLightHsv(blockAccessor, pos, stack);
        }

        
        public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return partCollBoxes;
        }

        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer player, BlockPos pos, out bool isWindAffected)
        {
            isWindAffected = true;

            BlockEntityBoiler be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBoiler;
            if (be != null && be.firepitStage == 6) return true;

            return base.ShouldReceiveClientParticleTicks(world, player, pos, out isWindAffected);
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            BlockEntityBoiler be = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBoiler;
            if (be != null && be.firepitStage == 6)
            {
                //firepitBlock.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
                var props = firepitBlock.ParticleProperties;

                if (props != null && props.Length > 0)
                {
                    for (int i = 0; i < props.Length; i++)
                    {
                        AdvancedParticleProperties bps = props[i];
                        bps.WindAffectednesAtPos = windAffectednessAtPos;
                        bps.basePos.X = pos.X + firepitBlock.TopMiddlePos.X;
                        bps.basePos.Y = pos.Y + firepitBlock.TopMiddlePos.Y;
                        bps.basePos.Z = pos.Z + firepitBlock.TopMiddlePos.Z;

                        manager.Spawn(bps);
                    }
                }

                return;
            }

            base.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
        }


        public override EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            BlockEntityBoiler beb = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBoiler;
            if (!beb.CanIgnite()) return EnumIgniteState.NotIgnitablePreventDefault;

            return secondsIgniting > 4 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
        }

        public override void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockEntityBoiler beb = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBoiler;
            beb?.TryIgnite();
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            string info = base.GetPlacedBlockInfo(world, pos, forPlayer);

            BlockEntityBoiler beb = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBoiler;
            float temp = beb?.InputStackTemp ?? 0;
            if (temp <= 20)
            {
                info += "\r\n" + Lang.Get("Cold.");
            }
            else
            {
                info += "\r\n" + Lang.Get("Temperature: {0}°C", (int)temp);
            }

            if (beb != null && beb.firepitStage >= 5)
            {
                if (beb.fuelHours <= 0)
                {
                    info += "\r\n" + Lang.Get("No more fuel.");
                } else
                {
                    info += "\r\n" + Lang.Get("Fuel for {0:#.#} hours.", beb.fuelHours);
                }
            }

            return info;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer).Append(boilerinteractions);
        }
    }
}
