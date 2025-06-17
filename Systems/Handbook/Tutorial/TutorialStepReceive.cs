using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class TutorialStepLookatBlock : TutorialStepGeneric
    {
        protected override PlayerMilestoneWatcherGeneric watcher => bwatcher;
        protected PlayerLookatBlockWatcher bwatcher;

        public TutorialStepLookatBlock(ICoreClientAPI capi, string text, BlockLookatMatcherDelegate matcher, int goal) : base(capi, text)
        {
            bwatcher = new PlayerLookatBlockWatcher();
            bwatcher.BlockMatcher = matcher;
            bwatcher.QuantityGoal = goal;
        }

        public override bool OnBlockPlaced(BlockPos pos, Block block, ItemStack withStackInHands)
        {
            return false;
        }

        public override bool OnBlockLookedAt(BlockSelection currentBlockSelection)
        {
            bwatcher.OnBlockLookedAt(currentBlockSelection);
            if (bwatcher.Dirty)
            {
                bwatcher.Dirty = false;
                return true;
            }

            return false;
        }
    }

    public class TutorialStepPlaceBlock : TutorialStepGeneric
    {
        protected override PlayerMilestoneWatcherGeneric watcher => bwatcher;
        protected PlayerPlaceBlockWatcher bwatcher;

        public TutorialStepPlaceBlock(ICoreClientAPI capi, string text, BlockMatcherDelegate matcher, int goal) : base(capi, text)
        {
            bwatcher = new PlayerPlaceBlockWatcher();
            bwatcher.BlockMatcher = matcher;
            bwatcher.QuantityGoal = goal;
        }

        public override bool OnBlockPlaced(BlockPos pos, Block block, ItemStack withStackInHands)
        {
            bwatcher.OnBlockPlaced(pos, block, withStackInHands);
            if (bwatcher.Dirty)
            {
                bwatcher.Dirty = false;
                return true;
            }

            return false;
        }
    }

    public class TutorialStepReceive : TutorialStepGeneric
    {
        static Dictionary<EnumReceiveType, string> receiveEventMapping = new Dictionary<EnumReceiveType, string>() { 
            { EnumReceiveType.Collect, "onitemcollected" },
            { EnumReceiveType.Craft, "onitemcrafted" },
            { EnumReceiveType.Knap, "onitemknapped" },
            { EnumReceiveType.Clayform, "onitemclayformed" },
            { EnumReceiveType.Grab, "onitemgrabbed" },
        };

        PlayerReceiveItemWatcher rwatcher;
        protected EnumReceiveType receiveType;

        protected override PlayerMilestoneWatcherGeneric watcher => rwatcher;

        public TutorialStepReceive(ICoreClientAPI capi, string text, ItemStackMatcherDelegate matcher, EnumReceiveType enumReceiveType, int goal) : base(capi, text)
        {
            this.receiveType = enumReceiveType; 
            rwatcher = new PlayerReceiveItemWatcher();
            rwatcher.StackMatcher = matcher;
            rwatcher.QuantityGoal = goal;
            rwatcher.MatchEventName = receiveEventMapping[enumReceiveType];
        }

        public override RichTextComponentBase[] GetText(CairoFont font)
        {          
            string stats ="";
            if (rwatcher.QuantityGoal > 1) stats = " " + Lang.Get("({0}/{1} collected)", rwatcher.QuantityAchieved, rwatcher.QuantityGoal);

            string vtmlCode;

            switch (receiveType)
            {
                //case EnumReceiveType.Clayform: vtmlCode = Lang.Get(text, stats); break; 
                //case EnumReceiveType.Knap: vtmlCode = Lang.Get(text, stats); break;
                //case EnumReceiveType.Craft: vtmlCode = Lang.Get(text, stats); break;   // Commented out because "0/4 collected" looks wrong for crafting items, and crafting is usually all or nothing
                default: vtmlCode = Lang.Get(text, rwatcher.QuantityAchieved >= rwatcher.QuantityGoal ? rwatcher.QuantityGoal : rwatcher.QuantityGoal - rwatcher.QuantityAchieved); break;
            }
            
            vtmlCode = Lang.Get("tutorialstep-numbered", index + 1, vtmlCode);
            return VtmlUtil.Richtextify(capi, vtmlCode, font);
        }

        public override bool OnItemStackReceived(ItemStack stack, string eventName)
        {
            rwatcher.OnItemStackReceived(stack, eventName);
            if (rwatcher.Dirty)
            {
                rwatcher.Dirty = false;
                return true;
            }

            return false;
        }


    }
}