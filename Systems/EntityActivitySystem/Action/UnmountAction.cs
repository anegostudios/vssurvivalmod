using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class UnmountAction : EntityActionBase
    {
        public override string Type => "unmount";

        public UnmountAction() { }

        public UnmountAction(EntityActivitySystem vas)
        {
            this.vas = vas;
        }

        public override bool IsFinished()
        {
            return vas.Entity.MountedOn == null;
        }

        public override void Start(EntityActivity act)
        {
            if (vas.Entity.MountedOn == null) return;

            vas.Entity.TryUnmount();
            ExecutionHasFailed = vas.Entity.MountedOn != null;
        }

        public override IEntityAction Clone()
        {
            return new UnmountAction(vas);
        }

        public override string ToString()
        {
            return "Unmount from block/entity";
        }


        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            return true;
        }
    }
}
