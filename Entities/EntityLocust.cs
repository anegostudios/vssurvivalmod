using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
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
            Block belowBlock = World.BlockAccessor.GetBlock((int)LocalPos.X, (int)(LocalPos.Y - 0.05f), (int)LocalPos.Z);
            Block insideblock = World.BlockAccessor.GetBlock((int)LocalPos.X, (int)(LocalPos.Y + 0.01f), (int)LocalPos.Z);

            double multiplier = (servercontrols.Sneak ? GlobalConstants.SneakSpeedMultiplier : 1.0) * (servercontrols.Sprint ? GlobalConstants.SprintSpeedMultiplier : 1.0);

            if (FeetInLiquid) multiplier /= 2.5;

            double mul1 = belowBlock.Code.Path.Contains("metalspike") ? 1 : belowBlock.WalkSpeedMultiplier;
            double mul2 = insideblock.Code.Path.Contains("metalspike") ? 1 : insideblock.WalkSpeedMultiplier;

            multiplier *= mul1 * mul2;
            
            // Apply walk speed modifiers.
            var attribute = WatchedAttributes.GetTreeAttribute("walkSpeedModifiers");
            if (attribute?.Count > 0)
            {
                // Enumerate over all values in this attribute as tree attributes, then
                // multiply their "Value" properties together with the current multiplier.
                multiplier *= attribute.Values.Cast<ITreeAttribute>()
                    .Aggregate(1.0F, (current, modifier) => current * modifier.GetFloat("Value"));
            }

            return multiplier;
        }
    }
}
