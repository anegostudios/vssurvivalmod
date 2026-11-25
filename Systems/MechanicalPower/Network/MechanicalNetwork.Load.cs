using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    // Mechanical load n' stress
    // - ✔ Remember real connections during discovery and loops included
    // - ✔ Work out the torque each part actually carries, not just the network total!
    // - ✔ Show it in the entity debug info
    // - Hosted gears and shaft sections as their own parts
    // - Load ratings, strain, and popping an overloaded part off as an item
    // - Warnings before anything breaks: info text, creaking, wobble, dust
    // - Speed limits and overheating/fires for wooden parts
    public partial class MechanicalNetwork
    {
        // updateNetwork() keeps a 'stalled' system creeping at 0.000001, so that counts as stopped
        const double stoppedSpeed = 0.0000011;
        const double linkageDerivativeStep = 0.0001;

        readonly Dictionary<BEBehaviorMPBase, MechanicalPowerPart> loadParts = [];
        readonly List<(MechanicalPowerPart A, MechanicalPowerPart B)> loadConnections = [];
        readonly Dictionary<BlockPos, float> clientLoads = [];
        MechanicalLoadGraph loadGraph = new();
        bool loadGraphDirty;
        bool loadSamplesComplete;
        double[] loadInputs = [];
        double[] loadRatios = [];
        double[] loadAccelerations = [];

        public bool LoadsValid { get; private set; }

        public float GetLoad(BEBehaviorMPBase device)
        {
            return loadParts.TryGetValue(device, out MechanicalPowerPart? part) ? part.Load : clientLoads.GetValueOrDefault(device.Position);
        }

        // Both devices need their geared ratio for this network first
        public void ConnectLoad(BEBehaviorMPBase? from, BEBehaviorMPBase to)
        {
            if (from == null || from == to || from.GearedRatio == 0 || to.GearedRatio == 0 || mechanicalPowerMod.Api.Side != EnumAppSide.Server) return;

            MechanicalPowerPart a = getLoadPart(from);
            MechanicalPowerPart b = getLoadPart(to);
            if (loadGraph.Connect(a.Index, b.Index, getCompliance(a, b))) loadConnections.Add((a, b));
        }

        public void ClearLoadTopology()
        {
            loadParts.Clear();
            loadConnections.Clear();
            loadGraph = new();
            loadGraphDirty = false;
            LoadsValid = false;
        }

        void removeLoadPart(IMechanicalPowerNode node)
        {
            if (node is not BEBehaviorMPBase device || !loadParts.Remove(device)) return;

            loadConnections.RemoveAll(connection => connection.A.Owner == device || connection.B.Owner == device);
            loadGraphDirty = true;
            LoadsValid = false;
        }

        MechanicalPowerPart getLoadPart(BEBehaviorMPBase device)
        {
            if (loadGraphDirty) rebuildLoadGraph();
            if (loadParts.TryGetValue(device, out MechanicalPowerPart? part)) return part;

            part = new(device, loadGraph.AddNode());
            loadParts.Add(device, part);
            return part;
        }

        void rebuildLoadGraph()
        {
            loadGraph = new();
            foreach (MechanicalPowerPart part in loadParts.Values) part.Index = loadGraph.AddNode();
            foreach (var (a, b) in loadConnections) loadGraph.Connect(a.Index, b.Index, getCompliance(a, b));
            loadGraphDirty = false;
        }

        // Every connection is equally springy for now and scaled by its gearing
        static double getCompliance(MechanicalPowerPart a, MechanicalPowerPart b)
        {
            double ra = a.Owner.GearedRatio;
            double rb = b.Owner.GearedRatio;
            return 1 / (ra * ra) + 1 / (rb * rb);
        }

        void beginLoadSamples()
        {
            loadSamplesComplete = true;
            foreach (MechanicalPowerPart part in loadParts.Values)
            {
                part.Torque = 0;
                part.Resistance = 0;
                part.Inertia = 0;
            }
        }

        // Reuse the speed update's numbers, calling GetTorque() again would advance rotors twice
        void sampleLoad(IMechanicalPowerNode node, float torque, float resistance, float inertia)
        {
            if (node is not BEBehaviorMPBase device)
            {
                loadSamplesComplete = false;
                return;
            }

            MechanicalPowerPart part = getLoadPart(device);
            part.Torque = torque;
            part.Resistance = resistance;
            part.Inertia = inertia;
        }

        void updateLoads(float shaftSpeed, float dt)
        {
            LoadsValid = false;
            if (!Valid || !fullyLoaded || !loadSamplesComplete)
            {
                clearLoads();
                return;
            }
            if (loadGraphDirty) rebuildLoadGraph();
            if (loadInputs.Length != loadGraph.Count)
            {
                loadInputs = new double[loadGraph.Count];
                loadRatios = new double[loadGraph.Count];
                loadAccelerations = new double[loadGraph.Count];
            }

            double phase = angle;
            foreach (MechanicalPowerPart part in loadParts.Values)
            {
                if (part.Owner.AngleTransform != null && Math.Abs(part.GetRatio(phase)) < 0.00001)
                {
                    // A rocking linkage has no speed at dead centre, so look just past it
                    phase += linkageDerivativeStep;
                    break;
                }
            }

            double torque = 0;
            double capacity = 0;
            double inertia = 0;
            double changingRatioTorque = 0;
            foreach (MechanicalPowerPart part in loadParts.Values)
            {
                double r = part.GetRatio(phase);
                if (r == 0 || !double.IsFinite(r))
                {
                    clearLoads();
                    return;
                }

                double ratioChange = part.Owner.AngleTransform == null ? 0 : (part.GetRatio(phase + linkageDerivativeStep) - part.GetRatio(phase - linkageDerivativeStep)) / (2 * linkageDerivativeStep);
                double acceleration = ratioChange * shaftSpeed * shaftSpeed * 0.5;
                loadRatios[part.Index] = r;
                loadAccelerations[part.Index] = acceleration;
                changingRatioTorque += part.Inertia * r * acceleration;
                torque += r * part.Torque;
                capacity += getResistance(part, r, shaftSpeed);
                inertia += part.Inertia * r * r;
            }

            double direction = Math.Abs(shaftSpeed) <= stoppedSpeed ? Math.Sign(torque) : Math.Sign(shaftSpeed);
            double resistanceScale = getResistanceScale(torque, capacity, inertia, shaftSpeed, dt / torqueUpdateInterval);
            double shaftAcceleration = inertia > 0 ? (torque - direction * capacity * resistanceScale - changingRatioTorque) / inertia : 0;
            foreach (MechanicalPowerPart part in loadParts.Values)
            {
                int i = part.Index;
                double r = loadRatios[i];
                loadInputs[i] = r * part.Torque - direction * getResistance(part, r, shaftSpeed) * resistanceScale - part.Inertia * r * (r * shaftAcceleration + loadAccelerations[i]);
            }

            LoadsValid = loadGraph.Solve(loadInputs);
            if (!LoadsValid)
            {
                clearLoads();
                return;
            }

            foreach (MechanicalPowerPart part in loadParts.Values)
            {
                part.Load = (float)Math.Max(loadGraph.GetLoad(part.Index) / Math.Abs(loadRatios[part.Index]), part.Resistance * resistanceScale);
            }
        }

        static double getResistance(MechanicalPowerPart part, double r, double shaftSpeed)
        {
            return Math.Abs(r) * (part.Resistance + r * r * shaftSpeed * shaftSpeed / 1000);
        }

        // Brakes and machines only push back as hard as it takes to hold or slow the train
        static double getResistanceScale(double torque, double capacity, double inertia, double speed, double timeStep)
        {
            if (capacity == 0) return 0;
            if (Math.Abs(speed) <= stoppedSpeed) return Math.Min(1, Math.Abs(torque) / capacity);

            double stoppingTorque = Math.Sign(speed) * torque + inertia * Math.Abs(speed) / timeStep;
            return Math.Clamp(stoppingTorque / capacity, 0, 1);
        }

        void clearLoads()
        {
            LoadsValid = false;
            foreach (MechanicalPowerPart part in loadParts.Values) part.Load = 0;
        }

        MechanicalPartLoad[] getPartLoads()
        {
            if (!LoadsValid) return [];

            MechanicalPartLoad[] loads = new MechanicalPartLoad[loadParts.Count];
            int i = 0;
            foreach (MechanicalPowerPart part in loadParts.Values) loads[i++] = new() { Position = part.Owner.Position, Load = part.Load };
            return loads;
        }

        void setClientLoads(MechanicalPartLoad[]? loads)
        {
            clientLoads.Clear();
            if (loads == null) return;
            foreach (MechanicalPartLoad load in loads) clientLoads[load.Position] = load.Load;
        }
    }
}
