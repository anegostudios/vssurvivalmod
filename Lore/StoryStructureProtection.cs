using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class StoryStructureProtection : ModSystem
    {
        public override bool ShouldLoad(ICoreAPI api) => true;

        ICoreAPI api;
        StoryStructuresSpawnConditions ssys;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            api.Event.OnTestBlockAccess += Event_OnTestBlockAccess;

            ssys = api.ModLoader.GetModSystem<StoryStructuresSpawnConditions>();
        }


        private EnumWorldAccessResponse Event_OnTestBlockAccess(IPlayer player, BlockSelection blockSel, EnumBlockAccessFlags accessType, ref string claimant, EnumWorldAccessResponse response)
        {
            if (accessType == EnumBlockAccessFlags.Use && response == EnumWorldAccessResponse.Granted)
            {
                var ba = api.World.BlockAccessor;

                var struc = ssys.GetStoryStructureAt(blockSel.Position);
                if (struc == null) return response;

                if (struc.Code == "village:game:story/village" || struc.Code == "tobiascave:game:story/tobiascave")
                {
                    // Allow use of doors and trapdoors
                    var block = ba.GetBlock(blockSel.Position);
                    if (block.GetBEBehavior<BEBehaviorDoor>(blockSel.Position) != null || block.GetBEBehavior<BEBehaviorTrapDoor>(blockSel.Position) != null) return response;

                    // Lets not spam the use message everywhere the player right clicks. Anything that is not a block entity is fine to interact wi th
                    var be = ba.GetBlockEntity(blockSel.Position);
                    if (be == null || be is BlockEntityMicroBlock) return response;

                    // Allow use of ruined chests
                    var beCnt = be as BlockEntityGenericTypedContainer;
                    if (beCnt != null && beCnt.retrieveOnly == true) return response;

                    claimant = struc.Code == "tobiascave:game:story/tobiascave"? "custommessage-tobias" : "custommessage-nadiya";
                    return EnumWorldAccessResponse.NoPrivilege;
                }
            }

            return response;
        }

    }
}
