using System;
using System.Collections.Generic;

namespace SuperCircuit64.Elements;

/// <summary>
/// Companion model: a conductance in parallel with a current source carrying history state from the
/// previous step. The conductance and current-source formulas depend on <see cref="Method"/>.
/// </summary>
public sealed class Capacitor : ICircuitElement
{
    public int NodeA { get; }
    public int NodeB { get; }

    public double Capacitance { get; set; } // TODO: validate - the setter bypasses the constructor's positive-value check.
    public IntegrationMethod Method { get; set; }

    private double _voltage;
    private double _current;

    public Capacitor(int nodeA, int nodeB, double capacitance, double initialVoltage = 0.0, IntegrationMethod method = IntegrationMethod.BackwardEuler)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacitance);

        NodeA = nodeA;
        NodeB = nodeB;
        Capacitance = capacitance;
        Method = method;
        _voltage = initialVoltage;
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
    {
        double geq;
        double ieq;

        if (Method == IntegrationMethod.Trapezoidal)
        {
            geq = 2.0 * Capacitance / deltaTime;
            ieq = geq * _voltage + _current;
        }
        else
        {
            geq = Capacitance / deltaTime;
            ieq = geq * _voltage;
        }

        builder.AddConductance(NodeA, NodeB, geq);
        builder.AddCurrentSource(NodeB, NodeA, ieq);
    }

    public void Commit(CircuitState state, double deltaTime)
    {
        double voltage = state.Voltage(NodeA) - state.Voltage(NodeB);

        if (Method == IntegrationMethod.Trapezoidal)
        {
            double geq = 2.0 * Capacitance / deltaTime;
            _current = geq * (voltage - _voltage) - _current;
        }

        _voltage = voltage;
    }
}
