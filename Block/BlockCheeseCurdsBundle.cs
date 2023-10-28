using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockCheeseCurdsBundle : Block
    {
        public Dictionary<string, MeshData> meshes = new Dictionary<string, MeshData>();

        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            ItemStack[] stickStack = new ItemStack[] { new ItemStack(api.World.GetItem(new AssetLocation("stick"))) };
            ItemStack[] saltStack = new ItemStack[] { new ItemStack(api.World.GetItem(new AssetLocation("salt")), 5) };

            interactions = new WorldInteraction[] { 
                new WorldInteraction() {
                    ActionLangCode = "blockhelp-curdbundle-addstick",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = stickStack,
                    GetMatchingStacks = (WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection) =>
                    {
                        BECheeseCurdsBundle beccb = api.World.BlockAccessor.GetBlockEntity(blockSelection.Position) as BECheeseCurdsBundle;
                        return beccb?.State == EnumCurdsBundleState.Bundled ? stickStack : null;
                    }
                },
                new WorldInteraction() {
                    ActionLangCode = "blockhelp-curdbundle-squeeze",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = null,
                    ShouldApply = (WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection) =>
                    {
                        BECheeseCurdsBundle beccb = api.World.BlockAccessor.GetBlockEntity(blockSelection.Position) as BECheeseCurdsBundle;
                        return beccb?.State == EnumCurdsBundleState.BundledStick && !beccb.Squuezed;
                    }
                },
                new WorldInteraction() {
                    ActionLangCode = "blockhelp-curdbundle-open",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = null,
                    ShouldApply = (WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection) =>
                    {
                        BECheeseCurdsBundle beccb = api.World.BlockAccessor.GetBlockEntity(blockSelection.Position) as BECheeseCurdsBundle;
                        return beccb?.State == EnumCurdsBundleState.BundledStick && beccb.Squuezed;
                    }
                },
                new WorldInteraction() {
                    ActionLangCode = "blockhelp-curdbundle-addsalt",
                    MouseButton = EnumMouseButton.Right,
                    
                    Itemstacks = saltStack,
                    GetMatchingStacks = (WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection) =>
                    {
                        BECheeseCurdsBundle beccb = api.World.BlockAccessor.GetBlockEntity(blockSelection.Position) as BECheeseCurdsBundle;
                        return beccb?.State == EnumCurdsBundleState.Opened ? saltStack : null;
                    }
                },
                new WorldInteraction() {
                    ActionLangCode = "blockhelp-curdbundle-pickupcheese",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = null,
                    ShouldApply = (WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection) =>
                    {
                        BECheeseCurdsBundle beccb = api.World.BlockAccessor.GetBlockEntity(blockSelection.Position) as BECheeseCurdsBundle;
                        return beccb?.State == EnumCurdsBundleState.OpenedSalted;
                    }
                }
            };
        }


        public Shape GetShape(EnumCurdsBundleState state)
        {
            string path = "shapes/block/food/curdbundle-plain.json";
            if (state == EnumCurdsBundleState.BundledStick) path = "shapes/block/food/curdbundle-stick.json";
            else if (state == EnumCurdsBundleState.Opened) path = "shapes/item/food/dairy/cheese/linen-raw.json";
            else if (state == EnumCurdsBundleState.OpenedSalted) path = "shapes/item/food/dairy/cheese/linen-salted.json";

            return API.Common.Shape.TryGet(api, path);
        }


        public MeshData GetMesh(EnumCurdsBundleState state, float angle)
        {
            string key = (int)state + "-" + angle;
            if (!meshes.ContainsKey(key))
            {
                Shape shape = GetShape(state);
                ICoreClientAPI capi = api as ICoreClientAPI;
                MeshData mesh;
                capi.Tesselator.TesselateShape(this, shape, out mesh, new Vec3f(0, angle * GameMath.RAD2DEG, 0));

                meshes[key] = mesh;
            }

            return meshes[key];
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                BECheeseCurdsBundle bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BECheeseCurdsBundle;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);

                    float deg22dot5rad = GameMath.PIHALF / 4;
                    float roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                    bect.MeshAngle = roundRad;
                }
            }

            return val;
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BECheeseCurdsBundle beccb = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BECheeseCurdsBundle;
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (beccb == null) return false;

            if (beccb.State == EnumCurdsBundleState.Bundled)
            {
                if (hotbarSlot.Itemstack?.Collectible.Code.Path == "stick")
                {
                    beccb.State = EnumCurdsBundleState.BundledStick;
                    hotbarSlot.TakeOut(1);
                    hotbarSlot.MarkDirty();
                }
                return true;
            }

            if (beccb.State == EnumCurdsBundleState.BundledStick && !beccb.Squuezed) { 
                beccb.StartSqueeze(byPlayer);
                return true;
            }

            if (beccb.State == EnumCurdsBundleState.BundledStick && beccb.Squuezed)
            {
                beccb.State = EnumCurdsBundleState.Opened;
                api.World.PlaySoundAt(Sounds.Place, blockSel.Position.X + 0.5, blockSel.Position.Y, blockSel.Position.Z + 0.5, byPlayer);
                return true;
            }

            if (beccb.State == EnumCurdsBundleState.Opened)
            {
                if (hotbarSlot.Itemstack?.Collectible.Code.Path == "salt" && hotbarSlot.StackSize >= 5)
                {
                    beccb.State = EnumCurdsBundleState.OpenedSalted;
                    hotbarSlot.TakeOut(5);
                    hotbarSlot.MarkDirty();
                }

                return true;
            }

            if (beccb.State == EnumCurdsBundleState.OpenedSalted)
            {
                ItemStack cheeseRoll = new ItemStack(api.World.GetItem(new AssetLocation("rawcheese-salted")));
                if (!byPlayer.InventoryManager.TryGiveItemstack(cheeseRoll, true))
                {
                    api.World.SpawnItemEntity(cheeseRoll, byPlayer.Entity.Pos.XYZ.Add(0, 0.5, 0));
                }

                api.World.BlockAccessor.SetBlock(api.World.GetBlock(new AssetLocation("linen-normal-down")).Id, blockSel.Position);
                return true;
            }



            return true;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        public void SetContents(ItemStack blockstack, ItemStack contents)
        {
            blockstack.Attributes.SetItemstack("contents", contents);
        }

        public ItemStack GetContents(ItemStack blockstack)
        {
            ItemStack stack = blockstack.Attributes.GetItemstack("contents");
            stack?.ResolveBlockOrItem(api.World);
            return stack;
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }
    }
}
