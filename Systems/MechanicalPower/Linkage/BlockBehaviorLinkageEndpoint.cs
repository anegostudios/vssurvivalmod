using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockBehaviorLinkageEndpoint : BlockBehavior
    {
        ModSystemLinkage linkageSystem = null!;
        WorldInteraction[] removeInteractions = null!;
        WorldInteraction[] adjustInteractions = null!;

        public BlockBehaviorLinkageEndpoint(Block block) : base(block)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            linkageSystem = api.ModLoader.GetModSystem<ModSystemLinkage>()
                ?? throw new InvalidOperationException("The linkage mod system is not loaded.");
            removeInteractions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api, "mpLinkageRemoveInteractions", () =>
            {
                return
                [
                    new()
                    {
                        ActionLangCode = "linkage-remove",
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = ObjectCacheUtil.GetToolStacks(api, EnumTool.Wrench)
                    }
                ];
            });
            adjustInteractions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api, "mpLinkageAdjustInteractions", () =>
            {
                return
                [
                    new()
                    {
                        ActionLangCode = "linkage-adjust-peg",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = ObjectCacheUtil.GetToolStacks(api, EnumTool.Wrench)
                    }
                ];
            });
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer? byPlayer, float dropQuantityMultiplier, ref EnumHandling handling)
        {
            BlockSelection? selection = byPlayer?.CurrentBlockSelection;
            if (selection == null || !IsLinkageSelection(selection)) return;

            handling = EnumHandling.PreventDefault;
        }

        public bool IsLinkageSelection(BlockSelection selection)
        {
            return linkageSystem.IsLinkageSelection(selection);
        }

        public Cuboidf[] GetSelectionBoxes(BlockPos pos, Cuboidf[] baseBoxes)
        {
            return linkageSystem.GetSelectionBoxes(pos, baseBoxes);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            return linkageSystem.IsPointLinked(selection) ? removeInteractions : block is BlockAxleArm ? adjustInteractions : [];
        }

        public override int GetPlacedBlockInteractionHelpCount(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            return block is BlockAxleArm || linkageSystem.IsPointLinked(selection) ? 1 : 0;
        }
    }
}
