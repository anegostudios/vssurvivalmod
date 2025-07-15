using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBunchOCandles : Block
    {
        WorldInteraction[] interactions;
        internal int QuantityCandles;

        internal Vec3f[] candleWickPositions = {
            new(3 + 0.8f, 0 + 4, 3 + 0.8f),
            new(7 + 0.8f, 0 + 7, 4 + 0.8f),
            new(12 + 0.8f, 0 + 2, 1 + 0.8f),

            new(4 + 0.8f, 0 + 5, 9 + 0.8f),
            new(7 + 0.8f, 0 + 2, 8 + 0.8f),
            new(12 + 0.8f, 0 + 6, 12 + 0.8f),
            new(11 + 0.8f, 0 + 4, 6 + 0.8f),
            new(1 + 0.8f, 0 + 1, 12 + 0.8f),
            new(6 + 0.8f, 0 + 4, 13 + 0.8f)
        };

        Vec3f[][] candleWickPositionsByRot = new Vec3f[4][];


        internal void initRotations()
        {
            for (int i = 0; i < 4; i++)
            {
                Matrixf m = new Matrixf();
                m.Translate(0.5f, 0.5f, 0.5f);
                m.RotateYDeg(i * 90);
                m.Translate(-0.5f, -0.5f, -0.5f);

                Vec3f[] poses = candleWickPositionsByRot[i] = new Vec3f[candleWickPositions.Length];
                for (int j = 0; j < poses.Length; j++)
                {
                    Vec4f rotated = m.TransformVector(new Vec4f(candleWickPositions[j].X / 16f, candleWickPositions[j].Y / 16f, candleWickPositions[j].Z / 16f, 1));
                    poses[j] = new Vec3f(rotated.X, rotated.Y, rotated.Z);
                }
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            initRotations();
            QuantityCandles = Variant["quantity"].ToInt();

            interactions = ObjectCacheUtil.GetOrCreate(api, "candleInteractions", () =>
            {
                ItemStack[] candleStacks = api.World.SearchItems("candle").Select(x => new ItemStack(x)).ToArray();

                return new WorldInteraction[] {
                  new WorldInteraction() {
                      ActionLangCode = "blockhelp-groundstorage-addone",
                      MouseButton = EnumMouseButton.Right,
                      HotKeyCode = "shift",
                      Itemstacks = candleStacks,
                  },
                  new WorldInteraction() {
                      ActionLangCode = "blockhelp-groundstorage-removeone",
                      MouseButton = EnumMouseButton.Right,
                      RequireFreeHand = true
                  }
              };
            });
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            if (ParticleProperties != null && ParticleProperties.Length > 0)
            {
                int rnd = GameMath.MurmurHash3Mod(pos.X, pos.Y, pos.Z, 4);
                Vec3f[] poses = candleWickPositionsByRot[rnd];

                for (int i = 0; i < ParticleProperties.Length; i++)
                {
                    AdvancedParticleProperties bps = ParticleProperties[i];
                    bps.WindAffectednesAtPos = windAffectednessAtPos;

                    

                    for (int j = 0; j < QuantityCandles; j++)
                    {
                        Vec3f dp = poses[j];

                        bps.basePos.X = pos.X + dp.X;
                        bps.basePos.Y = pos.Y + dp.Y;
                        bps.basePos.Z = pos.Z + dp.Z;
                        manager.Spawn(bps);
                    }
                }
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!byPlayer.Entity.ActiveHandItemSlot.Empty)
            {
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            string curQuantity = this.Variant["quantity"];
            if (curQuantity == null)
            {
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            int.TryParse(curQuantity, out int stage);
            Block nextblock = world.GetBlock(this.CodeWithVariant("quantity", "" + (stage - 1)));

            if (nextblock == null)
            {
                world.BlockAccessor.SetBlock(0, blockSel.Position);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(blockSel.Position);
            }
            else
            {
                world.BlockAccessor.SetBlock(nextblock.BlockId, blockSel.Position);
                world.PlaySoundAt(nextblock.Sounds.Place, blockSel.Position, -0.4, byPlayer);
            }

            ItemStack outstack = new ItemStack(world.GetItem("candle"));
            if (outstack != null && !byPlayer.InventoryManager.TryGiveItemstack(outstack, slotNotifyEffect: true))
            {
                world.SpawnItemEntity(outstack, blockSel.Position);
            }

            return true;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
