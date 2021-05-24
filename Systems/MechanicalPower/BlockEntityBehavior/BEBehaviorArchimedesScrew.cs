using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPArchimedesScrew : BEBehaviorMPBase
    {
        ICoreClientAPI capi;
        float resistance;


        public BEBehaviorMPArchimedesScrew(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
            }

            AxisSign = new int[] { 0, 1, 0 };

            resistance = properties["resistance"].AsFloat(0.015f);
        }

        public override float GetResistance()
        {
            return resistance;
        }

        protected virtual MeshData getHullMesh()
        {
            CompositeShape cshape = properties["staticShapePart"].AsObject<CompositeShape>(null, Block.Code.Domain);
            if (cshape == null) return null;

            cshape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            return ObjectCacheUtil.GetOrCreate(Api, "archimedesscrew-mesh-" + cshape.Base.Path + "-" + cshape.rotateX + "-" + cshape.rotateY + "-" + cshape.rotateZ, () =>
            {
                Shape shape = capi.Assets.TryGet(cshape.Base).ToObject<Shape>();
                MeshData mesh;
                capi.Tesselator.TesselateShape(Block, shape, out mesh);

                mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), cshape.rotateX * GameMath.DEG2RAD, cshape.rotateY * GameMath.DEG2RAD, cshape.rotateZ * GameMath.DEG2RAD);

                return mesh;
            });
        }

        public bool IsAttachedToBlock()
        {
            for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
            {
                BlockFacing face = BlockFacing.HORIZONTALS[i];
                Block block = Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y + face.Normali.Y, Position.Z + face.Normali.Z);
                if (Block != block && block.SideSolid[face.Opposite.Index])
                {
                    return true;
                }
            }
            

            return false;
        }

        protected virtual bool AddStands => false;


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(getHullMesh());

            return base.OnTesselation(mesher, tesselator);
        }
    }
}
