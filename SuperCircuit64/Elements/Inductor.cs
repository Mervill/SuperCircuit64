using System;
using System.Collections.Generic;

namespace SuperCircuit64.Elements;

/// <summary>
/// Companion model: a conductance in parallel with a current source carrying history state from the
/// previous step. The conductance and current-source formulas depend on <see cref="Method"/>.
/// </summary>
public sealed class Inductor : ICircuitElement
{
    public int NodeA { get; }
    public int NodeB { get; }

    public double Inductance { get; set; } // TODO: validate - the setter bypasses the constructor's positive-value check.
    public IntegrationMethod Method { get; set; }

    private double _current;
    private double _voltage;

    public Inductor(int nodeA, int nodeB, double inductance, double initialCurrent = 0.0, IntegrationMethod method = IntegrationMethod.BackwardEuler)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(inductance);

        NodeA = nodeA;
        NodeB = nodeB;
        Inductance = inductance;
        Method = method;
        _current = initialCurrent;
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
    {
        double geq;
        double ieq;

        if (Method == IntegrationMethod.Trapezoidal)
        {
            geq = deltaTime / (2.0 * Inductance);
            ieq = _current + geq * _voltage;
        }
        else
        {
            geq = deltaTime / Inductance;
            ieq = _current;
        }

        builder.AddConductance(NodeA, NodeB, geq);
        builder.AddCurrentSource(NodeA, NodeB, ieq);
    }

    public void Commit(CircuitState state, double deltaTime)
    {
        double voltage = state.Voltage(NodeA) - state.Voltage(NodeB);

        if (Method == IntegrationMethod.Trapezoidal)
        {
            double geq = deltaTime / (2.0 * Inductance);
            _current += geq * (voltage + _voltage);
            _voltage = voltage;
        }
        else
        {
            double geq = deltaTime / Inductance;
            _current += geq * voltage;
        }
    }
}
