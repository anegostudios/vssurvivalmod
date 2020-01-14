using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockForge : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "forgeBlockInteractions", () =>
            {
                List<ItemStack> heatableStacklist = new List<ItemStack>();
                List<ItemStack> fuelStacklist = new List<ItemStack>();
                List<ItemStack> canIgniteStacks = new List<ItemStack>();

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
                            if (stacks != null) fuelStacklist.AddRange(stacks);
                        }
                    }

                    if (obj is Block && (obj as Block).HasBehavior<BlockBehaviorCanIgnite>())
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) canIgniteStacks.AddRange(stacks);
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-forge-addworkitem",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = heatableStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityForge bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityForge;
                            if (bef!= null && bef.Contents != null)
                            {
                                return wi.Itemstacks.Where(stack => stack.Equals(api.World, bef.Contents, GlobalConstants.IgnoredStackAttributes)).ToArray();
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
                            if (bef!= null && bef.Contents != null)
                            {
                                return new ItemStack[] { bef.Contents };
                            }
                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-forge-fuel",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = fuelStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityForge bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityForge;
                            if (bef!= null && bef.FuelLevel < 10/16f)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-forge-ignite",
                        HotKeyCode = "sneak",
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


        public override EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            BlockEntityForge bea = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityForge;

            if (bea==null || !bea.CanIgnite)
            {
                return EnumIgniteState.NotIgnitablePreventDefault;
            }

            if (secondsIgniting > 0.25f && (int)(30 * secondsIgniting) % 9 == 1)
            {
                Random rand = byEntity.World.Rand;
                Vec3d dpos = new Vec3d(pos.X + 2 / 8f + 4 / 8f * rand.NextDouble(), pos.Y + 7 / 8f, pos.Z + 2 / 8f + 4 / 8f * rand.NextDouble());

                Block blockFire = byEntity.World.GetBlock(new AssetLocation("fire"));

                AdvancedParticleProperties props = blockFire.ParticleProperties[blockFire.ParticleProperties.Length - 1];
                props.basePos = dpos;
                props.Quantity.avg = 1;

                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                byEntity.World.SpawnParticles(props, byPlayer);

                props.Quantity.avg = 0;
            }

            if (secondsIgniting >= 2f)
            {
                return EnumIgniteState.IgniteNow;
            }

            return EnumIgniteState.Ignitable;
        }

        public override void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            if (secondsIgniting < 1.95f) return;

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
