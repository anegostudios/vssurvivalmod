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
        public string RockTypeRemapGroup = null; // For rocktyped ruins
        [JsonProperty]
        public Dictionary<AssetLocation, AssetLocation> RockTypeRemaps = null;  // For rocktyped ruins
        [JsonProperty]
        public AssetLocation[] InsideBlockCodes;
        [JsonProperty]
        public EnumOrigin Origin = EnumOrigin.StartPos;
        [JsonProperty]
        public Dictionary<string, int> OffsetYByCode;

        protected T[][] LoadSchematicsWithRotations<T>(ICoreAPI api, AssetLocation[] locs, BlockLayerConfig config, Dictionary<string, int> schematicYOffsets, int? defaultOffsetY, string pathPrefix = "schematics/") where T : BlockSchematicStructure
        {
            List<T[]> schematics = new List<T[]>();

            for (int i = 0; i < locs.Length; i++)
            {
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
                    int offsety = getOffsetY(schematicYOffsets, defaultOffsetY, OffsetYByCode, assets[j]);
                    var sch = LoadSchematic<T>(api, assets[j], config, offsety);
                    if (sch != null) schematics.Add(sch);
                }
            }

            return schematics.ToArray();
        }

        public static int getOffsetY(Dictionary<string, int> schematicYOffsets, int? defaultOffsetY, Dictionary<string, int> OffsetYByCode, IAsset asset)
        {
            var assloc = asset.Location.Path.Substring("worldgen/schematics/".Length).Replace(".json", "");
            int offsety = 0;
            if (OffsetYByCode != null && OffsetYByCode.TryGetValue(assloc, out offsety)) { }
            else if (defaultOffsetY != null)
            {
                offsety = (int)defaultOffsetY;
            }
            else if (schematicYOffsets != null && schematicYOffsets.TryGetValue(assloc, out offsety)) { }

            return offsety;
        }

        public static T[] LoadSchematic<T>(ICoreAPI api, IAsset asset, BlockLayerConfig config, int offsety) where T : BlockSchematicStructure
        {
            T schematic = asset.ToObject<T>();

            if (schematic == null)
            {
                api.World.Logger.Warning("Could not load schematic {0}", asset.Location);
                return null;
            }
            
            schematic.OffsetY = offsety;
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

            return rotations;
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
