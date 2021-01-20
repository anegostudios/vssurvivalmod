using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    // Concept:
    // - Check every 1 second if the hammer is obstructed, if so, stop moving
    // - Hammer itself is rendered using 1 draw call
    // - Check every 0.25 seconds if its receiving power from the toggle, if not, stop animating the hammer
    // - Helve hammer helps with iron bloom refining and plate making
    public class BEHelveHammer : BlockEntity, ITexPositionSource
    {
        int count = 40;
        bool obstructed;

        BEBehaviorMPToggle mptoggle;
        ITexPositionSource blockTexSource;

        ItemStack hammerStack;
        public ItemStack HammerStack
        {
            get
            {
                return hammerStack;
            }
            set
            {
                hammerStack = value;
                MarkDirty(false);
                setRenderer();
            }
        }

        double ellapsedInameSecGrow;

        float rnd;
        public BlockFacing facing;
        private BlockPos togglePos;
        private BlockPos anvilPos;
        BlockEntityAnvil targetAnvil;

        double angleBefore;
        bool didHit;
        float vibrate;

        ICoreClientAPI capi;

        public float Angle
        {
            get
            {
                if (mptoggle == null) return 0;
                if (obstructed) return (float)angleBefore;

                double totalIngameSeconds = Api.World.Calendar.TotalHours * 60 * 2;

                double adjust = 0;
                switch (facing.Index)
                {
                    case 3:
                        adjust = mptoggle.isRotationReversed() ? 1.9 : 0.6;
                        break;
                    case 1:
                        adjust = mptoggle.isRotationReversed() ? -0.65 : -1.55;
                        break;
                    case 0:
                        adjust = mptoggle.isRotationReversed() ? -0.4 : 1.2;
                        break;
                    default:
                        adjust = mptoggle.isRotationReversed() ? 1.8 : 1.2;
                        break;
                }
                double x = GameMath.Mod(mptoggle.AngleRad * 2.0 + adjust - rnd, Math.PI * 20);
                double angle = Math.Abs(Math.Sin(x) / 4.5);
                float outAngle = (float)angle;

                if (angleBefore > angle)
                {
                    outAngle -= (float)(totalIngameSeconds - ellapsedInameSecGrow) * 1.5f;
                }
                else
                {
                    ellapsedInameSecGrow = totalIngameSeconds;
                }

                outAngle = Math.Max(0f, outAngle);

                vibrate *= 0.5f;

                if (outAngle <= 0.01f && !didHit)
                {
                    didHit = true;
                    vibrate = 0.02f;

                    if (Api.Side == EnumAppSide.Client && targetAnvil != null)
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/effect/anvilhit"), Pos.X + facing.Normali.X * 3 + 0.5f, Pos.Y + 0.5f, Pos.Z + facing.Normali.Z * 3 + 0.5f, null, 0.3f + (float)Api.World.Rand.NextDouble() * 0.2f, 8, 1);
                        targetAnvil.OnHelveHammerHit();
                    }
                }

                if (outAngle > 0.2)
                {
                    didHit = false;
                }

                angleBefore = angle;

                float finalAngle = outAngle + (float)Math.Sin(totalIngameSeconds) * vibrate;

                if (targetAnvil?.WorkItemStack != null)
                {
                    finalAngle = Math.Max(1.5f / 32f, finalAngle);
                }

                return finalAngle;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "metal")
                {
                    CompositeTexture ctex;
                    if (hammerStack.Item.Textures.TryGetValue(textureCode, out ctex))
                    {
                        AssetLocation texturePath = ctex.Base;
                        return capi.BlockTextureAtlas[texturePath];
                    }
                }

                return blockTexSource[textureCode];
            }
        }


        HelveHammerRenderer renderer;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            facing = BlockFacing.FromCode(Block.Variant["side"]);
            if (facing == null) { Api.World.BlockAccessor.SetBlock(0, Pos); return; }
            Vec3i dir = facing.Normali;
            anvilPos = Pos.AddCopy(dir.X * 3, 0, dir.Z * 3);
            togglePos = Pos.AddCopy(dir);


            RegisterGameTickListener(onEvery25ms, 25);




            capi = api as ICoreClientAPI;

            if (capi != null)
            {
                blockTexSource = capi.Tesselator.GetTexSource(Block);
            }

            setRenderer();
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            rnd = (float)Api.World.Rand.NextDouble() / 10;
        }

        public void updateAngle()
        {

        }

        void setRenderer()
        {
            if (HammerStack != null && renderer == null && Api.Side == EnumAppSide.Client)
            {
                renderer = new HelveHammerRenderer(Api as ICoreClientAPI, this, Pos, GenHammerMesh());
                (Api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "helvehammer");
                (Api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.ShadowFar, "helvehammer");
                (Api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.ShadowNear, "helvehammer");
            }
        }

        public override void OnBlockBroken()
        {
            if (HammerStack != null)
            {
                Api.World.SpawnItemEntity(HammerStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
            base.OnBlockBroken();
        }

        private MeshData GenHammerMesh()
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            if (block.BlockId == 0) return null;
            MeshData mesh;
            ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;
            Shape shape = Api.Assets.TryGet("shapes/block/wood/mechanics/helvehammer.json").ToObject<Shape>();
            mesher.TesselateShape("helvehammerhead", shape, out mesh, this, new Vec3f(0, block.Shape.rotateY, 0));

            return mesh;
        }

        float accumHits;

        private void onEvery25ms(float dt)
        {
            if (count >= 40)
            {
                count = 0;
                CheckValidToggleAndNotObstructed();
            }

            if (Api.World.Side == EnumAppSide.Server && targetAnvil != null && mptoggle?.Network != null && HammerStack != null && !obstructed)
            {
                float weirdOffset = 0.62f;

                float speed = Math.Abs(mptoggle.Network.Speed) * mptoggle.GearedRatio;
                accumHits += speed * weirdOffset * dt * 8f;

                if (accumHits > GameMath.PIHALF)
                {
                    targetAnvil.OnHelveHammerHit();
                    accumHits -= GameMath.PIHALF;
                }
            }

            count++;
        }

        private void CheckValidToggleAndNotObstructed()
        {
            targetAnvil = Api.World.BlockAccessor.GetBlockEntity(anvilPos) as BlockEntityAnvil;

            obstructed = false;
            if (renderer != null) renderer.Obstructed = false;

            mptoggle = Api.World.BlockAccessor.GetBlockEntity(togglePos)?.GetBehavior<BEBehaviorMPToggle>();
            if (mptoggle?.ValidHammerBase(Pos) == false)
            {
                mptoggle = null;
                obstructed = true;
                if (renderer != null) renderer.Obstructed = true;
                return;
            }

            BlockPos npos = Pos.AddCopy(0, 1, 0);
            for (int i = 0; i < 3; i++)
            {
                Block block = Api.World.BlockAccessor.GetBlock(npos);
                Cuboidf[] collboxes = block.GetCollisionBoxes(Api.World.BlockAccessor, npos);
                if (collboxes != null && collboxes.Length > 0)
                {
                    obstructed = true;
                    if (renderer != null) renderer.Obstructed = true;
                    break;
                }

                npos.Add(facing.Normali);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            hammerStack = tree.GetItemstack("hammerStack");
            hammerStack?.ResolveBlockOrItem(worldAccessForResolve);

            rnd = tree.GetFloat("rnd");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetItemstack("hammerStack", HammerStack);

            tree.SetFloat("rnd", rnd);
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();
        }


        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed);

            hammerStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);

            hammerStack?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(HammerStack), blockIdMapping, itemIdMapping);
        }
    }
}
