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
    public class EntityLocust : EntityGlowingAgent
    {

        /// <summary>
        /// Gets the walk speed multiplier.
        /// </summary>
        /// <param name="groundDragFactor">The amount of drag provided by the current ground. (Default: 0.3)</param>
        public override double GetWalkSpeedMultiplier(double groundDragFactor = 0.3)
        {
            Block belowBlock = World.BlockAccessor.GetBlock((int)SidedPos.X, (int)(SidedPos.Y - 0.05f), (int)SidedPos.Z);
            Block insideblock = World.BlockAccessor.GetBlock((int)SidedPos.X, (int)(SidedPos.Y + 0.01f), (int)SidedPos.Z);

            double multiplier = (servercontrols.Sneak ? GlobalConstants.SneakSpeedMultiplier : 1.0) * (servercontrols.Sprint ? GlobalConstants.SprintSpeedMultiplier : 1.0);

            if (FeetInLiquid) multiplier /= 2.5;

            double mul1 = belowBlock.Code == null || belowBlock.Code.Path.Contains("metalspike") ? 1 : belowBlock.WalkSpeedMultiplier;
            double mul2 = insideblock.Code == null || insideblock.Code.Path.Contains("metalspike") ? 1 : insideblock.WalkSpeedMultiplier;

            multiplier *= mul1 * mul2;

            // Apply walk speed modifiers.
            multiplier *= GameMath.Clamp(Stats.GetBlended("walkspeed"), 0, 999);

            return multiplier;
        }
    }
}
