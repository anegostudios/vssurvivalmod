using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockResonator : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "echoChamberBlockInteractions", () =>
            {
                List<ItemStack> echochamberStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Attributes?.IsTrue("isPlayableDisc") == true)
                    {
                        echochamberStacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bloomery-playdisc",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = echochamberStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityResonator bee = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityResonator;
                            if (bee == null || !bee.HasDisc) return wi.Itemstacks;

                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bloomery-takedisc",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityResonator bee = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityResonator;
                            return bee != null && bee.HasDisc;
                        }
                    }
                };
            });
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel.Position == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            BlockEntityResonator beec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityResonator;

            if (beec != null)
            {
                beec.OnInteract(world, byPlayer);
            }

            return true;
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
