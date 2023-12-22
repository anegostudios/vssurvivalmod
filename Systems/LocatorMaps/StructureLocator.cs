using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class StructureLocation
    {
        public int StructureIndex;
        public int RegionX;
        public int RegionZ;
    }

    public class ModSystemStructureLocator : ModSystem
    {
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return base.ShouldLoad(forSide);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
        }


        public GeneratedStructure GetStructure(StructureLocation loc)
        {
            var reg = sapi.World.BlockAccessor.GetMapRegion(loc.RegionX, loc.RegionZ);
            return reg.GeneratedStructures[loc.StructureIndex];
        }

        public StructureLocation FindFreshStructureLocation(string code, BlockPos nearPos, int searchRange)
        {
            return FindStructureLocation((scode, index, region) => {
                if (scode.Split('/')[0] == code)
                {
                    var locs = region.GetModdata<int[]>("consumedStructureLocations");
                    return locs == null || !locs.Contains(index);
                }
                return false;
            }, nearPos, searchRange);
        }

        public StructureLocation FindStructureLocation(ActionBoolReturn<string, int, IMapRegion> matcher, BlockPos pos, int searchRange)
        {
            int regionSize = sapi.WorldManager.RegionSize;
            int minrx = (pos.X - searchRange) / regionSize;
            int maxrx = (pos.X + searchRange) / regionSize;
            int minrz = (pos.Z - searchRange) / regionSize;
            int maxrz = (pos.Z + searchRange) / regionSize;

            for (int rx = minrx; rx <= maxrx; rx++)
            {
                for (int rz = minrz; rz <= maxrz; rz++)
                {
                    var reg = sapi.World.BlockAccessor.GetMapRegion(rx, rz);
                    if (reg == null) continue;

                    for (int i = 0; i < reg.GeneratedStructures.Count; i++)
                    {
                        var struc = reg.GeneratedStructures[i];

                        if (struc.Location.ShortestDistanceFrom(pos.X, pos.Y, pos.Z) < searchRange && matcher(struc.Code, i, reg))
                        {
                            return new StructureLocation()
                            {
                                StructureIndex = i,
                                RegionX = rx,
                                RegionZ = rz
                            };
                        }
                    }
                }
            }

            return null;
        }


        public bool IsStructureLocationConsumed(StructureLocation strucLoc)
        {
            var reg = sapi.World.BlockAccessor.GetMapRegion(strucLoc.RegionX, strucLoc.RegionZ);
            var locs = reg.GetModdata<int[]>("consumedStructureLocations");

            return locs.Contains(strucLoc.StructureIndex);
        }

        public void ConsumeStructureLocation(StructureLocation strucLoc)
        {
            var reg = sapi.World.BlockAccessor.GetMapRegion(strucLoc.RegionX, strucLoc.RegionZ);
            var locs = reg.GetModdata<int[]>("consumedStructureLocations");
            reg.SetModdata("consumedStructureLocations", locs == null ? new int[] { strucLoc.StructureIndex } : locs.Append(strucLoc.StructureIndex));
        }
    }
}
