using System;
using System.Collections.Generic;
using MineVent.Domain;
using MineVent.Network;

namespace MineVent.Solvers
{
    public class HardyCrossSolver
    {
        public SolveResult Solve(MineNetwork network, float tolerance = 0.0001f, int maxIterations = 100)
        {
            var result = new SolveResult();

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                float maxDelta = 0f;

                foreach (var loop in network.Loops)
                {
                    float numerator = 0f;
                    float denominator = 0f;

                    foreach (var (airwayId, sign) in loop.AirwaySigns)
                    {
                        var airway = network.GetAirway(airwayId);
                        if (airway == null)
                            continue;

                        numerator += sign * (airway.R * airway.Q * MathF.Abs(airway.Q) - airway.FanPressure);
                        denominator += 2f * airway.R * MathF.Abs(airway.Q);
                    }

                    if (MathF.Abs(denominator) < 0.000001f)
                        continue;

                    float deltaQ = -numerator / denominator;
                    maxDelta = MathF.Max(maxDelta, MathF.Abs(deltaQ));

                    foreach (var (airwayId, sign) in loop.AirwaySigns)
                    {
                        var airway = network.GetAirway(airwayId);
                        if (airway == null)
                            continue;

                        airway.Q += sign * deltaQ;
                    }
                }

                result.Iterations = iteration + 1;

                if (maxDelta <= tolerance)
                {
                    result.Converged = true;
                    return result;
                }
            }

            result.Converged = false;
            return result;
        }
    }

    public class SolveResult
    {
        public bool Converged { get; set; }
        public int Iterations { get; set; }
    }
}