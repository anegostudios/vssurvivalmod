using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent

{
    public class BlockGroundStorage : Block
    {
        ItemStack[] groundStorablesQuadrants;
        ItemStack[] groundStorablesHalves;

        public static bool IsUsingContainedBlock; // This value is only relevant (and correct) client side

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            ItemStack[][] stacks = ObjectCacheUtil.GetOrCreate(api, "groundStorablesQuadrands", () =>
            {
                List<ItemStack> qstacks = new List<ItemStack>();
                List<ItemStack> hstacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    var storableBh = obj.GetBehavior<CollectibleBehaviorGroundStorable>();
                    if (storableBh?.StorageProps.Layout == EnumGroundStorageLayout.Quadrants)
                    {
                        qstacks.Add(new ItemStack(obj));
                    }
                    if (storableBh?.StorageProps.Layout == EnumGroundStorageLayout.Halves)
                    {
                        hstacks.Add(new ItemStack(obj));
                    }
                }

                return new ItemStack[][] { qstacks.ToArray(), hstacks.ToArray() };
            });

            groundStorablesQuadrants = stacks[0];
            groundStorablesHalves = stacks[1];

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                capi.Event.MouseUp += Event_MouseUp;
            }

        }

        private void Event_MouseUp(MouseEvent e)
        {
            IsUsingContainedBlock = false;
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                return beg.GetCollisionBoxes();
            }

            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                return beg.GetCollisionBoxes();
            }

            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var be = blockAccessor.GetBlockEntity<BlockEntityGroundStorage>(pos);
            if (be != null)
            {
                return be.GetSelectionBoxes();
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            var be = blockAccessor.GetBlockEntity<BlockEntityGroundStorage>(pos);
            if (be != null)
            {
                return be.CanAttachBlockAt(blockFace, attachmentArea);
            }
            return base.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (api.Side == EnumAppSide.Client && IsUsingContainedBlock) return false;

            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityGroundStorage beg) 
            { 
                return beg.OnPlayerInteractStart(byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityGroundStorage beg)
            {
                return beg.OnPlayerInteractStep(secondsUsed, byPlayer, blockSel);
            }

            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityGroundStorage beg)
            {
                beg.OnPlayerInteractStop(secondsUsed, byPlayer, blockSel);
                return;
            }

            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

        public override EnumBlockMaterial GetBlockMaterial(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            return base.GetBlockMaterial(blockAccessor, pos, stack);
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                List<ItemStack> stacks = new List<ItemStack>();
                foreach (var slot in beg.Inventory)
                {
                    if (slot.Empty) continue;
                    stacks.Add(slot.Itemstack);
                }

                return stacks.ToArray();
            }

            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public float FillLevel(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                return (int)Math.Ceiling((float)beg.TotalStackSize / beg.Capacity);
            }

            return 1;
        }



        public bool CreateStorage(IWorldAccessor world, BlockSelection blockSel, IPlayer player)
        {
            if (!world.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            BlockPos pos;
            if (blockSel.Face == null)
            {
                pos = blockSel.Position;
            } else
            {
                pos = blockSel.Position.AddCopy(blockSel.Face);
            }
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.CanAttachBlockAt(world.BlockAccessor, this, pos.DownCopy(), BlockFacing.UP) && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) != 1)) return false;

            var storageProps = player.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps;
            if (storageProps != null && storageProps.SprintKey && !player.Entity.Controls.CtrlKey)
            {
                return false;
            }

            BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
            double dx = player.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
            double dz = (float)player.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
            float angleHor = (float)Math.Atan2(dx, dz);

            float deg90 = GameMath.PIHALF;
            float roundRad = ((int)Math.Round(angleHor / deg90)) * deg90;
            BlockFacing attachFace = null;

            if (storageProps.Layout == EnumGroundStorageLayout.WallHalves)
            {
                attachFace = SuggestedHVOrientation(player, blockSel)[0];

                var npos = pos.AddCopy(attachFace).Up(storageProps.WallOffY - 1);
                var block = world.BlockAccessor.GetBlock(npos);
                if (!block.CanAttachBlockAt(world.BlockAccessor, this, npos, attachFace.Opposite))
                {
                    attachFace = null;
                    foreach (var face in BlockFacing.HORIZONTALS)
                    {
                        npos = pos.AddCopy(face).Up(storageProps.WallOffY - 1);
                        block = world.BlockAccessor.GetBlock(npos);
                        if (block.CanAttachBlockAt(world.BlockAccessor, this, npos, face.Opposite))
                        {
                            attachFace = face;
                            break;
                        }
                    }
                }

                if (attachFace == null)
                {
                    if (storageProps.WallOffY > 1)
                    {
                        (api as ICoreClientAPI)?.TriggerIngameError(this, "requireswall", Lang.Get("placefailure-requirestallwall", storageProps.WallOffY));
                    }
                    else
                    {
                        (api as ICoreClientAPI)?.TriggerIngameError(this, "requireswall", Lang.Get("placefailure-requireswall"));
                    }
                    return false;
                }

                roundRad = (float)Math.Atan2(attachFace.Normali.X, attachFace.Normali.Z);
            }

            world.BlockAccessor.SetBlock(BlockId, pos);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                beg.MeshAngle = roundRad;
                beg.AttachFace = attachFace;
                beg.OnPlayerInteractStart(player, blockSel);
                beg.MarkDirty(true);
            }

            if (CollisionTester.AabbIntersect(
                GetCollisionBoxes(world.BlockAccessor, pos)[0],
                pos.X, pos.Y, pos.Z,
                player.Entity.SelectionBox,
                player.Entity.SidedPos.XYZ
            ))
            {
                player.Entity.SidedPos.Y += GetCollisionBoxes(world.BlockAccessor, pos)[0].Y2;
            }

            (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            
            return true;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            BlockEntityGroundStorage beg = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGroundStorage;
            bool isWallHalves = beg?.StorageProps != null && beg.StorageProps.Layout == EnumGroundStorageLayout.WallHalves;

            if (isWallHalves)
            {
                var facing = beg.AttachFace;
                var bpos = pos.AddCopy(facing.Normali.X, beg.StorageProps.WallOffY - 1, facing.Normali.Z);
                var block = world.BlockAccessor.GetBlock(bpos);

                if (!block.CanAttachBlockAt(world.BlockAccessor, this, bpos, facing))
                {
                    world.BlockAccessor.BreakBlock(pos, null);
                }
            } else
            {
                // Don't run falling behavior for wall halves
                base.OnNeighbourBlockChange(world, pos, neibpos);
            }
        }


        public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
        {
            BlockEntity be = capi.World.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                ItemSlot slot = beg.Inventory.ToArray().Shuffle(capi.World.Rand).FirstOrDefault(s => !s.Empty);
                if (slot != null)
                {
                    return slot.Itemstack.Collectible.GetRandomColor(capi, slot.Itemstack);
                }
            }

            return base.GetColorWithoutTint(capi, pos);
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            BlockEntity be = capi.World.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                ItemSlot slot = beg.Inventory.ToArray().Shuffle(capi.World.Rand).FirstOrDefault(s => !s.Empty);
                if (slot != null)
                {
                    return slot.Itemstack.Collectible.GetRandomColor(capi, slot.Itemstack);
                }
            }

            return base.GetRandomColor(capi, pos, facing, rndIndex);
        }

        public override int GetRandomColor(ICoreClientAPI capi, ItemStack stack)
        {
            return base.GetRandomColor(capi, stack);
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                return beg.GetBlockName();
            }
            else return OnPickBlock(world, pos)?.GetName();
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var beg = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGroundStorage;
            if (beg != null)
            {
                return beg.Inventory.FirstNonEmptySlot?.Itemstack.Clone();
            }

            return null;
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            var beg = world.BlockAccessor.GetBlockEntity(selection.Position) as BlockEntityGroundStorage;
            if (beg?.StorageProps != null)
            {
                int bulkquantity = beg.StorageProps.BulkTransferQuantity;

                if (beg.StorageProps.Layout == EnumGroundStorageLayout.Stacking && !beg.Inventory.Empty)
                {
                    var collObj = beg.Inventory[0].Itemstack.Collectible;

                    return new WorldInteraction[]
                    {
                        new WorldInteraction()
                        {
                            ActionLangCode = "blockhelp-groundstorage-addone",
                            MouseButton = EnumMouseButton.Right,
                            HotKeyCode = "shift",
                            Itemstacks = new ItemStack[] { new ItemStack(collObj, 1) }
                        },
                        new WorldInteraction()
                        {
                            ActionLangCode = "blockhelp-groundstorage-removeone",
                            MouseButton = EnumMouseButton.Right,
                            HotKeyCode = null
                        },

                        new WorldInteraction()
                        {
                            ActionLangCode = "blockhelp-groundstorage-addbulk",
                            MouseButton = EnumMouseButton.Right,
                            HotKeyCodes = new string[] {"ctrl", "shift" },
                            Itemstacks = new ItemStack[] { new ItemStack(collObj, bulkquantity) }
                        },
                        new WorldInteraction()
                        {
                            ActionLangCode = "blockhelp-groundstorage-removebulk",
                            HotKeyCode = "ctrl",
                            MouseButton = EnumMouseButton.Right
                        }

                    }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
                }

                if (beg.StorageProps.Layout == EnumGroundStorageLayout.SingleCenter)
                {
                    return new WorldInteraction[]
                    {
                        new WorldInteraction()
                        {
                            ActionLangCode = "blockhelp-behavior-rightclickpickup",
                            MouseButton = EnumMouseButton.Right
                        },

                    }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)); 
                }

                if (beg.StorageProps.Layout == EnumGroundStorageLayout.Halves || beg.StorageProps.Layout == EnumGroundStorageLayout.Quadrants)
                {
                    return new WorldInteraction[]
                    {
                        new WorldInteraction()
                        {
                            ActionLangCode = "blockhelp-groundstorage-add",
                            MouseButton = EnumMouseButton.Right,
                            HotKeyCode = "shift",
                            Itemstacks = beg.StorageProps.Layout == EnumGroundStorageLayout.Halves ? groundStorablesHalves : groundStorablesQuadrants
                        },
                        new WorldInteraction()
                        {
                            ActionLangCode = "blockhelp-groundstorage-remove",
                            MouseButton = EnumMouseButton.Right,
                            HotKeyCode = null
                        }

                    }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
                }

            }

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }



    }




}
