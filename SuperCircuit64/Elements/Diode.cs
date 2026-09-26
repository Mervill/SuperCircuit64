using System;
using System.Collections.Generic;

namespace SuperCircuit64.Elements;

public sealed class Diode : ICircuitElement
{
    public const double DefaultSaturationCurrent = 1e-14;
    public const double DefaultIdealityFactor = 1.0;
    public const double DefaultMinimumConductance = 1e-9;

    public int NodeAnode { get; }
    public int NodeCathode { get; }

    public double SaturationCurrent { get; set; } // TODO: validate - the setter bypasses the constructor's positive-value check.
    public double IdealityFactor { get; set; } // TODO: validate - the setter bypasses the constructor's positive-value check.
    public double MinimumConductance { get; set; } // TODO: validate - the setter bypasses the constructor's non-negative check.

    /// <summary>
    /// Thermal voltage scaled by <see cref="IdealityFactor"/>.
    /// </summary>
    private double Vt
        => IdealityFactor * PhysicalConstants.ThermalVoltage;

    /// <summary>
    /// Forward voltage past which <see cref="SpiceMath.Pnjlim"/> starts damping the Newton step.
    /// </summary>
    private double VCrit
        => Vt * Math.Log(Vt / (SaturationCurrent * Math.Sqrt(2.0)));

    private double _voltage;

    private double _committedVoltage;

    public bool IsNonlinear => true;

    public Diode(int nodeAnode, int nodeCathode, double saturationCurrent = DefaultSaturationCurrent, double idealityFactor = DefaultIdealityFactor, double minimumConductance = DefaultMinimumConductance)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(saturationCurrent);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idealityFactor);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumConductance);

        NodeAnode = nodeAnode;
        NodeCathode = nodeCathode;
        SaturationCurrent = saturationCurrent;
        IdealityFactor = idealityFactor;
        MinimumConductance = minimumConductance;
    }

    public IEnumerable<int> Terminals
    {
        get
        {
            yield return NodeAnode;
            yield return NodeCathode;
        }
    }

    public void Stamp(MnaBuilder builder, double time, double deltaTime)
    {
        double junctionCurrent = SaturationCurrent * (Math.Exp(_voltage / Vt) - 1.0);
        double geq = (junctionCurrent + SaturationCurrent) / Vt + MinimumConductance;

        double ieq = geq * _voltage - (junctionCurrent + MinimumConductance * _voltage);

        builder.AddConductance(NodeAnode, NodeCathode, geq);
        builder.AddCurrentSource(NodeCathode, NodeAnode, ieq);
    }

    public bool Relinearize(CircuitState state)
    {
        double proposed = state.Voltage(NodeAnode) - state.Voltage(NodeCathode);
        double limited = SpiceMath.Pnjlim(proposed, _voltage, Vt, VCrit, out bool wasLimited);

        bool converged = !wasLimited && Circuit.HasConverged(limited, _voltage);
        _voltage = limited;
        return converged;
    }

    public void Commit(CircuitState state, double deltaTime)
        => _committedVoltage = _voltage;

    public void Rollback()
        => _voltage = _committedVoltage;

    public double Current(CircuitState state)
    {
        double voltage = state.Voltage(NodeAnode) - state.Voltage(NodeCathode);
        return SaturationCurrent * (Math.Exp(voltage / Vt) - 1.0) + MinimumConductance * voltage;
    }
}
