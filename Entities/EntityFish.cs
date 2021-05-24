using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Vintagestory.GameContent
{
    public class EntityFish : EntityAgent
    {
        private bool prevOnGround = false;
        private CompositeShape rotatedShape;

        static EntityFish()
        {
            AiTaskRegistry.Register<AiTaskFishMoveFast>("fishmovefast");
            AiTaskRegistry.Register<AiTaskFishOutOfWater>("fishoutofwater");

            //TODO: in flowing water, either leap or swim faster?  align against flow
            //TODO: die if out of water for too long
        }

        public override void OnGameTick(float dt)
        {
            //if (World.Side == EnumAppSide.Client)
            //{
            //    if (Alive)
            //    {
            //        if (this.OnGround != prevOnGround)
            //        {
            //            prevOnGround = this.OnGround;

            //            var essr = Properties.Client.Renderer as EntityShapeRenderer;
            //            if (this.OnGround)
            //            {
            //                if (rotatedShape == null)
            //                {
            //                    rotatedShape = Properties.Client.Shape.Clone();  //TODO cache this per type of entity, not per entity
            //                    rotatedShape.rotateX = 90;
            //                    rotatedShape.offsetY = -0.25f;
            //                }
            //                essr.OverrideCompositeShape = rotatedShape;
            //            }
            //            else
            //            {
            //                essr.OverrideCompositeShape = null;
            //            }

            //            essr.MarkShapeModified();
            //        }
            //    }
            //}

            base.OnGameTick(dt);
        }

    }
}
