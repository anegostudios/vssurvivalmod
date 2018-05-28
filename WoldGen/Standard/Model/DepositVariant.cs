using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using System.Linq;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DepositVariant : WorldPropertyVariant
    {
        [JsonProperty]
        public new string Code;

        /// <summary>
        /// Block to be placed, use {parentblocktype} to inherit the parent block properties
        /// </summary>
        [JsonProperty]
        public AssetLocation BlockCode;

        [JsonProperty]
        public AssetLocation[] BlockCodes;

        /// <summary>
        /// List of blocks in which this deposit may appear in. Append a "*" to any block code to add any blocks beginning with that code, e.g. "rock-*"
        /// </summary>
        [JsonProperty]
        public AssetLocation[] ParentBlockCodes;

        // BelowSurface     - Follows heightmap, depth determines how many blocks below the surfaces (0 = 0 blocks below surface, 1 = 1 block below surface, ...)
        // Static           - Don't follow heightmap, depth determines y-coordinate                  (0 = map bottom, ..., 1 = map top)
        // BelowSealLevel   - Don't follow heightmap, depth determines y-coordinate below sealevel   (0 = map bottom, ..., 1 = sealevel)
        // InDeposit        - Generate inside another deposit, depth value is not used
        [JsonProperty]
        public EnumDepositPlacement Placement;

        /// <summary>
        /// Radius in blocks, capped 64 blocks
        /// </summary>
        [JsonProperty]
        public NatFloat Radius;

        /// <summary>
        /// Thickness in blocks
        /// </summary>
        [JsonProperty]
        public NatFloat Thickness;

        /// <summary>
        /// for Placement=FollowSurfaceBelow depth is absolute blocks below surface
        /// for Placement=FollowSurface depth in percent. 0 = bottom, 1=surface at current pos
        /// for Placement=Straight depth in percent. 0 = bottom, 1=surface at current pos
        /// for Placement=Anywhere depth in percent. 0 = bottom, 1=map height
        /// for Placement=FollowSeaLevel depth in percent. 0 = bottom, 1=sealevel
        /// </summary>
        [JsonProperty]
        public NatFloat Depth;

        /// <summary>
        /// Amount of deposits per serverchunk-column 
        /// </summary>
        [JsonProperty]
        public float Quantity;

        [JsonProperty]
        public float MinTemp = -30;

        [JsonProperty]
        public float MaxTemp = 30;

        [JsonProperty]
        public float MinRain;

        [JsonProperty]
        public float MaxRain = 1;

        [JsonProperty]
        public float MaxYRoughness = 10;

        [JsonProperty]
        public float MaxY = 1;


        [JsonProperty]
        public bool CheckClimate;

        [JsonProperty]
        public bool WithBlockCallback;

        [JsonProperty]
        public bool WithOreMap;

        [JsonProperty]
        public DepositVariant[] ChildDeposits;

        [JsonProperty]
        public AssetLocation SurfaceBlockCode = null;

        [JsonProperty]
        public float SurfaceBlockChance = 0.05f;

        // Resolved values
        public ushort[] ParentBlockIds; // Parent material
        public ushort[] BlockIds; // Deposit blocks
        public ushort[] SurfaceBlockIds; // Surface Deposit blocks

        public AssetLocation[] GrandParentBlockLocs = new AssetLocation[0];
        public MapLayerBase OreMap;

        public void Init(ICoreServerAPI api)
        {
            ResolveBlockCodes(api);

            if (Placement == EnumDepositPlacement.Anywhere)
            {
                Depth.avg *= api.WorldManager.MapSizeY;
                Depth.var *= api.WorldManager.MapSizeY;
            }

            if (Placement == EnumDepositPlacement.FollowSeaLevel)
            {
                Depth.avg *= TerraGenConfig.seaLevel;
                Depth.var *= TerraGenConfig.seaLevel;
            }

            if (CheckClimate && Radius.avg + Radius.var >= 32)
            {
                api.Server.LogWarning("Deposit has CheckClimate=true and radius > 32 blocks - this is not supported, sorry. Defaulting to uniform radius 10");
                Radius = NatFloat.createUniform(10, 0);
            }
            
            if (BlockIds.Length == 0)
            {
                string code = BlockCode.Path.Replace("{", "{{").Replace("}", "}}");
                throw new Exception("Invalid deposits.json: Can't have a deposit without blockcode (maybe invalid name? " + code  + ")!");
            }

            if (ChildDeposits != null)
            {
                for (int i = 0; i < ChildDeposits.Length; i++)
                {
                    if (ChildDeposits[i].ParentBlockCodes == null)
                    {
                        ChildDeposits[i].ParentBlockCodes = new AssetLocation[BlockIds.Length];
                        for (int j = 0; j < BlockIds.Length; j++)
                        {
                            ChildDeposits[i].ParentBlockCodes[j] = api.World.Blocks[BlockIds[j]].Code;
                        }

                        
                        ChildDeposits[i].GrandParentBlockLocs = new AssetLocation[ParentBlockCodes.Length];
                        for (int j = 0; j < ParentBlockCodes.Length; j++)
                        {
                            ChildDeposits[i].GrandParentBlockLocs[j] = ParentBlockCodes[j];
                        }
                        
                    }
                    ChildDeposits[i].Init(api);
                }
            } else
            {
                ChildDeposits = new DepositVariant[0];
            }
        }


        void ResolveBlockCodes(ICoreServerAPI api)
        {
            List<ushort> parentBlockIds = new List<ushort>();
            List<ushort> blockIds = new List<ushort>();

            bool popSurfaceBlockIds = false;


            if (SurfaceBlockCode != null)
            {
                if (SurfaceBlockCode.Path.Contains("{parentblocktype}"))
                {
                    List<ushort> notused = new List<ushort>();
                    List<ushort> surfaceblockIds = new List<ushort>();
                    ResolveBlockCodesWithParentPlaceHolder(api, SurfaceBlockCode, notused, surfaceblockIds);
                    SurfaceBlockIds = surfaceblockIds.ToArray();
                } else
                {
                    popSurfaceBlockIds = true;
                }
            }

            if (BlockCodes != null)
            {
                for (int i = 0; i < BlockCodes.Length; i++)
                {
                    blockIds.Add(api.WorldManager.GetBlockId(BlockCodes[i]));
                    parentBlockIds.Add(api.WorldManager.GetBlockId(ParentBlockCodes[i]));
                }

                ParentBlockIds = parentBlockIds.ToArray();
                BlockIds = blockIds.ToArray();

                if (popSurfaceBlockIds) SurfaceBlockIds = Enumerable.Repeat(api.World.GetBlock(SurfaceBlockCode).BlockId, BlockIds.Length).ToArray();

                return;
            }

            if (BlockCode.Path.Contains("{parentblocktype}"))
            {
                ResolveBlockCodesWithParentPlaceHolder(api, BlockCode, parentBlockIds, blockIds);
                ParentBlockIds = parentBlockIds.ToArray();
                BlockIds = blockIds.ToArray();

                if (popSurfaceBlockIds) SurfaceBlockIds = Enumerable.Repeat(api.World.GetBlock(SurfaceBlockCode).BlockId, BlockIds.Length).ToArray();

                return;
            }

            if (BlockCode.Path.Contains("{grandparentblocktype}"))
            {
                ResolveBlockCodesWithGrandParentPlaceHolder(api, parentBlockIds, blockIds);
                ParentBlockIds = parentBlockIds.ToArray();
                BlockIds = blockIds.ToArray();

                if (popSurfaceBlockIds) SurfaceBlockIds = Enumerable.Repeat(api.World.GetBlock(SurfaceBlockCode).BlockId, BlockIds.Length).ToArray();

                return;
            }
            
            for (int j = 0; j < ParentBlockCodes.Length; j++)
            {
                AddBlock(api, parentBlockIds, ParentBlockCodes[j]);
                AddBlock(api, blockIds, BlockCode);
            }
            
            ParentBlockIds = parentBlockIds.ToArray();
            BlockIds = blockIds.ToArray();

            if (popSurfaceBlockIds) SurfaceBlockIds = Enumerable.Repeat(api.World.GetBlock(SurfaceBlockCode).BlockId, BlockIds.Length).ToArray();
        }

        private void ResolveBlockCodesWithGrandParentPlaceHolder(ICoreServerAPI api, List<ushort> parentBlockIds, List<ushort> blockIds)
        {
            Block[] grandParentBlocks = api.WorldManager.SearchBlockTypes(GrandParentBlockLocs[0].Path.Substring(0, GrandParentBlockLocs[0].Path.Length - 1));

            for (int i = 0; i < ParentBlockCodes.Length; i++)
            {
                string grandparentblockcode = grandParentBlocks[i].Code.Path.Substring(GrandParentBlockLocs[0].Path.Length - 1);
                AddBlock(api, parentBlockIds, ParentBlockCodes[i]);
                AddBlock(api, blockIds, BlockCode.CopyWithPath(BlockCode.Path.Replace("{grandparentblocktype}", grandparentblockcode)));
            }
            int missingIds = parentBlockIds.Count - blockIds.Count;
            for (int i = 0; i < missingIds; i++)
            {
                blockIds.Add(blockIds[0]);
            }
        }


        private void ResolveBlockCodesWithParentPlaceHolder(ICoreServerAPI api, AssetLocation forBlockCode, List<ushort> parentBlockIds, List<ushort> blockIds)
        {
            bool haveWildcard = false;

            for (int j = 0; j < ParentBlockCodes.Length; j++)
            {
                AssetLocation parentLocation = ParentBlockCodes[j];

                if (parentLocation.Path.EndsWith("*"))
                {
                    haveWildcard = true;
                    Block[] parentBlocks = api.WorldManager.SearchBlockTypes(parentLocation.Path.Substring(0, parentLocation.Path.Length - 1));

                    for (int i = 0; i < parentBlocks.Length; i++)
                    {
                        parentBlockIds.Add(parentBlocks[i].BlockId);
                        string parentblockcode = parentBlocks[i].Code.Path.Substring(parentLocation.Path.Length - 1);
                        AddBlock(api, blockIds, forBlockCode.CopyWithPath(forBlockCode.Path.Replace("{parentblocktype}", parentblockcode)));
                    }
                }
            }

            if (!haveWildcard)
            {
                for (int j = 0; j < ParentBlockCodes.Length; j++)
                {
                    AssetLocation parentLocation = ParentBlockCodes[j];

                    Block parentBlock = api.World.GetBlock(parentLocation);

                    parentBlockIds.Add(parentBlock.BlockId);
                    string parentblockcode = parentBlock.CodeEndWithoutParts(1);
                    AddBlock(api, blockIds, forBlockCode.CopyWithPath(forBlockCode.Path.Replace("{parentblocktype}", parentblockcode)));
                }
            }


            int missingIds = parentBlockIds.Count - blockIds.Count;
            for (int i = 0; i < missingIds; i++)
            {
                blockIds.Add(blockIds[0]);
            }
        }

        void AddBlock(ICoreServerAPI api, List<ushort> blockIds, AssetLocation code)
        {
            int id = api.WorldManager.GetBlockId(code);
            if (id == 0 && code.Path != "air")
            {
                api.Server.LogWarning("Deposit Variant Resolver: Block with code {0} not found.", code);
            }
            else
            {
                blockIds.Add((ushort)id);
            }
        }
    }
}
