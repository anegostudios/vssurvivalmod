using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBrake : BlockEntity
    {
        public bool Engaged { get; protected set; }


        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            /*if (api.World.Side == EnumAppSide.Client)
            {
                float rotY = Block.Shape.rotateY;
                animUtil.InitializeAnimator("brake", new Vec3f(0, rotY, 0));
                
            }*/


        }


        private void OnClientGameTick(float dt)
        {
            /*            if (ownBlock == null || Api?.World == null || !canTeleport || !Activated) return;

                        if (playerInside)
                        {
                            animUtil.StartAnimation(new AnimationMetaData() { Animation = "idle", Code = "idle", AnimationSpeed = 1, EaseInSpeed = 100, EaseOutSpeed = 100, BlendMode = EnumAnimationBlendMode.Average });
                            animUtil.StartAnimation(new AnimationMetaData() { Animation = "teleport", Code = "teleport", AnimationSpeed = 1, EaseInSpeed = 8, EaseOutSpeed = 8, BlendMode = EnumAnimationBlendMode.Add });
                        }
                        else
                        {
                            animUtil.StopAnimation("teleport");
                        }*/
        }

        MeshData ownMesh;

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            base.OnTesselation(mesher, tessThreadTesselator);

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

            MeshData mesh;

            if (meshes.TryGetValue("" + rotY, out mesh))
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
            Api.World.PlaySoundAt(new AssetLocation("sounds/effect/woodswitch.ogg"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, byPlayer);

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
    }
}
