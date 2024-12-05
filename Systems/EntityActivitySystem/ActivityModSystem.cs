using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class ActivityModSystem : ModSystem
    {
        public static OrderedDictionary<string, Type> ActionTypes = new OrderedDictionary<string, Type>();
        public static OrderedDictionary<string, Type> ConditionTypes = new OrderedDictionary<string, Type>();
        public static bool Debug = false;

        public override bool ShouldLoad(ICoreAPI api) => true;

        public override void Start(ICoreAPI api)
        {
            ActionTypes["goto"] = typeof(GotoAction);
            ActionTypes["activateblock"] = typeof(ActivateBlockAction);
            ActionTypes["lookatblock"] = typeof(LookatBlockAction);
            ActionTypes["lookatentity"] = typeof(LookatEntityAction);
            ActionTypes["turn"] = typeof(TurnAction);
            ActionTypes["mountblock"] = typeof(MountBlockAction);
            ActionTypes["playanimation"] = typeof(PlayAnimationAction);
            ActionTypes["stopanimation"] = typeof(StopAnimationAction);
            ActionTypes["triggeremotionstate"] = typeof(TriggerEmotionStateAction);
            ActionTypes["unmount"] = typeof(UnmountAction);
            ActionTypes["teleport"] = typeof(TeleportAction);
            ActionTypes["playsound"] = typeof(PlaySoundAction);
            ActionTypes["equip"] = typeof(EquipAction);
            ActionTypes["unequip"] = typeof(UnequipAction);
            ActionTypes["dress"] = typeof(DressAction);
            ActionTypes["undress"] = typeof(UndressAction);
            ActionTypes["jump"] = typeof(JumpAction);
            ActionTypes["setvariable"] = typeof(SetVarAction);
            ActionTypes["startactivity"] = typeof(StartActivityAction);
            ActionTypes["wait"] = typeof(WaitAction);
            ActionTypes["playsong"] = typeof(PlaySongAction);
            ActionTypes["talk"] = typeof(TalkAction);
            ActionTypes["standardai"] = typeof(StandardAIAction);

            ConditionTypes["timeofday"] = typeof(TimeOfDayCondition);
            ConditionTypes["variable"] = typeof(VariableCondition);
            ConditionTypes["mounted"] = typeof(MountedCondition);
            ConditionTypes["temporalstorm"] = typeof(TemporalStormCondition);
            ConditionTypes["temperature"] = typeof(TemperatureCondition);
            ConditionTypes["afteractivity"] = typeof(AfterActivityCondition);
            ConditionTypes["emotionstate"] = typeof(EmotionStateCondition);
            ConditionTypes["entityvicinity"] = typeof(EntityVicinityCondition);
            ConditionTypes["blockvicinity"] = typeof(BlockVicinityCondition);
            ConditionTypes["positionvicinity"] = typeof(PositionVicinityCondition);
            ConditionTypes["coordinate"] = typeof(CoordinateCondition);
            ConditionTypes["lightlevelCondition"] = typeof(LightLevelCondition);
            ConditionTypes["monthcondition"] = typeof(MonthCondition);
            ConditionTypes["random"] = typeof(RandomCondition);
            ConditionTypes["animation"] = typeof(AnimationCondition);
            ConditionTypes["held"] = typeof(HeldCondition);
            ConditionTypes["dressed"] = typeof(DressedCondition);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.ChatCommands.GetOrCreate("dev")
                .BeginSubCommand("aedit")
                    .RequiresPrivilege(Privilege.controlserver)
                    .BeginSubCommand("debug")
                        .WithDescription("Toggle debug mode")
                        .WithArgs(api.ChatCommands.Parsers.OptionalBool("state"))
                        .HandleWith(args =>
                            {
                                if (!args.Parsers[0].IsMissing)
                                {
                                    Debug = (bool)args[0];
                                }

                                var on = Debug ? "on" : "off";
                                return TextCommandResult.Success($"Activity Debugging: {on}");
                            })
                    .EndSubCommand()
                .EndSubCommand()
                ;
        }
    }
}
