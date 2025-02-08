using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class StructureLocation
    {
        public int StructureIndex=-1;
        public int RegionX;
        public int RegionZ;
        public Vec3i Position { get; set; }
    }

    public class ModSystemStructureLocator : ModSystem
    {
        ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
        }

        public GeneratedStructure GetStructure(StructureLocation loc)
        {
            var reg = sapi.World.BlockAccessor.GetMapRegion(loc.RegionX, loc.RegionZ);
            GeneratedStructure structure = null;
            if (loc.Position != null)
            {
                structure = reg?.GeneratedStructures.Find(s => 
                    s.Location.X1 == loc.Position.X && 
                    s.Location.Y1 == loc.Position.Y && 
                    s.Location.Z1 == loc.Position.Z);
            }
            else if (loc.StructureIndex >= 0 && loc.StructureIndex < reg?.GeneratedStructures.Count)
            {
                structure = reg.GeneratedStructures[loc.StructureIndex];
            }

            return structure;
        }

        public StructureLocation FindFreshStructureLocation(string code, BlockPos nearPos, int searchRange)
        {
            return FindStructureLocation((struc, index, region) => {
                if (struc.Code.Split('/')[0] == code)
                {
                    var locs = region.GetModdata<int[]>("consumedStructureLocations");
                    var locPos = region.GetModdata<List<Vec3i>>("consumedStrucLocPos");
                    var notUsed = !(locs != null && locs.Contains(index));
                    if(locPos != null && locPos.Contains(struc.Location.Start)) notUsed = false;
                    return notUsed;
                }
                return false;
            }, nearPos, searchRange);
        }

        public StructureLocation FindStructureLocation(ActionBoolReturn<GeneratedStructure, int, IMapRegion> matcher, BlockPos pos, int searchRange)
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

                        if (struc.Location.ShortestDistanceFrom(pos.X, pos.Y, pos.Z) < searchRange && matcher(struc, i, reg))
                        {
                            return new StructureLocation()
                            {
                                Position = struc.Location.Start,
                                RegionX = rx,
                                RegionZ = rz
                            };
                        }
                    }
                }
            }

            return null;
        }

        public void ConsumeStructureLocation(StructureLocation strucLoc)
        {
            var reg = sapi.World.BlockAccessor.GetMapRegion(strucLoc.RegionX, strucLoc.RegionZ);
            var locs = reg.GetModdata<List<Vec3i>>("consumedStrucLocPos") ?? new List<Vec3i>();
            locs.Add(strucLoc.Position);
            reg.SetModdata("consumedStrucLocPos", locs);
        }
    }
}
