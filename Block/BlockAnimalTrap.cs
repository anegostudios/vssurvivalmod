using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    public class BlockAnimalTrap : Block
    {
        protected float rotInterval = GameMath.PIHALF / 4;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            CanStep = false;
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                var be = GetBlockEntity<BlockEntityAnimalTrap>(blockSel.Position);
                if (be != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);

                    float roundRad = ((int)Math.Round(angleHor / rotInterval)) * rotInterval;

                    be.RotationYDeg = roundRad * GameMath.RAD2DEG;
                    be.MarkDirty(true);
                }
            }

            return val;
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = GetBlockEntity<BlockEntityAnimalTrap>(blockSel.Position);
            if (be != null) return be.Interact(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var be = GetBlockEntity<BlockEntityAnimalTrap>(pos);
            if (be != null && be.TrapState == EnumTrapState.Destroyed)
            {
                BlockDropItemStack[] destroyedDrops = Attributes?["destroyedDrops"]?.AsObject<BlockDropItemStack[]>(null);
                if (destroyedDrops == null) return Array.Empty<ItemStack>();

                List<ItemStack> todrop = new List<ItemStack>();
                for (int i = 0; i < destroyedDrops.Length; i++)
                {
                    BlockDropItemStack dstack = destroyedDrops[i];
                    dstack.Resolve(world, "Block ", Code);
                    ItemStack stack = dstack.ToRandomItemstackForPlayer(byPlayer, world, dropQuantityMultiplier);
                    if (stack != null) todrop.Add(stack);
                    if (dstack.LastDrop) break;
                }
                return todrop.ToArray();
            }

            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var be = GetBlockEntity<BlockEntityAnimalTrap>(pos);
            if (be != null)
            {
                blockModelData = be.GetCurrentMesh(null).Clone().Rotate(Vec3f.Half, 0, (be.RotationYDeg-90) * GameMath.DEG2RAD, 0);
                decalModelData = be.GetCurrentMesh(decalTexSource).Clone().Rotate(Vec3f.Half, 0, (be.RotationYDeg-90) * GameMath.DEG2RAD, 0);

                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);

        }

        public bool IsAppetizingBait(ICoreAPI api, ItemStack baitStack)
        {
            var collobj = baitStack.Collectible;

            return (collobj.NutritionProps != null || collobj.Attributes?["foodTags"].Exists == true) &&
                api.World.EntityTypes.Any(type => type.Attributes?["creatureDiet"].AsObject<CreatureDiet>()?.Matches(baitStack, true, 0.5f) == true);
        }

        public bool CanFitBait(ICoreAPI api, ItemStack baitStack)
        {
            var collobj = baitStack.Collectible;

            return Attributes?["excludeFoodTags"].AsArray<string>()?.Any(tag => collobj.Attributes?["foodTags"].AsArray<string>()?.Contains(tag) == true) != true;
        }
    }
}
