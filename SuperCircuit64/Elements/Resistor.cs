using System;
using System.Collections.Generic;

namespace SuperCircuit64.Elements;

/// <summary>
/// An ideal linear resistor between <see cref="NodeA"/> and <see cref="NodeB"/>, stamped as the
/// conductance 1 / <see cref="Resistance"/>.
/// </summary>
public sealed class Resistor : ICircuitElement
{
    public int NodeA { get; }
    public int NodeB { get; }

    public double Resistance { get; set; } // TODO: validate - the setter bypasses the constructor's positive-value check.

    public Resistor(int nodeA, int nodeB, double resistance)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resistance);

        NodeA = nodeA;
        NodeB = nodeB;
        Resistance = resistance;
    }

    public IEnumerable<int> Terminals
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
