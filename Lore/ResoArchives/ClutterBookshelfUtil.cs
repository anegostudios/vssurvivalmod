using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ClutterBookshelfUtil : ModSystem
    {
        ICoreAPI api;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands
                .GetOrCreate("dev")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSub("bookshelfvariant")
                    .WithArgs(parsers.WorldPosition("block position"), parsers.Word("index or 'random'"))
                    .HandleWith((args) => setBookshelfVariant(args, 1))
                .EndSub()
                .BeginSub("bookshelfvariant2")
                    .WithArgs(parsers.WorldPosition("block position"), parsers.Word("index or 'random'"))
                    .HandleWith((args) => setBookshelfVariant(args, 2))
                .EndSub()
            ;
        }

        private TextCommandResult setBookshelfVariant(TextCommandCallingArgs args, int type)
        {
            Vec3d pos = args[0] as Vec3d;
            int index = (args[1] as string).ToInt(-1);
            var beh = api.World.BlockAccessor.GetBlockEntity(pos.AsBlockPos)?.GetBehavior<BEBehaviorClutterBookshelf>();
            
            if (beh == null) return TextCommandResult.Error("Not looking at a bookshelf");

            var block = beh.Block as BlockClutterBookshelf;

            var vgroup = block.variantGroupsByCode[beh.Variant];
            if (index < 0) index = api.World.Rand.Next(vgroup.typesByCode.Count);
            
            if (vgroup.typesByCode.Count <= index)
            {
                return TextCommandResult.Error("Wrong index");
            }

            if (type == 1)
            {
                beh.Type = vgroup.typesByCode.GetKeyAtIndex(index);
            } else {
                beh.Type2 = vgroup.typesByCode.GetKeyAtIndex(index);
            }
            
            beh.initShape();
            beh.Blockentity.MarkDirty(true);
            
            return TextCommandResult.Success("type " + (type == 2 ? beh.Type : beh.Type2) + " set.");
        }
    }
}
