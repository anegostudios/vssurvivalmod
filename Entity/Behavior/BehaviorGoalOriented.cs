using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class BehaviorGoalOriented : EntityBehavior
    {
        ITreeAttribute goaltree;

        internal float Aggressivness
        {
            get { return goaltree.GetFloat("aggressivness"); }
        }

        

        public BehaviorGoalOriented(Entity entity) : base(entity)
        {
            goaltree = entity.WatchedAttributes.GetTreeAttribute("goaltree");
        }

        public override string PropertyName() { return "goaloriented"; }


    }
}
