using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockLogSection : Block
    {
        int barkFace1 = 0;
        int barkFace2 = 1;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            string rotation = Variant["rotation"] is string r ? string.Intern(r) : null;
            string segment = Variant["segment"] is string t ? string.Intern(t) : null;
            if (segment != null && segment.Length == 2)
            {
                if (rotation == "ud")
                {
                    barkFace1 = BlockFacing.FromFirstLetter(segment[0])?.Index ?? 0;
                    barkFace2 = BlockFacing.FromFirstLetter(segment[1])?.Index ?? 1;
                    alternatingVOffset = true;
                    alternatingVOffsetFaces = 0x30;
                }
                else if (rotation == "we")
                {
                    switch (segment)
                    {
                        case "ne": barkFace1 = 4; barkFace2 = 0; break;
                        case "se": barkFace1 = 5; barkFace2 = 0; break;
                        case "nw": barkFace1 = 4; barkFace2 = 2; break;
                        case "sw": barkFace1 = 5; barkFace2 = 2; break;
                    }
                    alternatingVOffset = true;
                    alternatingVOffsetFaces = 0x0A;
                }
                else if (rotation == "ns")
                {
                    switch (segment)
                    {
                        case "ne": barkFace1 = 4; barkFace2 = 1; break;
                        case "se": barkFace1 = 5; barkFace2 = 1; break;
                        case "nw": barkFace1 = 4; barkFace2 = 3; break;
                        case "sw": barkFace1 = 5; barkFace2 = 3; break;
                    }
                    alternatingVOffset = true;
                    alternatingVOffsetFaces = 0x05;
                }
            }
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            BlockLogSection[] around = new BlockLogSection[6];
            string rotation = GetBlocksAround(around, world.BlockAccessor, blockSel.Position.Copy());
            if (rotation == null || byPlayer?.Entity.Controls.ShiftKey == true)
            {
                switch (blockSel.Face.Axis)
                {
                    case EnumAxis.X: rotation = "we"; break;
                    case EnumAxis.Y: rotation = "ud"; break;
                    case EnumAxis.Z: rotation = "ns"; break;
                }
            }

            SelectSimilarBlocksAround(around, rotation);

            int normal0 = 0;
            int normal1 = 1;
            int normal2 = 2;
            int normal3 = 3;
            int normal4 = 4;
            int normal5 = 5;
            if (rotation == "we")
            {
                normal0 = 4;
                normal1 = 0;
                normal2 = 5;
                normal3 = 2;
                normal4 = 3;
                normal5 = 1;
            }
            else if (rotation == "ns")
            {
                normal0 = 4;
                normal1 = 1;
                normal2 = 5;
                normal3 = 3;
                normal4 = 0;
                normal5 = 2;
            }

            string seg = null;
            if (around[normal0] != null && around[normal2] == null)
            {
                seg = "s" + BlockFacing.FromFirstLetter(around[normal0].LastCodePart(1)[1]).Code.ToLowerInvariant()[0];
            }
            if (around[normal2] != null && around[normal0] == null)
            {
                seg = "n" + BlockFacing.FromFirstLetter(around[normal2].LastCodePart(1)[1]).Code.ToLowerInvariant()[0];
            }
            if (around[normal1] != null && around[normal3] == null)
            {
                seg = BlockFacing.FromFirstLetter(around[normal1].LastCodePart(1)[0]).Code.ToLowerInvariant()[0] + "w";
            }
            if (around[normal3] != null && around[normal1] == null)
            {
                seg = BlockFacing.FromFirstLetter(around[normal3].LastCodePart(1)[0]).Code.ToLowerInvariant()[0] + "e";
            }
            if (seg == null)
            {
                seg = around[normal5]?.LastCodePart(1) ?? around[normal4]?.LastCodePart(1) ?? "ne";
            }

            Block orientedBlock = world.BlockAccessor.GetBlock(CodeWithParts(seg, rotation));
            if (orientedBlock == null) orientedBlock = world.BlockAccessor.GetBlock(CodeWithParts(rotation));

            if (orientedBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }

            return false;
        }

        private void SelectSimilarBlocksAround(BlockLogSection[] around, string rotation)
        {
            int f1 = BlockFacing.FromFirstLetter(rotation[0]).Index;
            int f2 = BlockFacing.FromFirstLetter(rotation[1]).Index;
            for (int i = 0; i < 6; i++)
            {
                BlockLogSection neib = around[i];
                if (neib == null) continue;
                if (neib.LastCodePart() != rotation)
                {
                    around[i] = null;
                    continue;
                }
                if (i != f1 && i != f2)
                {
                    if (!neib.IsBarkFace(i)) around[i] = null;
                }
            }
        }

        private bool IsBarkFace(int i)
        {
            return barkFace1 == i || barkFace2 == i;
        }

        private string GetBlocksAround(BlockLogSection[] around, IBlockAccessor blockAccessor, BlockPos pos)
        {
            string[] any = new string[3];
            int[] anyCount = new int[3];
            if (blockAccessor.GetBlock(pos.North()) is BlockLogSection n)
            {
                around[0] = n;
                UpdateAny(n.LastCodePart(), any, anyCount);
            }
            if (blockAccessor.GetBlock(pos.South().East()) is BlockLogSection e)
            {
                around[1] = e;
                UpdateAny(e.LastCodePart(), any, anyCount);
            }
            if (blockAccessor.GetBlock(pos.South().West()) is BlockLogSection s)
            {
                around[2] = s;
                UpdateAny(s.LastCodePart(), any, anyCount);
            }
            if (blockAccessor.GetBlock(pos.North().West()) is BlockLogSection w)
            {
                around[3] = w;
                UpdateAny(w.LastCodePart(), any, anyCount);
            }
            if (blockAccessor.GetBlock(pos.East().Up()) is BlockLogSection u)
            {
                around[4] = u;
                UpdateAny(u.LastCodePart(), any, anyCount);
            }
            if (blockAccessor.GetBlock(pos.Down().Down()) is BlockLogSection d)
            {
                around[5] = d;
                UpdateAny(d.LastCodePart(), any, anyCount);
            }

            if (anyCount[1] > anyCount[0])
            {
                any[0] = any[1];
                anyCount[0] = anyCount[1];
            }
            return (anyCount[2] > anyCount[0]) ? any[2] : any[0];
        }

        private void UpdateAny(string part, string[] any, int[] anyCount)
        {
            for (int i = 0; i < 3; i++)
            {
                if (any[i] == null || any[i] == part)
                {
                    any[i] = part;
                    anyCount[i]++;
                    return;
                }
            }
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.GetBlock(CodeWithParts("ne", "ud")));
        }


        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            if (LastCodePart() == "ud") return Code;

            string[] angles = { "ns", "we" };
            var index = GameMath.Mod(angle / 90, 4);
            if (LastCodePart() == "we") index++;

            return CodeWithParts(angles[index % 2]);
        }


    }
}
