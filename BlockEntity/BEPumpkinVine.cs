using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityPumpkinVine : BlockEntity
    {
        #region Config
        /// <summary>
        /// Number of hours for a pumpkin to advance to it's next stage
        /// </summary>
        public static readonly float pumpkinHoursToGrow = 12;

        /// <summary>
        /// Number of hours for a vine to advance to it's next stage
        /// </summary>
        public static readonly float vineHoursToGrow = 12;

        /// <summary>
        /// Number of hours for a vine to advance from stage 2 to stage 3
        /// </summary>
        public static readonly float vineHoursToGrowStage2 = 6;

        /// <summary>
        /// Probability that the vine will bloom once it gets to stage 3
        /// </summary>
        public static readonly float bloomProbability = 0.5f;

        /// <summary>
        /// Probability that the vine will return to a normal stage 3 vine once it has bloomed
        /// </summary>
        public static readonly float debloomProbability = 0.5f;

        /// <summary>
        /// Probability that a an attempt to spawn a new vine will happen at stage 2
        /// </summary>
        public static readonly float vineSpawnProbability = 0.5f;

        /// <summary>
        /// Probability that a new vine will spawn in the preferred growth direction which is away from it's parent
        /// </summary>
        public static readonly float preferredGrowthDirProbability = 0.75f;

        /// <summary>
        /// Maximum number of tries allowed to spawn pumpkins
        /// </summary>
        public static readonly int maxAllowedPumpkinGrowthTries = 3;
        #endregion

        // Temporary data
        public long growListenerId;
        public Block stage1VineBlock;
        public Block pumpkinBlock;


        // Permanent (stored) data

        /// <summary>
        /// Total game hours when it can enter the next growth stage
        /// </summary>
        public double totalHoursForNextStage;

        /// <summary>
        /// If true then the vine is allowed to bloom. The vine only gets one chance during stage 3 to bloom
        /// </summary>
        public bool canBloom;

        /// <summary>
        /// Current number of times the vine has tried to grow a pumpkin
        /// </summary>
        public int pumpkinGrowthTries;

        /// <summary>
        /// Keeps up with when each surrounding pumpkin can advance to the next stage
        /// </summary>
        public Dictionary<BlockFacing, double> pumpkinTotalHoursForNextStage = new Dictionary<BlockFacing, double>();

        /// <summary>
        /// Position of the plant that spawned this vine.
        /// </summary>
        public BlockPos parentPlantPos;

        /// <summary>
        /// Favored direction of growth for new vines
        /// </summary>
        public BlockFacing preferredGrowthDir;


        public int internalStage = 0;





        public BlockEntityPumpkinVine() : base()
        {
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                pumpkinTotalHoursForNextStage.Add(facing, 0);
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            stage1VineBlock = api.World.GetBlock(new AssetLocation("pumpkin-vine-1-normal"));
            pumpkinBlock = api.World.GetBlock(new AssetLocation("pumpkin-fruit-1"));

            if (api is ICoreServerAPI)
            {
                growListenerId = RegisterGameTickListener(TryGrow, 2000);
            }
        }

        
        public void CreatedFromParent(BlockPos parentPlantPos, BlockFacing preferredGrowthDir, double currentTotalHours)
        {
            totalHoursForNextStage = currentTotalHours + vineHoursToGrow;
            this.parentPlantPos = parentPlantPos;
            this.preferredGrowthDir = preferredGrowthDir;
        }

        private void TryGrow(float dt)
        {
            if (DieIfParentDead()) return;

            while (api.World.Calendar.TotalHours > totalHoursForNextStage)
            {
                GrowVine();
                totalHoursForNextStage += vineHoursToGrow;
            }

            TryGrowPumpkins();
        }
        

        private void TryGrowPumpkins()
        {
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                double pumpkinTotalHours = pumpkinTotalHoursForNextStage[facing];
                while (pumpkinTotalHours > 0 && api.World.Calendar.TotalHours > pumpkinTotalHours)
                {
                    BlockPos pumpkinPos = pos.AddCopy(facing);
                    Block pumpkin = api.World.BlockAccessor.GetBlock(pumpkinPos);

                    if (IsPumpkin(pumpkin))
                    {
                        int currentStage = CurrentPumpkinStage(pumpkin);
                        if (currentStage == 4)
                        {
                            //Stop growing
                            pumpkinTotalHours = 0;
                        }
                        else
                        {
                            SetPumpkinStage(pumpkin, pumpkinPos, currentStage + 1);
                            pumpkinTotalHours += pumpkinHoursToGrow;
                        }
                    }
                    else
                    {
                        pumpkinTotalHours = 0;
                    }
                    pumpkinTotalHoursForNextStage[facing] = pumpkinTotalHours;
                }
            }
        }

        void GrowVine()
        {
            internalStage++;

            Block block = api.World.BlockAccessor.GetBlock(pos);

            int currentStage = CurrentVineStage(block);

            if (internalStage > 6)
            {
                SetVineStage(block, currentStage + 1);
            }

            if (IsBlooming())
            {
                if (pumpkinGrowthTries >= maxAllowedPumpkinGrowthTries || api.World.Rand.NextDouble() < debloomProbability)
                {
                    pumpkinGrowthTries = 0;

                    SetVineStage(block, 3);
                }
                else
                {
                    pumpkinGrowthTries++;

                    TrySpawnPumpkin(totalHoursForNextStage - vineHoursToGrow);
                }
            }

            if (currentStage == 3)
            {
                if(canBloom && api.World.Rand.NextDouble() < bloomProbability)
                {
                    SetBloomingStage(block);
                }
                canBloom = false;
            }

            if (currentStage == 2)
            {
                if (api.World.Rand.NextDouble() < vineSpawnProbability)
                {
                    TrySpawnNewVine();
                }

                totalHoursForNextStage += vineHoursToGrowStage2;
                canBloom = true;
                SetVineStage(block, currentStage + 1);
            }

            if (currentStage < 2)
            {
                SetVineStage(block, currentStage + 1);
            }
        }

        private void TrySpawnPumpkin(double curTotalHours)
        {
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                BlockPos candidatePos = pos.AddCopy(facing);
                Block block = api.World.BlockAccessor.GetBlock(candidatePos);
                if (!CanReplace(block)) continue;
                
                Block supportBlock = api.World.BlockAccessor.GetBlock(candidatePos.DownCopy());
                if (CanSupportPumpkin(supportBlock))
                {
                    api.World.BlockAccessor.SetBlock(pumpkinBlock.BlockId, candidatePos);
                    pumpkinTotalHoursForNextStage[facing] = curTotalHours + pumpkinHoursToGrow;
                    return;
                }
                
            }
        }

        private bool IsPumpkin(Block block)
        {
            if (block != null)
            {
                string code = block.Code.GetName();
                return code.StartsWith("pumpkin-fruit");
            }
            return false;
        }

        private bool DieIfParentDead()
        {
            if (parentPlantPos == null)//Can happen if someone places a pumpkin mother plan on farmland in creative mode(I think...)
            {
                Die();
                return true;
            }
            else
            {
                Block parentBlock = api.World.BlockAccessor.GetBlock(parentPlantPos);
                if (!IsValidParentBlock(parentBlock) && api.World.BlockAccessor.GetChunkAtBlockPos(parentPlantPos.X, parentPlantPos.Y, parentPlantPos.Z) != null)
                {
                    Die();
                    return true;
                }
            }
            return false;
        }

        private void Die()
        {
            api.Event.UnregisterGameTickListener(growListenerId);
            growListenerId = 0;
            api.World.BlockAccessor.SetBlock(0, pos);
        }

        private bool IsValidParentBlock(Block parentBlock)
        {
            if (parentBlock != null)
            {
                string blockCode = parentBlock.Code.GetName();
                if (blockCode.StartsWith("crop-pumpkin") || blockCode.StartsWith("pumpkin-vine"))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsBlooming()
        {
            Block block = api.World.BlockAccessor.GetBlock(pos);
            string lastCodePart = block.LastCodePart();
            return block.LastCodePart() == "blooming";
        }

        private bool CanReplace(Block block)
        {
            return block == null || (block.Replaceable >= 6000 && !block.Code.GetName().Contains("pumpkin"));
        }

        private bool CanSupportPumpkin(Block underBlock)
        {
            return underBlock != null && !underBlock.IsLiquid() && underBlock.Replaceable <= 5000;
        }

        private void SetVineStage(Block block, int toStage)
        {
            ReplaceSelf(block.CodeWithParts("" + toStage, toStage == 4 ? "withered" : "normal"));
        }

        private void SetPumpkinStage(Block pumpkinBlock, BlockPos pumpkinPos, int toStage)
        {
            Block nextBlock = api.World.GetBlock(pumpkinBlock.CodeWithParts("" + toStage));
            if (nextBlock == null) return;
            api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, pumpkinPos);
        }

        private void SetBloomingStage(Block block)
        {
            ReplaceSelf(block.CodeWithParts("blooming"));
        }
        

        private void ReplaceSelf(AssetLocation blockCode)
        {
            Block nextBlock = api.World.GetBlock(blockCode);
            if (nextBlock == null) return;
            api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, pos);
        }

        private void TrySpawnNewVine()
        {
            BlockFacing spawnDir = GetVineSpawnDirection();
            BlockPos newVinePos = pos.AddCopy(spawnDir);
            Block blockToReplace = api.World.BlockAccessor.GetBlock(newVinePos);

            if (!IsReplaceable(blockToReplace)) return;
            
            Block supportBlock = api.World.BlockAccessor.GetBlock(newVinePos.DownCopy());
            if (!CanGrowOn(supportBlock)) return;

            api.World.BlockAccessor.SetBlock(stage1VineBlock.BlockId, newVinePos);

            BlockEntityPumpkinVine be = api.World.BlockAccessor.GetBlockEntity(newVinePos) as BlockEntityPumpkinVine;
            if (be != null)
            {
                be.CreatedFromParent(pos, spawnDir, totalHoursForNextStage);
            }
        }
        

        private bool CanGrowOn(Block block)
        {
            return block != null && !block.IsLiquid() && block.SideSolid[BlockFacing.UP.Index];
        }

        private bool IsReplaceable(Block block)
        {
            return block == null || block.Replaceable >= 6000;
        }

        private BlockFacing GetVineSpawnDirection()
        {
            if(api.World.Rand.NextDouble() < preferredGrowthDirProbability)
            {
                return preferredGrowthDir;
            }
            else
            {
                return DirectionAdjacentToPreferred();
            }
        }

        private BlockFacing DirectionAdjacentToPreferred()
        {
            if (BlockFacing.NORTH == preferredGrowthDir || BlockFacing.SOUTH == preferredGrowthDir)
            {
                return api.World.Rand.NextDouble() < 0.5 ? BlockFacing.EAST : BlockFacing.WEST;
            }
            else
            {
                return api.World.Rand.NextDouble() < 0.5 ? BlockFacing.NORTH : BlockFacing.SOUTH;
            }
        }

        private int CurrentVineStage(Block block)
        {
            int stage = 0;
            int.TryParse(block.LastCodePart(1), out stage);
            return stage;
        }

        private int CurrentPumpkinStage(Block block)
        {
            int stage = 0;
            int.TryParse(block.LastCodePart(0), out stage);
            return stage;
        }




        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            totalHoursForNextStage = tree.GetDouble("totalHoursForNextStage");
            canBloom = tree.GetInt("canBloom") > 0;

            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                pumpkinTotalHoursForNextStage[facing] = tree.GetDouble(facing.Code);
            }
            pumpkinGrowthTries = tree.GetInt("pumpkinGrowthTries");

            parentPlantPos = new BlockPos(tree.GetInt("parentPlantPosX"), tree.GetInt("parentPlantPosY"), tree.GetInt("parentPlantPosZ"));
            preferredGrowthDir = BlockFacing.ALLFACES[tree.GetInt("preferredGrowthDir")];
            internalStage = tree.GetInt("internalStage");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("totalHoursForNextStage", totalHoursForNextStage);
            tree.SetInt("canBloom", canBloom ? 1 : 0);

            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                tree.SetDouble(facing.Code, pumpkinTotalHoursForNextStage[facing]);
            }
            tree.SetInt("pumpkinGrowthTries", pumpkinGrowthTries);

            tree.SetInt("parentPlantPosX", parentPlantPos.X);
            tree.SetInt("parentPlantPosY", parentPlantPos.Y);
            tree.SetInt("parentPlantPosZ", parentPlantPos.Z);
            tree.SetInt("preferredGrowthDir", preferredGrowthDir.Index);
            tree.SetInt("internalStage", internalStage);
        }


    }
}