using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFence : Block
    {
        public string GetOrientations(IWorldAccessor world, BlockPos pos)
        {
            string orientations =
                GetFenceCode(world, pos, BlockFacing.NORTH) +
                GetFenceCode(world, pos, BlockFacing.EAST) +
                GetFenceCode(world, pos, BlockFacing.SOUTH) +
                GetFenceCode(world, pos, BlockFacing.WEST)
            ;

            if (orientations.Length == 0) orientations = "empty";

            /*if (orientations != "ngs") orientations = orientations.Replace("gs", "ngs");
            if (orientations != "gns") orientations = orientations.Replace("gn", "gns");
            if (orientations != "gew") orientations = orientations.Replace("ge", "gew");
            if (orientations != "egw") orientations = orientations.Replace("gw", "egw");*/

            return orientations;
        }

        private string GetFenceCode(IWorldAccessor world, BlockPos pos, BlockFacing facing)
        {
            /*if (IsFenceGateAt(world, pos.AddCopy(facing)))
            {
                return "g" + facing.Code[0];
            }*/

            if (ShouldConnectAt(world, pos, facing)) return ""+facing.Code[0];

            return "";
        }



        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            string orientations = GetOrientations(world, blockSel.Position);
            Block block = world.BlockAccessor.GetBlock(CodeWithVariant("type", orientations));

            if (block == null) block = this;

            if (block.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            string orientations = GetOrientations(world, pos);

            AssetLocation newBlockCode = CodeWithVariant("type", orientations);

            if (!Code.Equals(newBlockCode))
            {
                Block block = world.BlockAccessor.GetBlock(newBlockCode);
                if (block == null) return;

                world.BlockAccessor.SetBlock(block.BlockId, pos);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
            }
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[] { new BlockDropItemStack(handbookStack) };
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithVariants(new string[] { "type", "cover" }, new string[] { "ew", "free" }));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithVariants(new string[] { "type", "cover" }, new string[] { "ew", "free" }));
            return new ItemStack(block);
        }



        public bool ShouldConnectAt(IWorldAccessor world, BlockPos ownPos, BlockFacing side)
        {
            Block block = world.BlockAccessor.GetBlock(ownPos.AddCopy(side));

            bool attrexists = block.Attributes?["fenceConnect"][side.Code].Exists == true;
            if (attrexists)
            {
                return block.Attributes["fenceConnect"][side.Code].AsBool(true);
            }

            return
                (block.FirstCodePart() == FirstCodePart() || block.FirstCodePart() == FirstCodePart() + "gate")
                || block.SideSolid[side.GetOpposite().Index];
            ;
        }


        static string[] OneDir = new string[] { "n", "e", "s", "w" };
        static string[] TwoDir = new string[] { "ns", "ew" };
        static string[] AngledDir = new string[] { "ne", "es", "sw", "nw" };
        static string[] ThreeDir = new string[] { "nes", "new", "nsw", "esw" };

        static string[] GateLeft = new string[] { "egw", "ngs" };
        static string[] GateRight = new string[] { "gew", "gns" };

        static Dictionary<string, KeyValuePair<string[], int>> AngleGroups = new Dictionary<string, KeyValuePair<string[], int>>();

        static BlockFence() {
            AngleGroups["n"] = new KeyValuePair<string[], int>(OneDir, 0);
            AngleGroups["e"] = new KeyValuePair<string[], int>(OneDir, 1);
            AngleGroups["s"] = new KeyValuePair<string[], int>(OneDir, 2);
            AngleGroups["w"] = new KeyValuePair<string[], int>(OneDir, 3);

            AngleGroups["ns"] = new KeyValuePair<string[], int>(TwoDir, 0);
            AngleGroups["ew"] = new KeyValuePair<string[], int>(TwoDir, 1);

            AngleGroups["ne"] = new KeyValuePair<string[], int>(AngledDir, 0);
            AngleGroups["nw"] = new KeyValuePair<string[], int>(AngledDir, 1);
            AngleGroups["es"] = new KeyValuePair<string[], int>(AngledDir, 2);
            AngleGroups["sw"] = new KeyValuePair<string[], int>(AngledDir, 3);

            AngleGroups["nes"] = new KeyValuePair<string[], int>(ThreeDir, 0);
            AngleGroups["new"] = new KeyValuePair<string[], int>(ThreeDir, 1);
            AngleGroups["nsw"] = new KeyValuePair<string[], int>(ThreeDir, 2);
            AngleGroups["esw"] = new KeyValuePair<string[], int>(ThreeDir, 3);


            AngleGroups["egw"] = new KeyValuePair<string[], int>(GateLeft, 0);
            AngleGroups["ngs"] = new KeyValuePair<string[], int>(GateLeft, 1);

            AngleGroups["gew"] = new KeyValuePair<string[], int>(GateRight, 0);
            AngleGroups["gns"] = new KeyValuePair<string[], int>(GateRight, 1);
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            string type = Variant["type"];

            if (type == "empty" || type == "nesw") return Code;

            int angleIndex = angle / 90;

            var val = AngleGroups[type];

            string newFacing = val.Key[(angleIndex + val.Value) % val.Key.Length];

            return CodeWithVariant("type", newFacing);
        }
    }
}
