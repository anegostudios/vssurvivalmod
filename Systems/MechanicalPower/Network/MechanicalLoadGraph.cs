using System;
using System.Collections.Generic;

namespace Vintagestory.GameContent.Mechanics
{
    public class MechanicalLoadGraph
    {
        readonly List<(int A, int B, double Conductance)> edges = [];
        readonly List<List<int>> neighbours = [];
        readonly HashSet<(int, int)> connections = [];
        int[] leaves = [];
        int[] parentEdges = [];
        bool[] core = [];
        bool[] anchors = [];
        double[] demand = [];
        double[] potential = [];
        double[] residual = [];
        double[] direction = [];
        double[] product = [];
        double[] diagonal = [];
        double[] loads = [];
        bool dirty = true;

        public int Count => neighbours.Count;

        public int AddNode()
        {
            neighbours.Add([]);
            dirty = true;
            return Count - 1;
        }

        public bool Connect(int a, int b, double compliance)
        {
            if (a == b) return false;
            if (!(compliance > 0) || !double.IsFinite(compliance)) throw new ArgumentOutOfRangeException(nameof(compliance));
            if (!connections.Add((Math.Min(a, b), Math.Max(a, b)))) return false;

            neighbours[a].Add(edges.Count);
            neighbours[b].Add(edges.Count);
            edges.Add((a, b, 1 / compliance));
            dirty = true;
            return true;
        }

        public double GetLoad(int node) => loads[node];

        // All torques are measured on the network's base shaft
        public bool Solve(ReadOnlySpan<double> input)
        {
            if (input.Length != Count) throw new ArgumentException("One torque input is required per load node.", nameof(input));
            if (dirty) prepare();
            input.CopyTo(demand);
            Array.Clear(loads);
            Array.Clear(potential);

            double scale = 0;
            foreach (double value in input)
            {
                if (!double.IsFinite(value)) return false;
                scale += Math.Abs(value);
            }
            double tolerance = Math.Max(1e-24, scale * 1e-10);

            foreach (int leaf in leaves)
            {
                int edge = parentEdges[leaf];
                if (edge < 0)
                {
                    if (Math.Abs(demand[leaf]) > tolerance) return false;
                    continue;
                }
                int parent = otherEnd(edge, leaf);
                double torque = demand[leaf];
                loads[leaf] = Math.Max(loads[leaf], Math.Abs(torque));
                loads[parent] = Math.Max(loads[parent], Math.Abs(torque));
                demand[parent] += torque;
            }

            Array.Clear(residual);
            double norm = 0;
            for (int i = 0; i < Count; i++)
            {
                if (!core[i] || anchors[i]) continue;
                residual[i] = demand[i];
                direction[i] = residual[i] / diagonal[i];
                norm += residual[i] * direction[i];
            }

            bool converged = norm <= tolerance * tolerance;
            for (int iteration = 0; !converged && iteration < Count * 2; iteration++)
            {
                multiply(direction, product);
                double denominator = 0;
                for (int i = 0; i < Count; i++) denominator += direction[i] * product[i];
                if (!(denominator > 0)) return false;
                double step = norm / denominator;
                double nextNorm = 0;
                double error = 0;
                for (int i = 0; i < Count; i++)
                {
                    if (!core[i] || anchors[i]) continue;
                    potential[i] += step * direction[i];
                    residual[i] -= step * product[i];
                    nextNorm += residual[i] * residual[i] / diagonal[i];
                    error = Math.Max(error, Math.Abs(residual[i]));
                }
                converged = error <= tolerance;
                double beta = nextNorm / norm;
                for (int i = 0; i < Count; i++)
                {
                    if (core[i] && !anchors[i]) direction[i] = residual[i] / diagonal[i] + beta * direction[i];
                }
                norm = nextNorm;
            }
            if (!converged) return false;

            Array.Clear(product);
            foreach (var edge in edges)
            {
                if (!core[edge.A] || !core[edge.B]) continue;
                double torque = edge.Conductance * (potential[edge.A] - potential[edge.B]);
                loads[edge.A] = Math.Max(loads[edge.A], Math.Abs(torque));
                loads[edge.B] = Math.Max(loads[edge.B], Math.Abs(torque));
                product[edge.A] += torque;
                product[edge.B] -= torque;
            }
            for (int i = 0; i < Count; i++)
            {
                if (core[i] && Math.Abs(product[i] - demand[i]) > tolerance * 4) return false;
            }
            return true;
        }

        void multiply(double[] source, double[] target)
        {
            Array.Clear(target);
            foreach (var edge in edges)
            {
                if (!core[edge.A] || !core[edge.B]) continue;
                double flow = edge.Conductance * (source[edge.A] - source[edge.B]);
                if (!anchors[edge.A]) target[edge.A] += flow;
                if (!anchors[edge.B]) target[edge.B] -= flow;
            }
        }

        int otherEnd(int edge, int node) => edges[edge].A == node ? edges[edge].B : edges[edge].A;

        void prepare()
        {
            int count = Count;
            core = new bool[count];
            anchors = new bool[count];
            parentEdges = new int[count];
            demand = new double[count];
            potential = new double[count];
            residual = new double[count];
            direction = new double[count];
            product = new double[count];
            diagonal = new double[count];
            loads = new double[count];
            int[] degree = new int[count];
            Queue<int> queue = new();
            List<int> peeled = [];
            for (int i = 0; i < count; i++)
            {
                core[i] = true;
                parentEdges[i] = -1;
                degree[i] = neighbours[i].Count;
                if (degree[i] <= 1) queue.Enqueue(i);
            }
            while (queue.TryDequeue(out int leaf))
            {
                if (!core[leaf]) continue;
                core[leaf] = false;
                peeled.Add(leaf);
                foreach (int edge in neighbours[leaf])
                {
                    int parent = otherEnd(edge, leaf);
                    if (!core[parent]) continue;
                    parentEdges[leaf] = edge;
                    if (--degree[parent] == 1) queue.Enqueue(parent);
                    break;
                }
            }
            leaves = peeled.ToArray();

            bool[] visited = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (!core[i] || visited[i]) continue;
                anchors[i] = true;
                visited[i] = true;
                queue.Enqueue(i);
                while (queue.TryDequeue(out int node))
                {
                    foreach (int edge in neighbours[node])
                    {
                        int other = otherEnd(edge, node);
                        if (!core[other]) continue;
                        diagonal[node] += edges[edge].Conductance;
                        if (visited[other]) continue;
                        visited[other] = true;
                        queue.Enqueue(other);
                    }
                }
            }
            dirty = false;
        }
    }
}
