using Vintagestory.API.Common;
using Vintagestory.API.Server;

#nullable disable

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
                // If in dim2 (e.g. Devastation Tower) do not use this system, as there may be no MapRegion loaded
                if (blockSel.Position.dimension != 0)
                    return response; 

                var struc = ssys.GetStoryStructureAt(blockSel.Position);
                if (struc == null) return response;

                // if we are in a story structure now do check for land claims in it
                var landClaims = api.World.Claims.Get(blockSel.Position);
                if (landClaims.Length == 0) return response;

                // if we are inside a land claim of a story struc check what we can do or not
                if (struc.Code == "village:game:story/village" || struc.Code == "tobiascave:game:story/tobiascave")
                {
                    // Disallow yoinking of armor stand armor
                    if (player.CurrentEntitySelection != null && player.CurrentEntitySelection.Entity is EntityArmorStand)
                    {
                        claimant = struc.Code == "tobiascave:game:story/tobiascave" ? "custommessage-tobias" : "custommessage-nadiya";
                        return EnumWorldAccessResponse.NoPrivilege;
                    }

                    // Allow use of doors and trapdoors
                    var ba = api.World.BlockAccessor;
                    var block = ba.GetBlock(blockSel.Position);
                    if (block.GetBEBehavior<BEBehaviorDoor>(blockSel.Position) != null ||
                        block.GetBEBehavior<BEBehaviorTrapDoor>(blockSel.Position) != null) return response;

                    // Lets not spam the use message everywhere the player right clicks. Anything that is not a block entity is fine to interact with
                    var be = ba.GetBlockEntity(blockSel.Position);
                    if (be == null || be is BlockEntityMicroBlock) return response;

                    // Allow use of ruined chests and per player chests
                    if (be is BlockEntityGenericTypedContainer beCnt && (beCnt.retrieveOnly || beCnt.isPerPlayer))
                        return response;

                    if (player.WorldData.CurrentGameMode == EnumGameMode.Creative && player.HasPrivilege(Privilege.useblockseverywhere))
                        return response;

                    claimant = struc.Code == "tobiascave:game:story/tobiascave" ? "custommessage-tobias" : "custommessage-nadiya";

                    return EnumWorldAccessResponse.NoPrivilege;
                }
            }

            if (accessType == EnumBlockAccessFlags.BuildOrBreak)
            {
                var struc = ssys.GetStoryStructureAt(blockSel.Position);
                if (struc == null) return response;

                // if we are in a story structure now do check for land claims in it
                var landClaims = api.World.Claims.Get(blockSel.Position);
                if (landClaims.Length == 0) return response;

                if (player.WorldData.CurrentGameMode == EnumGameMode.Creative && player.HasPrivilege(Privilege.buildblockseverywhere))
                    return response;

                claimant = struc.Code == "tobiascave" ? "custommessage-tobias" : "custommessage-nadiya";
                return EnumWorldAccessResponse.NoPrivilege;
            }

            return response;
        }
    }
}