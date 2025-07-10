using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class LookatEntityAction : EntityActionBase
    {
        public override string Type => "lookatentity";
     
        [JsonProperty]
        AssetLocation targetEntityCode;
        [JsonProperty]
        float searchRange;

        public LookatEntityAction() { }

        public LookatEntityAction(EntityActivitySystem vas, AssetLocation targetEntityCode, float searchRange)
        {
            this.vas = vas;
            this.targetEntityCode = targetEntityCode;
            this.searchRange = searchRange;
        }

        public override void Start(EntityActivity act)
        {
            Entity targetEntity = getTarget(vas.Entity.Api, vas.Entity.ServerPos.XYZ);

            ExecutionHasFailed = targetEntity == null;
            if (targetEntity != null)
            {
                Vec3f targetVec = new Vec3f();

                targetVec.Set(
                    (float)(targetEntity.ServerPos.X - vas.Entity.ServerPos.X),
                    (float)(targetEntity.ServerPos.Y - vas.Entity.ServerPos.Y),
                    (float)(targetEntity.ServerPos.Z - vas.Entity.ServerPos.Z)
                );

                vas.Entity.ServerPos.Yaw = (float)Math.Atan2(targetVec.X, targetVec.Z);
            }
        }

        private Entity getTarget(ICoreAPI api, Vec3d fromPos)
        {
            var ep = api.ModLoader.GetModSystem<EntityPartitioning>();
            var targetEntity = ep.GetNearestEntity(fromPos, searchRange, (e) => e.WildCardMatch(targetEntityCode), EnumEntitySearchType.Creatures);
            return targetEntity;
        }

        public override string ToString()
        {
            return "Look at nearest entity " + targetEntityCode.ToShortString() + " within " + searchRange + " blocks";
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Search Range", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "searchRange")

                .AddStaticText("Entity Code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "targetEntityCode")
            ;

            singleComposer.GetTextInput("searchRange").SetValue(searchRange);
            singleComposer.GetTextInput("targetEntityCode").SetValue(targetEntityCode);
        }

        public override IEntityAction Clone()
        {
            return new LookatEntityAction(vas, targetEntityCode, searchRange);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            searchRange = singleComposer.GetTextInput("searchRange").GetText().ToFloat();
            targetEntityCode = new AssetLocation(singleComposer.GetTextInput("targetEntityCode").GetText());
            return true;
        }

        public override void OnVisualize(ActivityVisualizer visualizer)
        {
            var target = getTarget(visualizer.Api, visualizer.CurrentPos);
            if (target != null)
            {
                visualizer.LineTo(visualizer.CurrentPos, target.Pos.XYZ.Add(0, 0.5, 0), ColorUtil.ColorFromRgba(0, 0, 255, 255));
            }
        }
    }
}
