using System;
using System.Collections.Generic;

namespace SuperCircuit64.Elements;

public sealed class Resistor : ICircuitElement
{
    public int NodeA { get; }
    public int NodeB { get; }

    public double Resistance { get; set; } // TODO: validate - the setter bypasses the constructor's positive-value check.

    public Resistor(int nodeA, int nodeB, double resistance)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(resistance, 0.0);

        NodeA = nodeA;
        NodeB = nodeB;
        Resistance = resistance;
    }

    public IEnumerable<int> Nodes
    {
        get
        {
            yield return NodeA;
            yield return NodeB;
        }
    }

    public void Stamp(MnaBuilder builder, double time, double deltaTime)
        => builder.AddConductance(NodeA, NodeB, 1.0 / Resistance);
}
