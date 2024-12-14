using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class LookatEntityAction : IEntityAction
    {
        public string Type => "lookatentity";
        public bool ExecutionHasFailed { get; set; }

        EntityActivitySystem vas;
        [JsonProperty]
        AssetLocation targetEntityCode;
        [JsonProperty]
        float searchRange;
        EntityAgent entity;

        public LookatEntityAction() { }

        public LookatEntityAction(EntityActivitySystem vas, AssetLocation targetEntityCode, float searchRange)
        {
            this.vas = vas;
            this.targetEntityCode = targetEntityCode;
            this.searchRange = searchRange;
        }


        public bool IsFinished()
        {
            return true;
        }

        public void Start(EntityActivity act)
        {
            Entity targetEntity = getTarget();

            ExecutionHasFailed = entity == null;
            if (entity != null)
            {
                Vec3f targetVec = new Vec3f();

                targetVec.Set(
                    (float)(targetEntity.ServerPos.X - vas.Entity.ServerPos.X),
                    (float)(targetEntity.ServerPos.Y - vas.Entity.ServerPos.Y),
                    (float)(targetEntity.ServerPos.Z - vas.Entity.ServerPos.Z)
                );

                entity.ServerPos.Yaw = (float)Math.Atan2(targetVec.X, targetVec.Z);
            }
        }

        private Entity getTarget()
        {
            var api = vas.Entity.Api;
            var ep = api.ModLoader.GetModSystem<EntityPartitioning>();
            var targetEntity = ep.GetNearestEntity(vas.Entity.ServerPos.XYZ, searchRange, (e) => e.WildCardMatch(targetEntityCode));
            return targetEntity;
        }

        public void OnTick(float dt)
        {

        }

        public void Cancel()
        {

        }
        public void Finish() { }
        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public override string ToString()
        {
            return "Look at nearest entity " + targetEntityCode + " within " + searchRange + " blocks";
        }

        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Search Range", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "searchRange")

                .AddStaticText("Entity Code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "targetEntityCode")
            ;
        }

        public IEntityAction Clone()
        {
            return new LookatEntityAction(vas, targetEntityCode, searchRange);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            searchRange = singleComposer.GetTextInput("searchRange").GetText().ToFloat();
            targetEntityCode = new AssetLocation(singleComposer.GetTextInput("targetEntityCode").GetText());
            return true;
        }

        public void OnVisualize(ActivityVisualizer visualizer)
        {
            var target = getTarget();
            if (target != null)
            {
                visualizer.LineTo(target.Pos.XYZ.Add(0, 0.5, 0));
            }
        }
        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
