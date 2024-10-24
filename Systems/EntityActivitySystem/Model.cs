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

    public abstract class EntityActionBase : IEntityAction
    {
        protected EntityActivitySystem vas;

        public abstract string Type { get; }
        public virtual void Start(EntityActivity entityActivity) { }
        public virtual void Cancel() { }
        public virtual void Finish() { }
        public virtual bool IsFinished() { return true; }
        public virtual void OnLoaded(EntityActivitySystem vas) {
            this.vas = vas;
        }
        public virtual bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer) { return true; }
        public virtual void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer) { }
        public abstract IEntityAction Clone();

        public virtual void OnTick(float dt) { }
        public virtual void StoreState(ITreeAttribute tree) { }
        public virtual void LoadState(ITreeAttribute tree) { }
        public virtual bool ExecutionHasFailed { get; set; }
        public virtual void OnHurt(DamageSource dmgSource, float damage) { }
        public virtual void OnVisualize(ActivityVisualizer visualizer) { }

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
        void OnHurt(DamageSource dmgSource, float damage);
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
        string Code { get; set; }
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
