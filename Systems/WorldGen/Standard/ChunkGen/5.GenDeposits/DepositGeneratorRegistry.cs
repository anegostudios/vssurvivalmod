using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.ServerMods
{
    public static class DepositGeneratorRegistry
    {
        static Dictionary<string, Type> Generators = new Dictionary<string, Type>();


        static DepositGeneratorRegistry()
        {
            RegisterDepositGenerator<FollowSurfaceDiscGenerator>("disc-followsurface");
            RegisterDepositGenerator<AnywhereDiscGenerator>("disc-anywhere");
            RegisterDepositGenerator<FollowSealevelDiscGenerator>("disc-followsealevel");
            RegisterDepositGenerator<FollowSurfaceBelowDiscGenerator>("disc-followsurfacebelow");
            RegisterDepositGenerator<ChildDepositGenerator>("childdeposit-pointcloud");

            RegisterDepositGenerator<AlluvialDepositGenerator>("disc-alluvialdeposit");
        }

        public static void RegisterDepositGenerator<T>(string code) where T : DepositGeneratorBase
        {
            Generators[code] = typeof(T);
        }

        public static DepositGeneratorBase CreateGenerator(string code, JsonObject attributes, params object[] args)
        {
            if (!Generators.ContainsKey(code)) return null;

            DepositGeneratorBase generator = Activator.CreateInstance(Generators[code], args) as DepositGeneratorBase;
            JsonUtil.Populate(attributes.Token, generator);
            generator.Init();

            return generator;
        }

    }
}
