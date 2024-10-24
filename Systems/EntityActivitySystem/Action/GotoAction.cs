using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{


    [JsonObject(MemberSerialization.OptIn)]
    public class GotoAction : EntityActionBase
    {
        [JsonProperty]
        public double targetX { get { return Target.X; } set { Target.X = value; } }
        [JsonProperty]
        public double targetY { get { return Target.Y; } set { Target.Y = value; } }
        [JsonProperty]
        public double targetZ { get { return Target.Z; } set { Target.Z = value; } }
        [JsonProperty]
        public float AnimSpeed=1;
        [JsonProperty]
        public float WalkSpeed=0.02f;
        [JsonProperty]
        public string AnimCode = "walk";
        [JsonProperty]
        public bool Astar = true;
        [JsonProperty]
        public float Radius = 0;
        public override string Type => "goto";

        
        public Vec3d Target = new Vec3d();

        bool done;

        public GotoAction() { }
        public GotoAction(EntityActivitySystem vas, Vec3d target, bool astar, string animCode = "walk", float walkSpeed = 0.02f, float animSpeed = 1, float radius = 0)
        {
            this.vas = vas;
            this.Astar = astar;
            this.Target = target;
            this.AnimSpeed = animSpeed;
            this.AnimCode = animCode;
            this.WalkSpeed = walkSpeed;
            this.Radius = radius;
        }

        public GotoAction(EntityActivitySystem vas)
        {
            this.vas = vas;
        }

        public override void Start(EntityActivity act)
        {
            done = false;
            ExecutionHasFailed = false;

            var hereTarget = Target.Clone();
            if (Radius > 0)
            {
                float alpha = (float)vas.Entity.World.Rand.NextDouble() * GameMath.TWOPI;
                hereTarget.X += Math.Sin(alpha) * Radius;
                hereTarget.Z += Math.Cos(alpha) * Radius;
            }

            EnumAICreatureType ct = EnumAICreatureType.Default;
            var aicreaturetype = vas.Entity.Properties.Server.Attributes.GetString("aiCreatureType", "Humanoid");
            if (Enum.TryParse(aicreaturetype, out EnumAICreatureType ect)) ct = ect;
            

            if (Astar)
            {
                int tries = 4;
                while (tries-- > 0 && !vas.wppathTraverser.NavigateTo(hereTarget, WalkSpeed, OnDone, OnStuck, OnNoPath, 0, ct)) { }
            }
            else
            {
                vas.linepathTraverser.NavigateTo(hereTarget, WalkSpeed, OnDone, OnStuck, null, 0, ct);
            }


            if (AnimSpeed != 0.02f)
            {
                vas.Entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = AnimCode, Code = AnimCode, AnimationSpeed = AnimSpeed }.Init());
            }
            else
            {

                if (!vas.Entity.AnimManager.StartAnimation(AnimCode))
                {
                    vas.Entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = AnimCode, Code = AnimCode, AnimationSpeed = AnimSpeed }.Init());
                }

            }

            vas.Entity.Controls.Sprint = AnimCode == "run" || AnimCode == "sprint";
        }

        private void OnNoPath()
        {
            var pos = vas.Entity.ServerPos;
            if (vas.Debug) vas.Entity.World.Logger.Debug("ActivitySystem entity {0} action goto from {1}/{2}/{3} to {4}/{5}/{6} failed, found no A* path to target.", vas.Entity.EntityId, pos.X, pos.Y, pos.Z, targetX, targetY, targetZ);
            ExecutionHasFailed = true;
            Finish();
        }


        private void OnStuck()
        {
            if (vas.Debug) vas.Entity.World.Logger.Debug("ActivitySystem entity {0} GotoAction, OnStuck() called", vas.Entity.EntityId);
            ExecutionHasFailed = true;
            Finish();
        }
        public override void Cancel()
        {
            Finish();
        }

        public override void Finish()
        {
            if (vas.Debug) vas.Entity.World.Logger.Debug("ActivitySystem entity {0} GotoAction, Stop() called", vas.Entity.EntityId);
            vas.linepathTraverser.Stop();
            vas.wppathTraverser.Stop();
            vas.Entity.AnimManager.StopAnimation(AnimCode);
            vas.Entity.Controls.StopAllMovement();
        }

        private void OnDone()
        {
            if (vas.Debug) vas.Entity.World.Logger.Debug("ActivitySystem entity {0} GotoAction, OnDone() called", vas.Entity.EntityId);
            vas.Entity.AnimManager.StopAnimation(AnimCode);
            vas.Entity.Controls.StopAllMovement();
            done = true;
        }

        public override bool IsFinished()
        {
            return done || ExecutionHasFailed;
        }

        public override string ToString()
        {
            return string.Format("{0}Goto {1}/{2}/{3} (walkSpeed {4}, animspeed {5})", Astar ? "A* " : "", Target.X, Target.Y, Target.Z, WalkSpeed, AnimSpeed);
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

                .AddSmallButton("Tp to", () => { capi.SendChatMessage(string.Format("/tp ={0} ={1} ={2}", targetX, targetY, targetZ)); return false; }, bc = bc.CopyOffsetedSibling(70), EnumButtonStyle.Small)

                .AddSmallButton("Insert Player Pos", () => onClickPlayerPos(capi, singleComposer), b = b.FlatCopy().FixedUnder(bc), EnumButtonStyle.Small)

                .AddStaticText("Goto animation code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddTextInput(b = b.BelowCopy(), null, CairoFont.WhiteDetailText(), "animCode")

                .AddStaticText("Animation Speed", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy().WithFixedHeight(25), null, CairoFont.WhiteDetailText(), "animSpeed")

                .AddStaticText("Walk Speed", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy().WithFixedHeight(25), null, CairoFont.WhiteDetailText(), "walkSpeed")

                .AddSwitch(null, b = b.BelowCopy(0, 15).WithFixedWidth(25), "astar", 25)
                .AddStaticText("A* Pathfinding", CairoFont.WhiteDetailText(), b = b.RightCopy(10, 5).WithFixedWidth(100))

                .AddStaticText("Random target offset radius", CairoFont.WhiteDetailText(), b = b.BelowCopy(-35, 10).WithFixedWidth(250))
                .AddNumberInput(b = b.BelowCopy().WithFixedSize(100,25), null, CairoFont.WhiteDetailText(), "radius")
            ;

            var s = singleComposer;
            s.GetTextInput("x").SetValue(Target?.X + "");
            s.GetTextInput("y").SetValue(Target?.Y + "");
            s.GetTextInput("z").SetValue(Target?.Z + "");
            s.GetSwitch("astar").On = this.Astar;
            s.GetTextInput("animCode").SetValue(AnimCode);
            s.GetNumberInput("animSpeed").SetValue(AnimSpeed + "");
            s.GetNumberInput("walkSpeed").SetValue(WalkSpeed + "");
            s.GetNumberInput("radius").SetValue(Radius + "");
        }

        private bool onClickPlayerPos(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var plrPos = capi.World.Player.Entity.Pos.XYZ;
            singleComposer.GetTextInput("x").SetValue("" + Math.Round(plrPos.X, 1));
            singleComposer.GetTextInput("y").SetValue("" + Math.Round(plrPos.Y, 1));
            singleComposer.GetTextInput("z").SetValue("" + Math.Round(plrPos.Z, 1));
            return true;
        }

        
        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer s)
        {
            this.Target = new Vec3d(s.GetTextInput("x").GetText().ToDouble(), s.GetTextInput("y").GetText().ToDouble(), s.GetTextInput("z").GetText().ToDouble());
            this.Astar = s.GetSwitch("astar").On;
            this.AnimCode = s.GetTextInput("animCode").GetText();
            this.AnimSpeed = s.GetNumberInput("animSpeed").GetText().ToFloat();
            this.WalkSpeed = s.GetNumberInput("walkSpeed").GetText().ToFloat();
            this.Radius = s.GetNumberInput("radius").GetText().ToFloat();
            return true;
        }

        public override IEntityAction Clone()
        {
            return new GotoAction(vas, Target, Astar, AnimCode, WalkSpeed, AnimSpeed, Radius);
        }

        public override void OnVisualize(ActivityVisualizer visualizer)
        {
            visualizer.GoTo(Target);
        }
    }
}
