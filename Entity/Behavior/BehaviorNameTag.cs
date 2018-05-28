using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    class EntityBehaviorNameTag : EntityBehavior
    {
        public string DisplayName
        {
            get { return entity.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name"); }
        }

        public bool ShowOnlyWhenTargeted
        {
            get { return entity.WatchedAttributes.GetTreeAttribute("nametag")?.GetInt("showtagonlywhentargeted") > 0; }
        }

        public EntityBehaviorNameTag(Entity entity) : base(entity)
        {
            ITreeAttribute nametagTree = entity.WatchedAttributes.GetTreeAttribute("nametag");
            if (nametagTree == null)
            {
                entity.WatchedAttributes.SetAttribute("nametag", nametagTree = new TreeAttribute());
                nametagTree.SetString("name", "");
                nametagTree.SetInt("showtagonlywhentargeted", 0);
                entity.WatchedAttributes.MarkPathDirty("nametag");
            }
        }
        
        public override void OnSetEntityName(string playername)
        {
            ITreeAttribute nametagTree = entity.WatchedAttributes.GetTreeAttribute("nametag");
            if (nametagTree == null)
            {
                entity.WatchedAttributes.SetAttribute("nametag", nametagTree = new TreeAttribute());
            }

            nametagTree.SetString("name", playername);
            entity.WatchedAttributes.MarkPathDirty("nametag");
        }

        public override string PropertyName()
        {
            return "displayname";
        }
    }
}
