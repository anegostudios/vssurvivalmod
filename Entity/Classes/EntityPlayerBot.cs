using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class EntityPlayerNpc : EntityHumanoid, IEntityNpc
    {
        public string Name;

        public override bool StoreWithChunk
        {
            get { return true; }
        }

        string IEntityNpc.Name
        {
            get
            {
                return Name;
            }
        }

        EntityControls IEntityNpc.Controls
        {
            get
            {
                return controls;
            }
        }

        public EntityPlayerNpc() : base()
        {

        }

        public override void Initialize(IWorldAccessor world, long chunkindex3d)
        {
            base.Initialize(world, chunkindex3d);

            Name = WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name");
        }

        public override void SetName(string playername)
        {
            base.SetName(playername);
            this.Name = playername;
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);
        }


    }
}
