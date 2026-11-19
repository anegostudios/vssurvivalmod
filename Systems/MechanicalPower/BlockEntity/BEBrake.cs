using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBrake : BlockEntity, IRotatable
    {
        public bool Engaged { get; protected set; }

        MeshData ownMesh;

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            base.OnTesselation(mesher, tessThreadTesselator);

            BlockBrakeFrame frameBlock = Block as BlockBrakeFrame ?? (Block as BlockAxleAttachmentAssembly)?.FrameBlock as BlockBrakeFrame;
            if (frameBlock != null)
            {
                ItemStack frameStack = GetBehavior<BEBehaviorAxleAttachmentFrame>().GetFrameStack();
                mesher.AddMeshData(frameBlock.GetFrameMesh(Api as ICoreClientAPI, tessThreadTesselator, frameStack, Engaged));
                return true;
            }

            if (!Engaged)
            {
                if (ownMesh == null)
                {
                    ownMesh = GenOpenedMesh(tessThreadTesselator, Block.Shape.rotateY);
                    if (ownMesh == null) return false;
                }

                mesher.AddMeshData(ownMesh);

                return true;
            }

            return false;
        }


        private MeshData GenOpenedMesh(ITesselatorAPI tesselator, float rotY)
        {
            string key = "mechbrakeOpenedMesh";

            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(Api, key, () =>
            {
                return new Dictionary<string, MeshData>();
            });


            if (meshes.TryGetValue("" + rotY, out MeshData mesh))
            {
                return mesh;
            }

            AssetLocation shapeloc = AssetLocation.Create("shapes/block/wood/mechanics/brake-stand-opened.json", Block.Code.Domain);
            Shape shape = API.Common.Shape.TryGet(Api, shapeloc);
            tesselator.TesselateShape(Block, shape, out mesh, new Vec3f(0, rotY, 0));

            return meshes["" + rotY] = mesh;
        }


        public bool OnInteract(IPlayer byPlayer)
        {
            Engaged = !Engaged;
            Api.World.PlaySoundAt(new AssetLocation("sounds/effect/woodswitch.ogg"), Pos, 0, byPlayer);

            MarkDirty(true);
            return true;
        }



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            Engaged = tree.GetBool("engaged");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("engaged", Engaged);
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            foreach (var bh in Behaviors)
            {
                (bh as IRotatable)?.OnTransformed(worldAccessor, tree, degreeRotation, oldBlockIdMapping, oldItemIdMapping, flipAxis);
            }
        }
    }
}
