using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class LootItem
    {
        public AssetLocation[] codes;
        public EnumItemClass type;
        public float chance;
        public float minQuantity;
        public float maxQuantity;

        public static LootItem Item(float chance, float minQuantity, float maxQuantity, params string[] codes)
        {
            return new LootItem()
            {
                codes = AssetLocation.toLocations(codes),
                type = EnumItemClass.Item,
                chance = chance,
                minQuantity = minQuantity,
                maxQuantity = maxQuantity
            };
        }

        public static LootItem Item(float chance, float minQuantity, float maxQuantity, params AssetLocation[] codes)
        {
            return new LootItem()
            {
                codes = codes,
                type = EnumItemClass.Item,
                chance = chance,
                minQuantity = minQuantity,
                maxQuantity = maxQuantity
            };
        }

        public static LootItem Block(float chance, float minQuantity, float maxQuantity, params string[] codes)
        {
            return new LootItem()
            {
                codes = AssetLocation.toLocations(codes),
                type = EnumItemClass.Block,
                chance = chance,
                minQuantity = minQuantity,
                maxQuantity = maxQuantity
            };
        }

        public static LootItem Block(float chance, float minQuantity, float maxQuantity, params AssetLocation[] codes)
        {
            return new LootItem()
            {
                codes = codes,
                type = EnumItemClass.Block,
                chance = chance,
                minQuantity = minQuantity,
                maxQuantity = maxQuantity
            };
        }

        public ItemStack GetItemStack(IWorldAccessor world, int variant, int quantity)
        {
            ItemStack stack = null;

            AssetLocation code = codes[variant % codes.Length];

            if (type == EnumItemClass.Block)
            {
                var block = world.GetBlock(code);
                if (block != null)
                {
                    stack = new ItemStack(block, quantity);
                } else
                {
                    world.Logger.Warning("BlockLootVessel: Failed resolving block code {0}", code);
                }
            } else
            {
                var item = world.GetItem(code);
                if (item != null)
                {
                    stack = new ItemStack(item, quantity);
                }
                else
                {
                    world.Logger.Warning("BlockLootVessel: Failed resolving item code {0}", code);
                }
            }

            return stack;
        }

        public int GetDropQuantity(IWorldAccessor world, float dropQuantityMul)
        {
            var qfloat = dropQuantityMul * (minQuantity + ((float)world.Rand.NextDouble() * (maxQuantity - minQuantity)));

            var quantity = GameMath.RoundRandom(world.Rand, qfloat);

            return quantity;
        }
    }

    public class LootList
    {
        public float Tries = 0;
        public List<LootItem> lootItems = new List<LootItem>();
        public float TotalChance = 0;


        public ItemStack[] GenerateLoot(IWorldAccessor world, IPlayer forPlayer)
        {
            List<ItemStack> stacks = new List<ItemStack>();

            int variant = world.Rand.Next();
            float curtries = Tries;

            float dropRate = forPlayer?.Entity.Stats.GetBlended("vesselContentsDropRate") ?? 1;

            while (curtries >= 1 || curtries > world.Rand.NextDouble())
            {
                lootItems.Shuffle(world.Rand);

                double choice = world.Rand.NextDouble() * TotalChance;

                foreach(LootItem lootItem in lootItems)
                {
                    choice -= lootItem.chance;

                    if (choice <= 0)
                    {
                        var quantity = lootItem.GetDropQuantity(world, dropRate);
                        var stack = lootItem.GetItemStack(world, variant, quantity);
                        if (stack != null)
                        {
                            stacks.Add(stack);
                        }
                        break;
                    }
                }

                curtries--;
            }

            return stacks.ToArray();
        }

        public static LootList Create(float tries, params LootItem[] lootItems)
        {
            LootList list = new LootList();
            list.Tries = tries;
            list.lootItems.AddRange(lootItems);

            for (int i = 0; i < lootItems.Length; i++) list.TotalChance += lootItems[i].chance;

            return list;
        }
        
    }

    public class BlockLootVessel : Block
    {
        LootList lootList;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            lootList = LootList.Create(
                Attributes["lootTries"].AsInt(), 
                Attributes["lootList"].AsObject<LootItem[]>()
            );
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            List<BlockDropItemStack> drops = new List<BlockDropItemStack>();

            foreach (var val in lootList.lootItems)
            {
                for (int i = 0; i < val.codes.Length; i++)
                {
                    var lstack = val.GetItemStack(api.World, i, 1);
                    if (lstack == null)
                    {
                        continue;
                    }

                    BlockDropItemStack stack = new BlockDropItemStack(lstack);
                    if (stack == null) continue;
                    
                    stack.Quantity.avg = val.chance / lootList.TotalChance / val.codes.Length;

                    // Prevent duplicates
                    if (drops.FirstOrDefault(dstack => dstack.ResolvedItemstack.Equals(api.World, stack.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)) == null)
                    {
                        drops.Add(stack);
                    }
                }   
            }

            return drops.ToArray();
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            float selfdropRate = (byPlayer?.Entity.Stats.GetBlended("wholeVesselLootChance") ?? 0) - 1;
            if (api.World.Rand.NextDouble() < selfdropRate)
            {
                return new ItemStack[] { new ItemStack(this) };
            }

            if (lootList == null) return System.Array.Empty<ItemStack>();

            return lootList.GenerateLoot(world, byPlayer);
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            EnumTool? tool = itemslot.Itemstack?.Collectible?.GetTool(itemslot);

            if (tool == EnumTool.Hammer || tool == EnumTool.Pickaxe || tool == EnumTool.Shovel || tool == EnumTool.Sword || tool == EnumTool.Spear || tool == EnumTool.Axe || tool == EnumTool.Hoe)
            {
                if (counter % 5 == 0 || remainingResistance <= 0)
                {
                    double posx = blockSel.Position.X + blockSel.HitPosition.X;
                    double posy = blockSel.Position.InternalY + blockSel.HitPosition.Y;
                    double posz = blockSel.Position.Z + blockSel.HitPosition.Z;
                    int dim = blockSel.Position.dimension;
                    player.Entity.World.PlaySoundAt(remainingResistance > 0 ? Sounds.GetHitSound(player) : Sounds.GetBreakSound(player), posx, posy, posz, dim, player);
                }

                return remainingResistance - 0.05f;
            }

            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            SimpleParticleProperties props =
                new SimpleParticleProperties(
                    15, 22,
                    ColorUtil.ToRgba(150, 255, 255, 255),
                    new Vec3d(pos.X, pos.Y, pos.Z),
                    new Vec3d(pos.X + 1, pos.Y + 1, pos.Z + 1),
                    new Vec3f(-0.2f, -0.1f, -0.2f),
                    new Vec3f(0.2f, 0.2f, 0.2f),
                    1.5f,
                    0,
                    0.5f,
                    1.0f,
                    EnumParticleModel.Quad
                );

            props.OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -200);
            props.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 2);

            world.SpawnParticles(props);



            SimpleParticleProperties spiders =
                new SimpleParticleProperties(
                    8, 16,
                    ColorUtil.ToRgba(255, 30, 30, 30),
                    new Vec3d(pos.X, pos.Y, pos.Z),
                    new Vec3d(pos.X + 1, pos.Y + 1, pos.Z + 1),
                    new Vec3f(-2f, -0.3f, -2f),
                    new Vec3f(2f, 1f, 2f),
                    1f,
                    0.5f,
                    0.5f,
                    1.5f,
                    EnumParticleModel.Cube
                );
            
            
            world.SpawnParticles(spiders);



            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }
    }
}
