using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using System;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ModSystemSwoopDev : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;
        ICoreServerAPI sapi;

        Vec3d[] points = new Vec3d[4];
        bool plot;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            sapi.ChatCommands
                .GetOrCreate("dev")
                .BeginSub("swoop")
                    .WithDesc("Bezier test thing")
                  .BeginSub("start1")
                    .WithDesc("Set a bezier point")
                    .WithAlias("end1", "start2", "end2")
                    .HandleWith(cmdPoint)
                .EndSub()
                  .BeginSub("plot")
                    .WithDesc("Plot bezier curves with particles")
                    .HandleWith((args) => { plot = !plot; return TextCommandResult.Success("plot now " + (plot ? "on":"off")); })
                  .EndSub()
                .EndSub()
            ;

            sapi.Event.RegisterGameTickListener(onTick1s, 1001, 12);
        }

        private TextCommandResult cmdPoint(TextCommandCallingArgs args)
        {
            string[] names = new string[] { "start1", "end1", "start2", "end2" };
            points[names.IndexOf(args.SubCmdCode)] = args.Caller.Pos;
            return TextCommandResult.Success("ok set");
        }

        Vec3f zero = Vec3f.Zero;

        private void onTick1s(float dt)
        {
            if (points[0] == null || points[1] == null || points[2] == null || points[2] == null) return;
            if (!plot) return;

            var delta1 = points[1] - points[0];
            var delta2 = points[3] - points[2];

            int its = 50;
            for (int i = 0; i < its; i++)
            {
                double p = (double)i / its;

                var mid1 = points[0] + p * delta1;
                var mid2 = points[2] + p * delta2;

                var bez = (1 - p) * mid1 + p * mid2;

                sapi.World.SpawnParticles(1, ColorUtil.WhiteArgb, bez, bez, zero, zero, 1, 0, 1);
            }

            sapi.World.SpawnParticles(1, ColorUtil.ColorFromRgba(0,0,128,255), points[0], points[0], zero, zero, 1, 0, 1);
            sapi.World.SpawnParticles(1, ColorUtil.ColorFromRgba(0,0,255,255), points[1], points[1], zero, zero, 1, 0, 1);

            sapi.World.SpawnParticles(1, ColorUtil.ColorFromRgba(0, 128, 0, 255), points[2], points[2], zero, zero, 1, 0, 1);
            sapi.World.SpawnParticles(1, ColorUtil.ColorFromRgba(0, 255, 0, 255), points[3], points[3], zero, zero, 1, 0, 1);
        }
    }
}
