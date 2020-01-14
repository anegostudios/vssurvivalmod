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
    // - Check every 0.25 seconds if its receiving power from the toggle, if so, stop animating the hammer
    // - Helve hammer helps with iron bloom refining and plate making
    public class BEHelveHammer : BlockEntity
    {
        int count = 0;
        bool hasPower;
        bool obstructed;

        BEBehaviorMPToggle mptoggle;

        bool hasHammer;
        public bool HasHammer { 
            get
            {
                return hasHammer;
            }
            set
            {
                hasHammer = value;
                MarkDirty(false);
                setRenderer();
            }
        }

        long ellapsedMsGrow;
        double lastGrowAngle;
        long lastImpactMs;
        float rnd;
        public BlockFacing facing;
        bool hasAnvil;

        double angleBefore;

        public float Angle
        {
            get {
                if (mptoggle == null) return 0;

                long totalMs = Api.World.ElapsedMilliseconds;
                
                double x = GameMath.Mod(mptoggle.AngleRad * 2 - 0.7, GameMath.TWOPI * 10);
                double angle = Math.Abs(Math.Sin(x) / 4.5);
                float outAngle = (float)angle;

                if (angleBefore > outAngle)
                {
                    outAngle -= (float)(((totalMs - ellapsedMsGrow) / 1000.0)) * 1.5f;
                    
                } else
                {
                    ellapsedMsGrow = totalMs;
                }

                outAngle = Math.Max(0f, outAngle);


                if (outAngle <= 0.01 && angleBefore >= 0.01 && totalMs - lastImpactMs > 300 && mptoggle.Network != null && mptoggle.Network.Speed > 0)
                {
                    //outAngle += 0.04f;
                    if (Api.Side == EnumAppSide.Client && hasAnvil)
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/effect/anvilhit"), Pos.X + facing.Normali.X * 3 + 0.5f, Pos.Y + 0.5f, Pos.Z + facing.Normali.Z * 3 + 0.5f, null, 0.3f + (float)Api.World.Rand.NextDouble() * 0.2f, 8, 1);
                    }

                    lastImpactMs = totalMs;
                }

                angleBefore = angle;

                return outAngle;
            }
        }

        HelveHammerRenderer renderer;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            facing = BlockFacing.FromCode(Block.Variant["side"]);
            if (facing == null) { Api.World.BlockAccessor.SetBlock(0, Pos); return; }

            RegisterGameTickListener(onEvery250ms, 250);


            rnd = (float)Api.World.Rand.NextDouble() / 20;

            setRenderer();

        }

        void setRenderer()
        {
            if (hasHammer && renderer == null && Api.Side == EnumAppSide.Client)
            {
                renderer = new HelveHammerRenderer(Api as ICoreClientAPI, this, Pos, GenHammerMesh());
                (Api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque);
                (Api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.ShadowFar);
                (Api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.ShadowNear);
            }
        }

        public override void OnBlockBroken()
        {
            if (HasHammer)
            {
                ItemStack stack = new ItemStack(Api.World.GetItem(new AssetLocation("helvehammer")));
                Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 05));
            }
            base.OnBlockBroken();
        }

        private MeshData GenHammerMesh()
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            if (block.BlockId == 0) return null;
            MeshData mesh;
            ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;
            mesher.TesselateShape(block, Api.Assets.TryGet("shapes/block/wood/mechanics/helvehammer.json").ToObject<Shape>(), out mesh);

            mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, block.Shape.rotateY * GameMath.DEG2RAD, 0);

            return mesh;
        }

        private void onEvery250ms(float dt)
        {
            if (count % 4 == 0)
            {
                Vec3i dir = facing.Normali;
                BlockPos npos = Pos.AddCopy(0, 1, 0);
                obstructed = false;
                hasPower = false;

                hasAnvil = Api.World.BlockAccessor.GetBlock(Pos.AddCopy(dir.X * 3, 0, dir.Z * 3)) is BlockAnvil;

                if (renderer != null)
                {
                    renderer.Obstruced = false;
                }

                mptoggle = Api.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(dir))?.GetBehavior<BEBehaviorMPToggle>();

                for (int i = 0; i < 3; i++)
                {
                    Block block = Api.World.BlockAccessor.GetBlock(npos);
                    Cuboidf[] collboxes = block.GetCollisionBoxes(Api.World.BlockAccessor, npos);
                    if (collboxes != null && collboxes.Length > 0)
                    {
                        obstructed = true;
                        if (renderer != null)
                        {
                            renderer.Obstruced = true;
                        }
                        break;
                    }

                    npos.Add(dir);
                }
            }

            count++;
        }



        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);
            hasHammer = tree.GetBool("hasHammer");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("hasHammer", hasHammer);
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Unregister();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Unregister();
        }
    }
}
