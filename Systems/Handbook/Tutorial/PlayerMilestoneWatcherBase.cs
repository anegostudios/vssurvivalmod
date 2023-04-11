using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    


    public enum EnumReceiveType
    {
        Collect, Craft, Knap, Clayform, Grab
    }


    public abstract class PlayerMilestoneWatcherBase
    {
        protected ICoreAPI api;

        public string Code;

        public virtual void Init(ICoreAPI api)
        {
            this.api = api;
        }

        public virtual void OnItemStackReceived(ItemStack stack, string eventName)
        {
            
        }

        public virtual void OnBlockPlaced(BlockPos pos, Block block, ItemStack withStackInHands)
        {

        }

        public virtual void OnBlockLookedAt(BlockSelection blockSel)
        {

        }

        public virtual void FromJson(JsonObject job)
        {

        }

        public virtual void ToJson(JsonObject job)
        {

        }
    }

    public abstract class PlayerMilestoneWatcherGeneric : PlayerMilestoneWatcherBase
    {
        public int QuantityGoal;
        public int QuantityAchieved = 0;
        public bool Dirty;

        public bool MilestoneReached()
        {
            return QuantityAchieved >= QuantityGoal;
        }

        public override void ToJson(JsonObject job)
        {
            if (QuantityAchieved > 0)
            {
                job.Token["achieved"] = new JValue(QuantityAchieved);
            }
        }

        public override void FromJson(JsonObject job)
        {
            QuantityAchieved = job["achieved"].AsInt();
        }

        public void Skip()
        {
            Dirty = true;
            QuantityAchieved = QuantityGoal;
        }
        public void Restart()
        {
            Dirty = true;
            QuantityAchieved = 0;
        }
    }
}