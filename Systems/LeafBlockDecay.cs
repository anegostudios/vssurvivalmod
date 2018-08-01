using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Responsible for breaking leaves when a support block is not connected nearby. 
    /// Listens for "checkLeafDecay" events published on the event bus and checks neighbor
    /// locations for whether they should decay.
    /// 
    /// Algorithm
    /// 
    /// First we initialize a 9x9x9 grid with 127. We put 0's for blocks that support leaves
    /// and -1 for leaves. The block we are checking is centered in the grid.
    /// See figure below for a figure of the 2D version in it's initial state. 
    /// 
    /// We will update the grid with multiple passes. 
    /// Iteration 1:
    ///    Look at 0's neighbors. If they are -1 update them to 1. 
    /// Iteration 2:
    ///    Look at 1's neighbors. If they are -1 update them to 2.
    /// Iteration 3:
    ///    Look at 2's neighbors. If they are -1 update them to 3.
    /// Iteration 4:
    ///    Look at 3's neighbors. If they are -1 update them to 4.
    ///    
    /// Finally, we check the value centered in the grid. If it is still -1 then we know that it wasn't close
    /// enough to a supporting block because it was never updated. 
    /// 
    /// |127|127|127|127|-1 |127|127|127|127|
    /// |127|127|127|-1 |-1 |-1 |127|127|127|
    /// |127|127|127|-1 |-1 |-1 |127|127|127|
    /// |127|127|-1 |-1 |-1 |-1 |-1 |127|127|
    /// |127|127|127|-1 |-1 |-1 |127|127|127|
    /// |127|127|127|127| 0 |127|127|127|127|
    /// |127|127|127|127| 0 |127|127|127|127|
    /// |127|127|127|127| 0 |127|127|127|127|
    /// |127|127|127|127| 0 |127|127|127|127|
    /// </summary>
    public class LeafBlockDecay : ModSystem
    {
        private static int MILLIS_TICK_INTERVAL = 50;
        private static string EVENT_NAME = "checkLeafDecay";

        private ICoreServerAPI sapi;
        private HashSet<BlockPos> checkDecayQueue = new HashSet<BlockPos>();
        public static object checkDecayLock = new object();

        private HashSet<BlockPos> performDecayQueue = new HashSet<BlockPos>();
        public static object performDecayLock = new object();

        private CheckDecayThread checkDecayThread;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            api.Event.SaveGameLoaded(onSaveGameLoaded);
            api.Event.GameWorldSave(onGameGettingSaved);
            //api.Event.RegisterEventBusListener(onCheckLeafDecay, 0.5, EVENT_NAME);
            //api.Event.RegisterGameTickListener(performDecay, MILLIS_TICK_INTERVAL);
        }

        private void performDecay(float dt)
        {
            BlockPos pos = null;
            lock (performDecayLock)
            {
                if(performDecayQueue.Count > 0)
                {
                    pos = performDecayQueue.First<BlockPos>();
                    performDecayQueue.Remove(pos);
                }       
            }
            if (pos != null)
            {
                decay(pos);
            }
        }

        private void onCheckLeafDecay(string eventName, ref EnumHandling handling, IAttribute data)
        {
            if (checkDecayThread != null)
            {
                TreeAttribute tree = data as TreeAttribute;
                BlockPos pos = new BlockPos(tree.GetInt("x"), tree.GetInt("y"), tree.GetInt("z"));
                queueNeighborsForCheckDecay(pos);
            }
        }

        private void queueNeighborsForCheckDecay(BlockPos pos)
        {
            tryEnqueueFaces(pos, BlockFacing.ALLFACES);
            tryEnqueueFaces(pos.AddCopy(0, 1, 0), BlockFacing.HORIZONTALS);
            tryEnqueueFaces(pos.AddCopy(0, -1, 0), BlockFacing.HORIZONTALS);
        }

        private void tryEnqueueFaces(BlockPos pos, BlockFacing[] faces)
        {
            foreach (BlockFacing facing in faces)
            {
                tryEnqueue(pos.AddCopy(facing));
            }
        }

        private void tryEnqueue(BlockPos pos)
        {
            lock(checkDecayLock)
            {
                checkDecayQueue.Add(pos);
            }
        }

        private void decay(BlockPos pos)
        {
            Block block = sapi.World.BlockAccessor.GetBlock(pos);
            if(canDecay(block))//In case a non leaf block has been placed recently
            {
                sapi.World.BlockAccessor.BreakBlock(pos, null);
            }
            queueNeighborsForCheckDecay(pos);
        }

        private void onGameGettingSaved()
        {
            sapi.WorldManager.StoreData("checkDecayQueue", SerializerUtil.Serialize(checkDecayQueue));
            sapi.WorldManager.StoreData("performDecayQueue", SerializerUtil.Serialize(performDecayQueue));
        }

        private void onSaveGameLoaded()
        {
            checkDecayQueue = deserializeQueue("checkDecayQueue");
            performDecayQueue = deserializeQueue("performDecayQueue");
            checkDecayThread = new CheckDecayThread(sapi);
            checkDecayThread.Start(checkDecayQueue, performDecayQueue);
        }

        private HashSet<BlockPos> deserializeQueue(string name)
        {
            HashSet<BlockPos> result = new HashSet<BlockPos>();
            try
            {
                byte[] data = sapi.WorldManager.GetData(name);
                if(data != null)
                {
                    foreach (BlockPos pos in SerializerUtil.Deserialize<HashSet<BlockPos>>(data))
                    {
                        result.Add(pos);
                    }
                }
            }
            catch (Exception e)
            {
                sapi.World.Logger.Error("Failed loading LeafBlockDecay.{0}. Resetting. Exception: {1}", name, e);
            }
            return result;
        }

        private static bool canDecay(Block block)
        {
            if (block.BlockMaterial == EnumBlockMaterial.Leaves)
            {
                JsonObject canDecay = block.Attributes?["canDecay"];
                return canDecay != null && canDecay.AsBool();
            }
            return false;
        }

        private static bool supportsLeaves(Block block)
        {
            JsonObject supportsLeaves = block.Attributes?["supportsLeaves"];
            return supportsLeaves != null && supportsLeaves.AsBool();
        }

        private class CheckDecayThread
        {
            private static int MILLIS_BETWEEN_CHECKS = 25;

            private static int MAX_DIST = 6;
            private static int CHECK_CUBE_SIZE = 2 * MAX_DIST + 1;
            private static int BLOCK_MAP_SIZE = (CHECK_CUBE_SIZE + 2) * (CHECK_CUBE_SIZE + 2) * (CHECK_CUBE_SIZE + 2);

            private sbyte[] blockMap = new sbyte[BLOCK_MAP_SIZE];
            public bool Stopping { get;  set; }
            private HashSet<BlockPos> checkDecay;
            private HashSet<BlockPos> performDecay;
            private ICoreServerAPI sapi;

            public CheckDecayThread(ICoreServerAPI sapi)
            {
                this.sapi = sapi;
            }

            public void Start(HashSet<BlockPos> checkDecay, HashSet<BlockPos> performDecay)
            {
                this.checkDecay = checkDecay;
                this.performDecay = performDecay;

                Thread thread = new Thread(new ThreadStart(() =>
                {
                    while (!sapi.Server.IsShuttingDown && !Stopping)
                    {
                        processCheckDecayQueue();
                        Thread.Sleep(MILLIS_BETWEEN_CHECKS);
                    }
                }));
                thread.Name = "CheckLeafDecay";
                thread.IsBackground = true;
                thread.Start();     
            }

            private void processCheckDecayQueue()
            {
                BlockPos pos = null;
                lock(checkDecayLock)
                {
                    if(checkDecay.Count > 0)
                    {
                        pos = checkDecay.First<BlockPos>();
                        checkDecay.Remove(pos);
                    }
                }
                if(pos != null)
                {
                    if(shouldDecay(pos))
                    {
                        lock(performDecayLock)
                        {
                            performDecay.Add(pos);
                        }
                    }
                }
            }

            private bool shouldDecay(BlockPos pos)
            {
                // Build a box to map neighboring blocks
                for (int x = 0; x < CHECK_CUBE_SIZE; x++)
                {
                    for (int z = 0; z < CHECK_CUBE_SIZE; z++)
                    {
                        for (int y = 0; y < CHECK_CUBE_SIZE; y++)
                        {
                            Block block = sapi.World.BlockAccessor.GetBlock(pos.AddCopy(x - MAX_DIST, y - MAX_DIST, z - MAX_DIST));
                            sbyte val = 127;
                            if (supportsLeaves(block))
                            {
                                val = 0;
                            }
                            else if (canDecay(block))
                            {
                                val = -1;
                            }
                            setBlockInMap(val, x, y, z);
                        }
                    }
                }

                // browse the map in several pass to detect connected leaves:
                // leaf block that is MAX_DIST blocks away from log or without connection
                // to another connected leaves block will decay
                for (int i = 0; i < MAX_DIST; i++)
                {
                    for (int x = 0; x < CHECK_CUBE_SIZE; x++)
                    {
                        for (int z = 0; z < CHECK_CUBE_SIZE; z++)
                        {
                            for (int y = 0; y < CHECK_CUBE_SIZE; y++)
                            {
                                if (getBlockInMap(x, y, z) != i)
                                {
                                    continue;
                                }
                                if (getBlockInMap(x - 1, y, z) == -1)
                                {
                                    setBlockInMap((sbyte)(i + 1), x - 1, y, z);
                                }
                                if (getBlockInMap(x, y - 1, z) == -1)
                                {
                                    setBlockInMap((sbyte)(i + 1), x, y - 1, z);
                                }
                                if (getBlockInMap(x, y, z - 1) == -1)
                                {
                                    setBlockInMap((sbyte)(i + 1), x, y, z - 1);
                                }
                                if (getBlockInMap(x + 1, y, z) == -1)
                                {
                                    setBlockInMap((sbyte)(i + 1), x + 1, y, z);
                                }
                                if (getBlockInMap(x, y + 1, z) == -1)
                                {
                                    setBlockInMap((sbyte)(i + 1), x, y + 1, z);
                                }
                                if (getBlockInMap(x, y, z + 1) == -1)
                                {
                                    setBlockInMap((sbyte)(i + 1), x, y, z + 1);
                                }
                            }
                        }
                    }
                }

                //Get this block's map value. If less than zero then it wasn't connected
                return (getBlockInMap(MAX_DIST, MAX_DIST, MAX_DIST) < 0);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private sbyte getBlockInMap(int x, int y, int z)
            {
                int index = ((x + 1) * 11 + z + 1) * 11 + y + 1;
                return blockMap[index];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void setBlockInMap(sbyte val, int x, int y, int z)
            {
                blockMap[((x + 1) * 11 + z + 1) * 11 + y + 1] = val;
            }
        }
    }
}
