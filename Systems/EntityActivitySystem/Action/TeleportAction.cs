using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TeleportAction : EntityActionBase
    {
        [JsonProperty]
        public double TargetX { get; set; }
        [JsonProperty]
        public double TargetY { get; set; }
        [JsonProperty]
        public double TargetZ { get; set; }
        [JsonProperty]
        public double Yaw { get; set; }
        public override string Type => "teleport";


        public TeleportAction() { }
        public TeleportAction(EntityActivitySystem vas, double targetX, double targetY, double targetZ, double yaw)
        {
            this.vas = vas;
            this.TargetX = targetX;
            this.TargetY = targetY;
            this.TargetZ = targetZ;
            this.Yaw = yaw;
        }

        public TeleportAction(EntityActivitySystem vas)
        {
            this.vas = vas;
        }

        public override void Start(EntityActivity act)
        {
            vas.Entity.TeleportToDouble(TargetX + vas.ActivityOffset.X, TargetY + vas.ActivityOffset.Y, TargetZ + vas.ActivityOffset.Z);
            vas.Entity.Controls.StopAllMovement();
            vas.wppathTraverser.Stop();
            vas.Entity.ServerPos.Yaw = (float)Yaw;
            vas.Entity.Pos.Yaw = (float)Yaw;
            vas.Entity.BodyYaw = (float)Yaw;
            vas.Entity.BodyYawServer = (float)Yaw;

            vas.ClearNextActionDelay();
        }

        public override string ToString()
        {
            var x = TargetX;
            var y = TargetY;
            var z = TargetZ;
            if (vas != null)
            {
                x += vas.ActivityOffset.X;
                y += vas.ActivityOffset.Y;
                z += vas.ActivityOffset.Z;
            }

            return string.Format("Teleport to {0}/{1}/{2}", x, y, z);
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var bc = ElementBounds.Fixed(0, 0, 65, 20);
            var b = ElementBounds.Fixed(0, 0, 200, 20);
            singleComposer
                .AddStaticText("x/y/z Pos", CairoFont.WhiteDetailText(), bc)
                .AddTextInput(bc = bc.BelowCopy(0), null, CairoFont.WhiteDetailText(), "x")
                .AddTextInput(bc = bc.CopyOffsetedSibling(70), null, CairoFont.WhiteDetailText(), "y")
                .AddTextInput(bc = bc.CopyOffsetedSibling(70), null, CairoFont.WhiteDetailText(), "z")
                .AddSmallButton("Tp to", () => onClickTpTo(capi), bc = bc.CopyOffsetedSibling(70), EnumButtonStyle.Small)

                .AddSmallButton("Insert Player Pos", () => onClickPlayerPos(capi, singleComposer), b = b.FlatCopy().FixedUnder(bc), EnumButtonStyle.Small)


                .AddStaticText("Yaw (in radians)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15).WithFixedWidth(120))
                .AddTextInput(b = b.BelowCopy(0, 5), null, CairoFont.WhiteDetailText(), "yaw")
            ;

            var s = singleComposer;
            s.GetTextInput("x").SetValue(TargetX + "");
            s.GetTextInput("y").SetValue(TargetY + "");
            s.GetTextInput("z").SetValue(TargetZ + "");
            s.GetTextInput("yaw").SetValue(Yaw + "");
        }

        private bool onClickTpTo(ICoreClientAPI capi)
        {
            var x = TargetX;
            var y = TargetY;
            var z = TargetZ;
            if (vas != null)
            {
                x += vas.ActivityOffset.X;
                y += vas.ActivityOffset.Y;
                z += vas.ActivityOffset.Z;
            }
            capi.SendChatMessage(string.Format("/tp ={0} ={1} ={2}", x, y, z));
            return false;
        }

        private bool onClickPlayerPos(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var plrPos = capi.World.Player.Entity.Pos.XYZ;
            singleComposer.GetTextInput("x").SetValue("" + Math.Round(plrPos.X, 1));
            singleComposer.GetTextInput("y").SetValue("" + Math.Round(plrPos.Y, 1));
            singleComposer.GetTextInput("z").SetValue("" + Math.Round(plrPos.Z, 1));

            singleComposer.GetTextInput("yaw").SetValue("" + Math.Round(capi.World.Player.Entity.ServerPos.Yaw - GameMath.PIHALF, 1));
            return true;
        }


        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer s)
        {
            this.TargetX = s.GetTextInput("x").GetText().ToDouble();
            this.TargetY = s.GetTextInput("y").GetText().ToDouble();
            this.TargetZ = s.GetTextInput("z").GetText().ToDouble();
            this.Yaw = s.GetTextInput("yaw").GetText().ToDouble();
            return true;
        }

        public override IEntityAction Clone()
        {
            return new TeleportAction(vas, TargetX, TargetY, TargetZ, Yaw);
        }

        public override void OnVisualize(ActivityVisualizer visualizer)
        {
            var target = new Vec3d(TargetX, TargetY , TargetZ);
            if (vas != null)
            {
                target.Add(vas.ActivityOffset);
            }
            visualizer.GoTo(target, ColorUtil.ColorFromRgba(255,255,0,255));
        }
    }
}
