using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityCoalPile : BlockEntityItemPile, ITexPositionSource, IHeatSource
    {
        static SimpleParticleProperties smokeParticles;
        static SimpleParticleProperties smallMetalSparks;

        static BlockEntityCoalPile()
        {
            smokeParticles = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(150, 40, 40, 40),
                new Vec3d(),
                new Vec3d(1, 0, 1),
                new Vec3f(-1 / 32f, 0.1f, -1 / 32f),
                new Vec3f(1 / 32f, 0.1f, 1 / 32f),
                2f,
                -0.025f / 4,
                0.2f,
                1f,
                EnumParticleModel.Quad
            );

            smokeParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.25f);
            smokeParticles.SelfPropelled = true;
            smokeParticles.AddPos.Set(1, 0, 1);


            smallMetalSparks = new SimpleParticleProperties(
                0.2f, 1,
                ColorUtil.ToRgba(255, 255, 150, 0),
                new Vec3d(), new Vec3d(),
                new Vec3f(-2f, 2f, -2f),
                new Vec3f(2f, 5f, 2f),
                0.04f,
                1f,
                0.2f, 0.25f,
                EnumParticleModel.Cube
            );

            smallMetalSparks.WithTerrainCollision = false;
            smallMetalSparks.VertexFlags = 150;
            smallMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.2f);
            smallMetalSparks.SelfPropelled = true;
        }


        bool burning;
        double burnStartTotalHours;
        ICoreClientAPI capi;
        ILoadedSound ambientSound;

        public override AssetLocation SoundLocation => new AssetLocation("sounds/block/charcoal");
        public override string BlockCode => "coalpile";

        public override int MaxStackSize => 16;
        public override int DefaultTakeQuantity => 2;
        public override int BulkTakeQuantity => 2;


        public int Layers => inventory[0].StackSize / 2;

        float cokeConversionRate = 0f;

        public float BurnHoursPerLayer = 4f;

        public bool IsBurning
        {
            get { return burning; }
        }

        public bool CanIgnite
        {
            get { return !burning; }
        }


        public BlockEntityCoalPile()
        {
            
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            capi = api as ICoreClientAPI;
            updateBurningState();
        }


        public void TryIgnite()
        {
            if (burning) return;

            burning = true;
            burnStartTotalHours = Api.World.Calendar.TotalHours;
            MarkDirty();

            updateBurningState();
        }

        public void Extinguish()
        {
            if (!burning) return;

            burning = false;
            UnregisterGameTickListener(listenerId);
            MarkDirty(true);
            Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, false, 16);
        }


        void updateBurningState()
        {
            if (!burning) return;

            if (Api.World.Side == EnumAppSide.Client)
            {
                if (ambientSound == null || !ambientSound.IsPlaying)
                {
                    ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/effect/embers.ogg"),
                        ShouldLoop = true,
                        Position = Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                        DisposeOnFinish = false,
                        Volume = 1
                    });

                    if (ambientSound != null)
                    {
                        ambientSound.PlaybackPosition = ambientSound.SoundLengthSeconds * (float)Api.World.Rand.NextDouble();
                        ambientSound.Start();
                    }
                }

                listenerId = RegisterGameTickListener(onBurningTickClient, 100);
            } else
            {
                listenerId = RegisterGameTickListener(onBurningTickServer, 10000);
            }
        }

        public static void SpawnBurningCoalParticles(ICoreAPI api, Vec3d pos, float addX = 1f, float addZ=1f)
        {
            smokeParticles.MinQuantity = 0.25f;
            smokeParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -15);
            smokeParticles.AddQuantity = 0;
            smokeParticles.MinPos.Set(pos.X, pos.Y - 0.1f, pos.Z);
            smokeParticles.AddPos.Set(addX, 0, addZ);

            smallMetalSparks.MinPos.Set(pos.X, pos.Y, pos.Z);
            smallMetalSparks.AddPos.Set(addX, 0.1f, addZ);
            api.World.SpawnParticles(smallMetalSparks, null);

            int g = 30 + api.World.Rand.Next(30);
            smokeParticles.Color = ColorUtil.ToRgba(150, g, g, g);
            api.World.SpawnParticles(smokeParticles);
        }

        private void onBurningTickClient(float dt)
        {
            if (burning && Api.World.Rand.NextDouble() < 0.93)
            {
                if (isCokable)
                {
                    smokeParticles.MinQuantity = 1;
                    smokeParticles.AddQuantity = 0;
                    smokeParticles.MinPos.Set(Pos.X, Pos.Y + 2 + 1 / 16f, Pos.Z);
                    int g = 30 + Api.World.Rand.Next(30);
                    smokeParticles.Color = ColorUtil.ToRgba(150, g, g, g);
                    Api.World.SpawnParticles(smokeParticles);

                } else
                {
                    SpawnBurningCoalParticles(Api, Pos.ToVec3d().Add(0, Layers / 8f, 0));
                }
            }
        }

        long listenerId;
        static BlockFacing[] facings = (BlockFacing[])BlockFacing.ALLFACES.Clone();

        public float GetHoursLeft(double startTotalHours)
        {
            double totalHoursPassed = startTotalHours - burnStartTotalHours;
            double burnHourTimeLeft = inventory[0].StackSize / 2 * BurnHoursPerLayer;
            return (float)(burnHourTimeLeft - totalHoursPassed);
        }
        

        private void onBurningTickServer(float dt)
        {
            facings.Shuffle(Api.World.Rand);

            foreach (var val in facings)
            {
                BlockPos npos = Pos.AddCopy(val);
                /*var combprops = Api.World.BlockAccessor.GetBlock(npos).CombustibleProps;
                if (combprops != null)
                {
                    Api.World.BlockAccessor.SetBlock(Api.World.GetBlock(new AssetLocation("fire")).BlockId, npos);
                    BlockEntityFire befire = byEntity.World.BlockAccessor.GetBlockEntity(bpos) as BlockEntityFire;
                    if (befire != null) befire.Init(blockSel.Face, (byEntity as EntityPlayer).PlayerUID);

                    continue;
                }*/

                var becp = Api.World.BlockAccessor.GetBlockEntity(npos) as BlockEntityCoalPile;
                becp?.TryIgnite();

                if (becp != null)
                {
                    if (Api.World.Rand.NextDouble() < 0.75) break;
                }   
            }

            cokeConversionRate = inventory[0].Itemstack.ItemAttributes?["cokeConversionRate"].AsFloat(0) ?? 0;
            if (cokeConversionRate > 0)
            {
                if (isCokable = TestCokable())
                {
                    if (Api.World.Calendar.TotalHours - burnStartTotalHours > 12)
                    {
                        inventory[0].Itemstack = new ItemStack(Api.World.GetItem(new AssetLocation("coke")), (int)(inventory[0].StackSize * cokeConversionRate));
                        burning = false;
                        UnregisterGameTickListener(listenerId);
                        MarkDirty(true);
                    } else
                    {
                        MarkDirty(false);
                    }

                    return;
                }
            }

            bool changed = false;
            while (Api.World.Calendar.TotalHours - burnStartTotalHours > BurnHoursPerLayer)
            {
                burnStartTotalHours += BurnHoursPerLayer;
                inventory[0].TakeOut(2);

                if (inventory[0].Empty)
                {
                    Api.World.BlockAccessor.SetBlock(0, Pos);
                    break;
                } else
                {
                    changed = true;
                }
            }

            if (changed)
            {
                MarkDirty(true);
            }
        }

        bool isCokable;
        private bool TestCokable()
        {
            var bl = Api.World.BlockAccessor;

            bool haveDoor = false;
            foreach (var facing in BlockFacing.HORIZONTALS)
            {
                Block block = bl.GetBlock(Pos.AddCopy(facing));
                haveDoor |= block is BlockCokeOvenDoor && block.Variant["state"] == "closed";
            }

            int centerCount = 0;
            int cornerCount = 0;
            bl.WalkBlocks(Pos.AddCopy(-1, -1, -1), Pos.AddCopy(1, 1, 1), (block, x, y, z) =>
            {
                int dx = Math.Abs(Pos.X - x);
                int dz = Math.Abs(Pos.Z - z);
                bool corner = dx == 1 && dz == 1;

                bool viable = block.Attributes?["cokeOvenViable"].AsBool(true) == true;
                if (viable)
                {
                    centerCount += !corner ? 1 : 0;
                    cornerCount += corner ? 1 : 0;
                }
            });

            // bottom: 5 center, 4 corner
            // mid: 3 center, 1 door, 4 corner
            // top: 5 center, 4 corner
            // 13 center, 12 corner. Allow 4 corner blocks to be missing

            return haveDoor && centerCount >= 12 && cornerCount >= 8 && bl.GetBlock(Pos.UpCopy()).Attributes?["cokeOvenViable"].AsBool(true) == true;
        }

        public override bool OnPlayerInteract(IPlayer byPlayer)
        {
            if (burning && !byPlayer.Entity.Controls.ShiftKey) return false;

            bool ok = base.OnPlayerInteract(byPlayer);

            TriggerPileChanged();

            return ok;
        }


        void TriggerPileChanged()
        {
            if (Api.Side != EnumAppSide.Server) return;

            int maxSteepness = 4;

            BlockCoalPile belowcoalpile = Api.World.BlockAccessor.GetBlock(Pos.DownCopy()) as BlockCoalPile;
            int belowwlayers = belowcoalpile == null ? 0 : belowcoalpile.GetLayercount(Api.World, Pos.DownCopy());

            foreach (var face in BlockFacing.HORIZONTALS)
            {
                BlockPos npos = Pos.AddCopy(face);
                Block nblock = Api.World.BlockAccessor.GetBlock(npos);
                BlockCoalPile nblockcoalpile = Api.World.BlockAccessor.GetBlock(npos) as BlockCoalPile;
                int nblockcoalpilelayers = nblockcoalpile == null ? 0 : nblockcoalpile.GetLayercount(Api.World, npos);

                // When should it collapse?
                // When there layers > 3 and nearby is air or replacable
                // When nearby is coal and herelayers - neiblayers > 3
                // When there is coal below us, the neighbour below us is coal, nearby is air or replaceable, and owncoal+belowcoal - neibbelowcoal > 3

                int layerdiff = Math.Max(nblock.Replaceable > 6000 ? Math.Max(0, Layers - maxSteepness) : 0, (nblockcoalpile != null ? Layers - nblockcoalpilelayers - maxSteepness : 0));

                if (belowwlayers > 0)
                {
                    BlockCoalPile nbelowblockcoalpile = Api.World.BlockAccessor.GetBlock(npos.DownCopy()) as BlockCoalPile;
                    int nbelowwlayers = nbelowblockcoalpile == null ? 0 : nbelowblockcoalpile.GetLayercount(Api.World, npos.DownCopy());
                    layerdiff = Math.Max(layerdiff, (nbelowblockcoalpile != null ? Layers + belowwlayers - nbelowwlayers - maxSteepness : 0));
                }

                if (Api.World.Rand.NextDouble() < layerdiff / (float)maxSteepness)
                {
                    if (TryPartialCollapse(npos.UpCopy(), 2)) return;
                }
            }
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            bool wasBurning = burning;

            burning = tree.GetBool("burning");
            burnStartTotalHours = tree.GetDouble("lastTickTotalHours");
            isCokable = tree.GetBool("isCokable");

            if (!burning)
            {
                if (listenerId != 0)
                {
                    UnregisterGameTickListener(listenerId);
                }
                ambientSound?.Stop();
                listenerId = 0;
            }

            if (Api != null && Api.Side == EnumAppSide.Client && !wasBurning && burning)
            {
                updateBurningState();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("burning", burning);
            tree.SetDouble("lastTickTotalHours", burnStartTotalHours);
            tree.SetBool("isCokable", isCokable);
        }


        public bool MergeWith(TreeAttribute blockEntityAttributes)
        {
            InventoryGeneric otherinv = new InventoryGeneric(1, BlockCode, null, null, null);
            otherinv.FromTreeAttributes(blockEntityAttributes.GetTreeAttribute("inventory"));
            otherinv.Api = Api;
            otherinv.ResolveBlocksOrItems();

            if (!inventory[0].Empty && otherinv[0].Itemstack.Equals(Api.World, inventory[0].Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                int quantityToMove = Math.Min(otherinv[0].StackSize, Math.Max(0, MaxStackSize - inventory[0].StackSize));
                inventory[0].Itemstack.StackSize += quantityToMove;

                otherinv[0].TakeOut(quantityToMove);
                if (otherinv[0].StackSize > 0)
                {
                    BlockPos uppos = Pos.UpCopy();
                    Block upblock = Api.World.BlockAccessor.GetBlock(uppos);
                    if (upblock.Replaceable > 6000)
                    {
                        ((IBlockItemPile)Block).Construct(otherinv[0], Api.World, uppos, null);
                    }
                }

                MarkDirty(true);
                TriggerPileChanged();
            }

            return true;
        }


        private bool TryPartialCollapse(BlockPos pos, int quantity)
        {
            if (inventory[0].Empty) return false;

            IWorldAccessor world = Api.World;

            if (world.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = (world as IServerWorldAccessor).Api as ICoreServerAPI;
                if (!sapi.Server.Config.AllowFallingBlocks) return false;
            }

            if (IsReplacableBeneath(world, pos) || IsReplacableBeneathAndSideways(world, pos))
            {
                // Prevents duplication
                Entity entity = world.GetNearestEntity(pos.ToVec3d().Add(0.5, 0.5, 0.5), 1, 1.5f, (e) =>
                {
                    return e is EntityBlockFalling && ((EntityBlockFalling)e).initialPos.Equals(pos);
                });

                if (entity == null)
                {
                    int prevstacksize = inventory[0].StackSize;

                    inventory[0].Itemstack.StackSize = quantity;
                    EntityBlockFalling entityblock = new EntityBlockFalling(Block, this, pos, null, 1, true, 0.05f);
                    entityblock.DoRemoveBlock = false; // We want to split the pile, not remove it 
                    world.SpawnEntity(entityblock);
                    entityblock.ServerPos.Y -= 0.25f;
                    entityblock.Pos.Y -= 0.25f;

                    inventory[0].Itemstack.StackSize = prevstacksize - quantity;
                    return true;
                }
            }

            
            return false;
        }


        private bool IsReplacableBeneathAndSideways(IWorldAccessor world, BlockPos pos)
        {
            for (int i = 0; i < 4; i++)
            {
                BlockFacing facing = BlockFacing.HORIZONTALS[i];

                Block nBlock = world.BlockAccessor.GetBlockOrNull(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y, pos.Z + facing.Normali.Z);
                Block nBBlock = world.BlockAccessor.GetBlockOrNull(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y - 1, pos.Z + facing.Normali.Z);

                if (nBlock != null && nBBlock != null && nBlock.Replaceable >= 6000 && nBBlock.Replaceable >= 6000)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsReplacableBeneath(IWorldAccessor world, BlockPos pos)
        {
            Block bottomBlock = world.BlockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            return (bottomBlock != null && bottomBlock.Replaceable > 6000);
        }


        public void GetDecalMesh(ITexPositionSource decalTexSource, out MeshData meshdata)
        {
            int size = Layers * 2;

            Shape shape = capi.TesselatorManager.GetCachedShape(new AssetLocation("block/basic/layers/" + GameMath.Clamp(size, 2, 16) + "voxel"));
            capi.Tesselator.TesselateShape("coalpile", shape, out meshdata, decalTexSource);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            lock (inventoryLock)
            {
                if (!inventory[0].Empty)
                {
                    int size = Layers * 2;
                    if (mesher is EntityBlockFallingRenderer) size = 2; // Haxy solution >.>

                    Shape shape = capi.TesselatorManager.GetCachedShape(new AssetLocation("block/basic/layers/" + GameMath.Clamp(size, 2, 16) + "voxel"));
                    MeshData meshdata;
                    capi.Tesselator.TesselateShape("coalpile", shape, out meshdata, this);

                    if (burning)
                    {
                        for (int i = 0; i < meshdata.FlagsCount; i++)
                        {
                            meshdata.Flags[i] |= 196; // glow level
                        }
                    }

                    mesher.AddMeshData(meshdata);
                }
            }

            return true;
        }




        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            ambientSound?.Dispose();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if (!burning)
            {
                base.OnBlockBroken(byPlayer);
            }

            ambientSound?.Dispose();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            ambientSound?.Dispose();
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (IsBurning)
                {
                    return capi.BlockTextureAtlas.Positions[capi.World.GetBlock(new AssetLocation("ember")).FirstTextureInventory.Baked.TextureSubId];
                }

                string itemcode = inventory[0].Itemstack.Collectible.Code.Path;
                return capi.BlockTextureAtlas.Positions[Block.Textures[itemcode].Baked.TextureSubId];
            }
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            /*if (!inventory[0].Empty)
            {
                dsc.AppendLine(string.Format("{0}x {1}", inventory[0].StackSize, inventory[0].Itemstack.GetName())) ;
            }*/
        }

        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return IsBurning && !isCokable ? 10 : 0;
        }
    }
}
