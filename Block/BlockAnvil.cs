using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockAnvil : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            Dictionary<string, MetalPropertyVariant> metalsByCode = new Dictionary<string, MetalPropertyVariant>();

            MetalProperty metals = api.Assets.TryGet("worldproperties/block/metal.json").ToObject<MetalProperty>();
            for (int i = 0; i < metals.Variants.Length; i++)
            {
                // Metals currently don't have a domain
                metalsByCode[metals.Variants[i].Code.Path] = metals.Variants[i];
            }

            string metalType = LastCodePart();
            int ownMetalTier = 0;
            if (metalsByCode.ContainsKey(metalType)) ownMetalTier = metalsByCode[metalType].Tier;
            
            interactions = ObjectCacheUtil.GetOrCreate(api, "anvilBlockInteractions" + ownMetalTier, () =>
            {
                List<ItemStack> workableStacklist = new List<ItemStack>();
                List<ItemStack> hammerStacklist = new List<ItemStack>();
                

                foreach (Item item in api.World.Items)
                {
                    if (item.Code == null) continue;

                    bool viableTier = metalsByCode.ContainsKey(metalType) && metalsByCode[metalType].Tier <= ownMetalTier + 1;
                    if (item is ItemIngot && viableTier)
                    {
                        workableStacklist.Add(new ItemStack(item));
                    }

                    if (item is ItemHammer)
                    {
                        hammerStacklist.Add(new ItemStack(item));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-anvil-placeworkable",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = workableStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityAnvil bea = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityAnvil;
                            return bea?.WorkItem == null ? wi.Itemstacks : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-anvil-smith",
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = hammerStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityAnvil bea = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityAnvil;
                            return bea?.WorkItem == null ? null : wi.Itemstacks;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-anvil-addvoxels",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = workableStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityAnvil bea = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityAnvil;
                            return bea?.WorkItem == null ? null : new ItemStack[] { bea.BaseMaterial };
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-anvil-takeworkable",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) => {
                            BlockEntityAnvil bea = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityAnvil;
                            return bea?.WorkItem != null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-selecttoolmode",
                        HotKeyCode = "toolmodeselect",
                        MouseButton = EnumMouseButton.None,
                        Itemstacks = hammerStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityAnvil bea = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityAnvil;
                            return bea?.WorkItem == null ? null : wi.Itemstacks;
                        }
                    }
                };
            });
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityAnvil bea = blockAccessor.GetBlockEntity(pos) as BlockEntityAnvil;
            if (bea != null)
            {
                Cuboidf[] selectionBoxes = bea.GetSelectionBoxes(blockAccessor, pos);
                selectionBoxes[0] = this.SelectionBoxes[0];
                return selectionBoxes;
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityAnvil bea = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityAnvil;
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
