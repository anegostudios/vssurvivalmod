using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockToolMold : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            // Left in here for backwards compatibility with mods from before 1.21
            string createdByText = createdByText = Attributes["createdByText"]?.AsString();

            if (Attributes?["drop"].Exists == true && createdByText != null)
            {
                JsonItemStack ojstack = Attributes["drop"].AsObject<JsonItemStack>();
                if (ojstack != null)
                {
                    MetalProperty metals = capi.Assets.TryGet("worldproperties/block/metal.json").ToObject<MetalProperty>();
                    for (int i = 0; i < metals.Variants.Length; i++)
                    {
                        string metaltype = metals.Variants[i].Code.Path;
                        JsonItemStack jstack = ojstack.Clone();
                        jstack.Code.Path = jstack.Code.Path.Replace("{metal}", metaltype);

                        CollectibleObject collObj;

                        if (jstack.Type == EnumItemClass.Block)
                        {
                            collObj = capi.World.GetBlock(jstack.Code);
                        }
                        else
                        {
                            collObj = capi.World.GetItem(jstack.Code);
                        }

                        if (collObj == null) continue;

                        JToken token;

                        if (collObj.Attributes?["handbook"].Exists != true)
                        {
                            if (collObj.Attributes == null) collObj.Attributes = new JsonObject(JToken.Parse("{ handbook: {} }"));
                            else
                            {
                                token = collObj.Attributes.Token;
                                token["handbook"] = JToken.Parse("{ }");
                            }
                        }

                        token = collObj.Attributes["handbook"].Token;
                        token["createdBy"] = JToken.FromObject(createdByText);
                    }
                }
            }


            interactions = ObjectCacheUtil.GetOrCreate(api, Variant["tooltype"] + "moldBlockInteractions", () =>
            {
                List<ItemStack> smeltedContainerStacks = new List<ItemStack>();
                List<ItemStack> chiselStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is BlockSmeltedContainer)
                    {
                        smeltedContainerStacks.Add(new ItemStack(obj));
                    }
                }

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Tool is EnumTool.Chisel)
                    {
                        chiselStacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-toolmold-pour",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = smeltedContainerStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityToolMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityToolMold;
                            return (betm != null && !betm.IsFull && !betm.Shattered) ? wi.Itemstacks : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-toolmold-takeworkitem",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityToolMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityToolMold;
                            return betm != null && betm.IsFull && betm.IsHardened && !betm.Shattered && !betm.BreaksWhenFilled;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-toolmold-breakmoldforitem",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Left,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityToolMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityToolMold;
                            return betm != null && betm.IsFull && betm.IsHardened && !betm.Shattered && betm.BreaksWhenFilled;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-toolmold-chiselmoldforbits",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = chiselStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityToolMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityToolMold;
                            return (betm != null && betm.FillLevel > 0 && betm.IsHardened && !betm.Shattered) ? wi.Itemstacks : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-toolmold-pickup",
                        HotKeyCode = null,
                        RequireFreeHand = true,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityToolMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityToolMold;
                            return betm != null && betm.MetalContent == null && !betm.Shattered;
                        }
                    }
                };
            });
        }


        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (world.Rand.NextDouble() < 0.05 && GetBlockEntity<BlockEntityToolMold>(pos)?.Temperature > 300)
            {
                entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = this, Type = EnumDamageType.Fire, SourcePos = pos.ToVec3d() }, 0.5f);
            }

            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);
        }

        public override float GetTraversalCost(BlockPos pos, EnumAICreatureType creatureType)
        {
            if (creatureType == EnumAICreatureType.LandCreature || creatureType == EnumAICreatureType.Humanoid)
            {
                if (GetBlockEntity<BlockEntityToolMold>(pos)?.Temperature > 300) return 10000f;
            }

            return 0;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor ba, BlockPos pos)
        {
            var boxesRotated = getColSelRotatedBoxes(SelectionBoxes, (ba.GetBlockEntity(pos) as BlockEntityToolMold)?.MeshAngle ?? 0);

            if (RandomDrawOffset != 0 && boxesRotated?.Length >= 1)
            {
                float x = (GameMath.oaatHash(pos.X, 0, pos.Z) % 12) / (24f + 12f * RandomDrawOffset);
                float z = (GameMath.oaatHash(pos.X, 1, pos.Z) % 12) / (24f + 12f * RandomDrawOffset);

                return new Cuboidf[] { boxesRotated[0].OffsetCopy(x, 0, z) };
            }

            if (boxesRotated?.Length != 1) return boxesRotated;

            var chunk = ba.GetChunkAtBlockPos(pos);
            if (chunk == null) return boxesRotated;

            return chunk.AdjustSelectionBoxForDecor(ba, pos, boxesRotated);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return getColSelRotatedBoxes(CollisionBoxes, (blockAccessor.GetBlockEntity(pos) as BlockEntityToolMold)?.MeshAngle ?? 0);
        }

        private Cuboidf[] getColSelRotatedBoxes(Cuboidf[] boxes, float meshAngle)
        {
            if (meshAngle == 0) return boxes;

            var boxesRotated = new Cuboidf[boxes.Length];
            for (int i = 0; i < boxesRotated.Length; i++)
            {
                boxesRotated[i] = boxes[i].RotatedCopy(0, meshAngle * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5));
            }

            return boxesRotated;
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var be = GetBlockEntity<BlockEntityToolMold>(pos);
            if (be != null && be.MeshAngle != 0)
            {
                blockModelData = be.GetCurrentDecalMesh(be).Rotate(Vec3f.Half, 0, be.MeshAngle, 0);
                decalModelData = be.GetCurrentDecalMesh(decalTexSource).Rotate(Vec3f.Half, 0, be.MeshAngle, 0);

                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            if (byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(blockSel.Face.Opposite)) is BlockEntityToolMold betm)
            {
                if (byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) is IPlayer byPlayer)
                {
                    if (betm.OnPlayerInteract(byPlayer, blockSel.Face, blockSel.HitPosition))
                    {
                        handHandling = EnumHandHandling.PreventDefault;
                    }
                }
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel?.Position) is BlockEntityToolMold betm)
            {
                return betm.OnPlayerInteract(byPlayer, blockSel.Face, blockSel.HitPosition);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                failureCode = "onlywhensneaking";
                return false;
            }

            if (!world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()).CanAttachBlockAt(world.BlockAccessor, this, blockSel.Position.DownCopy(), BlockFacing.UP))
            {
                failureCode = "requiresolidground";
                return false;
            }

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val && world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityToolMold betm)
            {
                var targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                var dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                var dz = byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                var angleHor = (float)Math.Atan2(dx, dz);

                var roundRad = ((int)Math.Round(angleHor / GameMath.PIHALF)) * GameMath.PIHALF;
                betm.MeshAngle = roundRad;
                betm.MarkDirty();
            }

            return val;
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            var be = GetBlockEntity<BlockEntityToolMold>(blockSel.Position);

            be.BeingChiseled = player?.InventoryManager is IPlayerInventoryManager invMan && invMan.OffhandTool is EnumTool.Hammer && invMan.ActiveTool is EnumTool.Chisel;

            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityToolMold betm)
            {
                if (betm.FillLevel > 0 && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    if (byPlayer?.InventoryManager is IPlayerInventoryManager invMan && invMan.OffhandTool is EnumTool.Hammer && invMan.ActiveTool is EnumTool.Chisel)
                    {
                        ItemStack drop = betm.GetChiseledStack();

                        if (drop != null)
                        {
                            if (SplitDropStacks)
                            {
                                for (int k = 0; k < drop.StackSize; k++)
                                {
                                    ItemStack stack = drop.Clone();
                                    stack.StackSize = 1;
                                    world.SpawnItemEntity(stack, pos, null);
                                }
                            }
                            else
                            {
                                world.SpawnItemEntity(drop, pos, null);
                            }

                            world.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos, 0, byPlayer);

                            betm.MetalContent = null;
                            betm.FillLevel = 0;

                            DamageItem(world, byPlayer.Entity, invMan.ActiveHotbarSlot);
                            DamageItem(world, byPlayer.Entity, byPlayer.Entity?.LeftHandItemSlot);
                            return;
                        }
                    }

                    if (betm.BreaksWhenFilled)
                    {
                        world.PlaySoundAt(new AssetLocation("sounds/block/ceramicbreak"), pos, -0.4);
                        SpawnBlockBrokenParticles(pos);
                    }
                }
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            List<ItemStack> stacks = new List<ItemStack>();

            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityToolMold betm)
            {
                if (betm.GetStateAwareMold() is ItemStack[] mold)
                {
                    stacks.AddRange(mold);
                }

                if (betm.GetStateAwareMoldedStacks() is ItemStack[] contents)
                {
                    stacks.AddRange(contents);
                }
            }
            else
            {
                stacks.Add(new ItemStack(this));
            }


            return stacks.ToArray();
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityToolMold betm && betm.Shattered)
            {
                return Lang.Get("ceramicblock-blockname-shattered", base.GetPlacedBlockName(world, pos));
            }

            return base.GetPlacedBlockName(world, pos);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

    }
}
