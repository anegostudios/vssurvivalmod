using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Entity that represents a falling block like sand. When spawned it sets an air block at the initial 
    /// position. When it hits the ground it despawns and sets the original block back at that location.
    /// </summary>
    public class EntityBlockFalling : Entity
    {
        private int ticksAlive;
        int lingerTicks;

        public bool InitialBlockRemoved;

        private AssetLocation blockCode;
        public BlockPos initialPos;
        private ItemStack[] drops;
        public TreeAttribute blockEntityAttributes;
        public string blockEntityClass;

        BlockEntity be;

        public EntityBlockFalling() { }

        public EntityBlockFalling (Block block, BlockEntity blockEntity, BlockPos initialPos)
        {
            this.blockCode = block.Code;
            this.be = blockEntity;
            this.initialPos = initialPos;

            ServerPos.SetPos(initialPos);
            ServerPos.X += 0.5;
            ServerPos.Z += 0.5;

            Pos.SetFrom(ServerPos);
        }


        public override void Initialize(IWorldAccessor world, long InChunkIndex3d)
        {
            if (be != null)
            {
                this.blockEntityAttributes = new TreeAttribute();
                be.ToTreeAttributes(blockEntityAttributes);
                blockEntityClass = world.ClassRegistry.GetBlockEntityClass(be.GetType());
                be = null;
            }

            SetType(world.GetEntityType(GlobalConstants.EntityBlockFallingTypeCode));

            TrackingRange = 3 * world.BlockAccessor.ChunkSize;
            
            base.Initialize(world, InChunkIndex3d);

            // Need to capture this now before we remove the block and start to fall
            drops = Block.GetDrops(world, initialPos, null);
        }

        /// <summary>
        /// Delays behaviors from ticking to reduce flickering
        /// </summary>
        /// <param name="dt"></param>
        public override void OnGameTick(float dt)
        {
            if (lingerTicks > 0)
            {
                lingerTicks--;
                if (lingerTicks == 0) Die();
                return;
            }


            ticksAlive++;
            if (ticksAlive >= 7)
            {
                if (!InitialBlockRemoved)
                {
                    UpdateBlock(true, initialPos);
                    InitialBlockRemoved = true;
                }

                foreach (EntityBehavior behavior in Behaviors)
                {
                    behavior.OnGameTick(dt);
                }
            }

        }

        private void UpdateBlock(bool remove, BlockPos pos)
        {
            if (remove)
            {
                World.BlockAccessor.SetBlock(0, pos);
            } else
            {
                World.BlockAccessor.SetBlock(Block.BlockId, pos);
                if (blockEntityAttributes != null)
                {
                    BlockEntity be = World.BlockAccessor.GetBlockEntity(pos);

                    blockEntityAttributes.SetInt("posx", pos.X);
                    blockEntityAttributes.SetInt("posy", pos.Y);
                    blockEntityAttributes.SetInt("posz", pos.Z);

                    if (be != null)
                    {
                        be.FromTreeAtributes(blockEntityAttributes, World);
                    }
                }
            }
            
            NotifyNeighborsOfBlockChange(pos);
        }

        private void NotifyNeighborsOfBlockChange(BlockPos pos)
        {
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                BlockPos npos = pos.AddCopy(facing);
                Block neib = World.BlockAccessor.GetBlock(npos);
                neib.OnNeighourBlockChange(World, npos, pos);
            }
        }

        public override void OnFallToGround(double motionY)
        {
            Block block = World.BlockAccessor.GetBlock(blockCode);
            World.SpawnBlockVoxelParticles(LocalPos.XYZ, block, CollisionBox.X2 - CollisionBox.X1, 10);

            if (World is IServerWorldAccessor)
            {
                BlockPos finalPos = ServerPos.AsBlockPos;
                Block blockAtFinalPos = World.BlockAccessor.GetBlock(finalPos);

                if (IsReplaceableBlock(finalPos))
                {
                    UpdateBlock(false, finalPos);
                }
                else
                {
                    // Space is occupied by maybe a torch or some other block we shouldn't replace
                    DropItems(finalPos);
                }
            }

            lingerTicks = 2;
        }

        private bool IsReplaceableBlock(BlockPos pos)
        {
            Block blockAtFinalPos = World.BlockAccessor.GetBlock(pos);
            
            return (blockAtFinalPos != null && (blockAtFinalPos.IsReplacableBy(Block)));
        }

        private void DropItems(BlockPos pos)
        {
            if (drops != null)
            {
                for (int i = 0; i < drops.Length; i++)
                {
                    World.SpawnItemEntity(drops[i], pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }
        }

        /// <summary>
        /// The Block that is falling
        /// </summary>
        public Block Block
        {
            get { return World.BlockAccessor.GetBlock(blockCode); }
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);

            writer.Write(initialPos.X);
            writer.Write(initialPos.Y);
            writer.Write(initialPos.Z);
            writer.Write(blockCode.ToShortString());
            writer.Write(blockEntityAttributes == null);

            if (blockEntityAttributes != null)
            {
                blockEntityAttributes.ToBytes(writer);
                writer.Write(blockEntityClass);
            }
        }

        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            initialPos = new BlockPos();
            initialPos.X = reader.ReadInt32();
            initialPos.Y = reader.ReadInt32();
            initialPos.Z = reader.ReadInt32();
            blockCode = new AssetLocation(reader.ReadString());

            bool beIsNull = reader.ReadBoolean();
            if (!beIsNull)
            {
                blockEntityAttributes = new TreeAttribute();
                blockEntityAttributes.FromBytes(reader);
                blockEntityClass = reader.ReadString();
            }
        }

        public override bool ShouldReceiveDamage(DamageSource damageSource, float damage)
        {
            return false;
        }

        public override bool IsInteractable
        {
            get { return false; }
        }
        
    }
}