using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class StoryStructuresSpawnConditions : ModSystem
    {
        ICoreServerAPI sapi;
        ICoreAPI api;

        Cuboidi[] structureLocations;

        List<GeneratedStructure> storyStructuresClient = new List<GeneratedStructure>();

        public override bool ShouldLoad(EnumAppSide forSide) => true;


        public override void Start(ICoreAPI api)
        {
            api.ModLoader.GetModSystem<SystemTemporalStability>().OnGetTemporalStability += ResoArchivesSpawnConditions_OnGetTemporalStability;
            this.api = api;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            structureLocations = new Cuboidi[0];
            api.Event.MapRegionLoaded += Event_MapRegionLoaded;
            api.Event.MapRegionUnloaded += Event_MapRegionUnloaded;
        }

        private void Event_MapRegionUnloaded(Vec2i mapCoord, IMapRegion region)
        {
            foreach (var val in region.GeneratedStructures)
            {
                if (val.Group == "storystructure")
                {
                    storyStructuresClient.Remove(val);
                }
            }

            structureLocations = storyStructuresClient.Select(val => val.Location).ToArray();
        }

        private void Event_MapRegionLoaded(Vec2i mapCoord, IMapRegion region)
        {
            foreach (var val in region.GeneratedStructures)
            {
                if (val.Group == "storystructure")
                {
                    storyStructuresClient.Add(val);
                }
            }

            structureLocations = storyStructuresClient.Select(val => val.Location).ToArray();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.sapi = api;

            sapi.Event.OnTrySpawnEntity += Event_OnTrySpawnEntity;
        }


        Vec3d tmpPos = new Vec3d();
        private float ResoArchivesSpawnConditions_OnGetTemporalStability(float stability, double x, double y, double z)
        {
            if (isInStoryStructure(tmpPos.Set(x,y,z)))
            {
                return 1f;
            }

            return stability;
        }


        private bool Event_OnTrySpawnEntity(IBlockAccessor blockAccessor, ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {
            if (properties.Server.SpawnConditions?.Runtime == null) return true;

            // Don't spawn hostile mobs in the reso archives. We have custom spawning in there.
            if (properties.Server.SpawnConditions.Runtime.Group == "hostile" && isInStoryStructure(spawnPosition))
            {
                return false;
            }


            return true;
        }

        private void loadLocations()
        {
            if (sapi == null) return;

            var structureGen = sapi.ModLoader.GetModSystem<GenStoryStructures>();
            if (structureGen == null) return;
            List<Cuboidi> locations = new List<Cuboidi>();
            foreach (var val in structureGen.storyStructureInstances.Values)
            {
                locations.Add(val.Location);
            }
            this.structureLocations = locations.ToArray();
        }

        private bool isInStoryStructure(Vec3d position)
        {
            if (structureLocations == null)
            {
                loadLocations();
            }
            if (structureLocations == null) return false;

            for (int i = 0; i < structureLocations.Length; i++)
            {
                var loc = structureLocations[i];
                if (loc.Contains(position)) return true;
            }

            return false;
        }

        public GeneratedStructure GetStoryStructureAt(BlockPos pos)
        {
            var mapregion = api.World.BlockAccessor.GetMapRegion(pos.X / api.World.BlockAccessor.RegionSize, pos.Z / api.World.BlockAccessor.RegionSize);

            for (int i = 0; i < mapregion.GeneratedStructures.Count; i++)
            {
                var struc = mapregion.GeneratedStructures[i];
                if (struc.Location != null && struc?.Group == "storystructure")
                {
                    if (struc.Location.Contains(pos)) return struc;
                }
            }

            return null;
        }
    }
}
