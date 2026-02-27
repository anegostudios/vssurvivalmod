using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BEBehaviorRightClickConstructable : BlockEntityBehavior, IInteractableWithHelp
    {
        protected RightClickConstruction rcc;
        public CompositeShape shape { get; protected set; }
        public event Action<CompositeShape> OnShapeChanged;

        protected float brokenDropsRatio=1f;

        public BEBehaviorRightClickConstructable(BlockEntity blockentity) : base(blockentity)
        {
            rcc = new RightClickConstruction();
            shape = blockentity.Block.Shape;
        }

        public bool IsComplete => rcc.CurrentCompletedStage == rcc.Stages.Length - 1;
        public int CurrentCompletedStage => rcc.CurrentCompletedStage;
        public int Stages => rcc.Stages.Length - 1;

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            
            base.Initialize(api, properties);
            brokenDropsRatio = properties["brokenDropsRatio"].AsFloat(1);
            var stages = properties["stages"].AsObject<ConstructionStage[]>();
            rcc.LateInit(stages, api, Pos.ToVec3d, "Block " + Block.Code);
            updateShape();
        }


        public bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            if (rcc.OnInteract(byPlayer.Entity, byPlayer.Entity.RightHandItemSlot))
            {
                updateShape();
                Blockentity.MarkDirty(true);
            }

            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            rcc.FromTreeAttributes(tree);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            rcc.ToTreeAttributes(tree);
            base.ToTreeAttributes(tree);
            updateShape();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            return true;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Api.World.EntityDebugMode)
            {
                dsc.AppendLine("<font color='#ccc'>construction stage= " + rcc.CurrentCompletedStage + " of " + rcc.Stages.Length + "</font>");
            }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative) return;

            var drops = rcc.GetDrops(brokenDropsRatio, Api.World.Rand);

            foreach (var drop in drops)
            {
                Api.World.SpawnItemEntity(drop, Pos);
            }
        }

        protected void updateShape()
        {
            shape = new CompositeShape()
            {
                Base = Block.Shape.Base,
                rotateY = Block.Shape.rotateY,
                SelectiveElements = rcc.getShapeElements()
            };

            OnShapeChanged?.Invoke(shape);
        }

        public WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            return rcc.GetInteractionHelp(world, forPlayer);
        }
    }
}
