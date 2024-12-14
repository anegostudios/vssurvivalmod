using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class UnmountAction : IEntityAction
    {
        protected EntityActivitySystem vas;
        public string Type => "unmount";
        public bool ExecutionHasFailed { get; set; }

        public UnmountAction() { }

        public UnmountAction(EntityActivitySystem vas)
        {
            this.vas = vas;
        }

        public bool IsFinished()
        {
            return vas.Entity.MountedOn == null;
        }

        public void Start(EntityActivity act)
        {
            if (vas.Entity.MountedOn == null) return;

            vas.Entity.TryUnmount();
            ExecutionHasFailed = vas.Entity.MountedOn != null;
        }

        public void OnTick(float dt)
        {

        }
        public void Cancel()
        {
        }

        public void Finish() { }
        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }



        public IEntityAction Clone()
        {
            return new UnmountAction(vas);
        }

        public override string ToString()
        {
            return "Unmount from block/entity";
        }

        public void OnVisualize(ActivityVisualizer visualizer)
        {
            
        }
        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }

        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            return true;
        }
    }
}
