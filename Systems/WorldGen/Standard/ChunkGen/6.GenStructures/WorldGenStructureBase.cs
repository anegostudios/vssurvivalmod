using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods
{
    public abstract class WorldGenStructureBase
    {
        [JsonProperty]
        public string Code;
        [JsonProperty]
        public string Name;
        [JsonProperty]
        public AssetLocation[] Schematics;
        [JsonProperty]
        public EnumStructurePlacement Placement = EnumStructurePlacement.SurfaceRuin;
        [JsonProperty]
        public NatFloat Depth = null;
        [JsonProperty]
        public NatFloat Quantity = NatFloat.createGauss(7, 7);
        [JsonProperty]
        public bool BuildProtected = false;
        [JsonProperty]
        public string BuildProtectionDesc = null;
        [JsonProperty]
        public string BuildProtectionName = null;
        [JsonProperty]
        public string RockTypeRemapGroup = null;
        [JsonProperty]
        public Dictionary<AssetLocation, AssetLocation> RockTypeRemaps = null;
        [JsonProperty]
        public AssetLocation[] InsideBlockCodes;
        [JsonProperty]
        public EnumOrigin Origin = EnumOrigin.StartPos;

        

        protected T[][] LoadSchematicsWithRotations<T>(ICoreAPI api, AssetLocation[] locs, BlockLayerConfig config, string pathPrefix = "schematics/") where T : BlockSchematicStructure
        {
            List<T[]> schematics = new List<T[]>();

            for (int i = 0; i < locs.Length; i++)
            {
                string error = "";
                IAsset[] assets;

                var schematicLoc = Schematics[i];

                if (locs[i].Path.EndsWith("*"))
                {
                    assets = api.Assets.GetManyInCategory("worldgen", pathPrefix + schematicLoc.Path.Substring(0, schematicLoc.Path.Length - 1), schematicLoc.Domain).ToArray();
                }
                else
                {
                    assets = new IAsset[] { api.Assets.Get(schematicLoc.Clone().WithPathPrefixOnce("worldgen/" + pathPrefix).WithPathAppendixOnce(".json")) };
                }

                for (int j = 0; j < assets.Length; j++)
                {
                    IAsset asset = assets[j];

                    T schematic = asset.ToObject<T>();


                    if (schematic == null)
                    {
                        api.World.Logger.Warning("Could not load {0}: {1}", Schematics[i], error);
                        continue;
                    }

                    schematic.FromFileName = asset.Name;

                    T[] rotations = new T[4];
                    rotations[0] = schematic;

                    for (int k = 0; k < 4; k++)
                    {
                        if (k > 0)
                        {
                            rotations[k] = rotations[0].ClonePacked() as T;
                            rotations[k].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, k * 90);
                        }

                        rotations[k].blockLayerConfig = config;
                        rotations[k].Init(api.World.BlockAccessor);
                        rotations[k].LoadMetaInformationAndValidate(api.World.BlockAccessor, api.World, asset.Location.ToShortString());
                    }

                    schematics.Add(rotations);
                }
            }

            return schematics.ToArray();
        }






        protected T[] LoadSchematics<T>(ICoreAPI api, AssetLocation[] locs, BlockLayerConfig config, string pathPrefix = "schematics/") where T : BlockSchematicStructure
        {
            List<T> schematics = new List<T>();

            for (int i = 0; i < locs.Length; i++)
            {
                string error = "";
                IAsset[] assets;

                var schematicLoc = Schematics[i];

                if (locs[i].Path.EndsWith("*"))
                {
                    assets = api.Assets.GetManyInCategory("worldgen", pathPrefix + schematicLoc.Path.Substring(0, schematicLoc.Path.Length - 1), schematicLoc.Domain).ToArray();
                }
                else
                {
                    assets = new IAsset[] { api.Assets.Get(schematicLoc.Clone().WithPathPrefixOnce("worldgen/" + pathPrefix).WithPathAppendixOnce(".json")) };
                }

                for (int j = 0; j < assets.Length; j++)
                {
                    IAsset asset = assets[j];

                    T schematic = asset.ToObject<T>();


                    if (schematic == null)
                    {
                        api.World.Logger.Warning("Could not load {0}: {1}", Schematics[i], error);
                        continue;
                    }

                    schematic.FromFileName = asset.Name;
                    schematics.Add(schematic);
                }
            }

            return schematics.ToArray();
        }

    }
}
