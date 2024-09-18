using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public interface IStorableTypedComponent
    {
        void OnLoaded(EntityActivitySystem vas);
        void StoreState(ITreeAttribute tree);
        void LoadState(ITreeAttribute tree);
        string Type { get; }
    }

    public interface IEntityAction : IStorableTypedComponent
    {
        bool ExecutionHasFailed { get; }
        void Start(EntityActivity entityActivity);
        void Cancel();
        void Finish();
        void OnTick(float dt);
        bool IsFinished();
        void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer);
        IEntityAction Clone();
        bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer);
        void OnVisualize(ActivityVisualizer visualizer);
    }

    public interface IActionCondition : IStorableTypedComponent
    {
        bool Invert { get; set; }
        void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer);
        IActionCondition Clone();
        bool ConditionSatisfied(Entity e);
        void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer);
    }

    public interface IEntityActivity
    {
        double Priority { get; }
        int Slot { get; set; }
        string Name { get; set; }
        IActionCondition[] Conditions { get; }
        IEntityAction[] Actions { get; }
        IEntityAction CurrentAction { get; }
        EnumConditionLogicOp ConditionsOp { get; set; }
        bool Finished { get; }

        void StoreState(ITreeAttribute tree);
        /// <summary>
        /// Abort this activity
        /// </summary>
        void Cancel();
        /// <summary>
        /// Called when Finished is true
        /// </summary>
        void Finish();
        /// <summary>
        /// Start this activity
        /// </summary>
        void Start();
        void OnTick(float dt);
    }

}
