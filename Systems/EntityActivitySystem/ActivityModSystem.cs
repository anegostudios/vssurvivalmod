using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class ActivityModSystem : ModSystem
    {
        public static OrderedDictionary<string, Type> ActionTypes = new OrderedDictionary<string, Type>();
        public static OrderedDictionary<string, Type> ConditionTypes = new OrderedDictionary<string, Type>();

        public override bool ShouldLoad(ICoreAPI api) => true;

        public override void Start(ICoreAPI api)
        {
            ActionTypes["goto"] = typeof(GotoAction);
            ActionTypes["activateblock"] = typeof(ActivateBlockAction);
            ActionTypes["lookatblock"] = typeof(LookatBlockAction);
            ActionTypes["lookatentity"] = typeof(LookatEntityAction);
            ActionTypes["mountblock"] = typeof(MountBlockAction);
            ActionTypes["animation"] = typeof(AnimationAction);
            ActionTypes["standardai"] = typeof(StandardAIAction);
            ActionTypes["triggeremotionstate"] = typeof(TriggerEmotionStateAction);
            ActionTypes["unmount"] = typeof(UnmountAction);
            ActionTypes["teleport"] = typeof(TeleportAction);
            ActionTypes["playsound"] = typeof(PlaySoundAction);
            ActionTypes["setvariable"] = typeof(SetVarAction);
            ActionTypes["equip"] = typeof(EquipAction);
            ActionTypes["unequip"] = typeof(UnequipAction);
            ActionTypes["dress"] = typeof(DressAction);
            ActionTypes["undress"] = typeof(UndressAction);
            ActionTypes["jump"] = typeof(JumpAction);
            ActionTypes["wait"] = typeof(WaitAction);

            ConditionTypes["timeofday"] = typeof(TimeOfDayCondition);
            ConditionTypes["entityvicinity"] = typeof(EntityVicinityCondition);
            ConditionTypes["variable"] = typeof(VariableCondition);
            ConditionTypes["mounted"] = typeof(MountedCondition);
            ConditionTypes["temporalstorm"] = typeof(TemporalStormCondition);
            ConditionTypes["temperature"] = typeof(TemperatureCondition);
            ConditionTypes["afteractivity"] = typeof(AfterActivityCondition);
            ConditionTypes["emotionstate"] = typeof(EmotionStateCondition);
            ConditionTypes["blockvicinity"] = typeof(BlockVicinityCondition);
            ConditionTypes["random"] = typeof(RandomCondition);
        }
    }
}
