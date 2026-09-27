using System;
using System.Collections.Generic;

namespace SuperCircuit64.Elements;

/// <summary>
/// Shockley-equation companion model: a conductance geq = dI/dV in parallel with a current source,
/// linearized around the previous Newton-Raphson solution. Nonlinear, so <see cref="Circuit.Step"/>
/// iterates <see cref="Stamp"/>/<see cref="Relinearize"/> until the linearization point converges.
/// The junction is shunted by <see cref="MinimumConductance"/> so that a reverse-biased diode still
/// leaves a finite path between its nodes.
/// </summary>
public sealed class Diode : ICircuitElement
{
    public const double DefaultSaturationCurrent = 1e-14;
    public const double DefaultIdealityFactor = 1.0;

    /// <summary>
    /// SPICE's GMIN: a conductance shunting the junction so that a reverse-biased diode still leaves
    /// a finite path between its nodes. The Shockley conductance underflows to zero below about
    /// -19.3 V, so a node reached only through reverse-biased junctions (a bridge rectifier's
    /// floating rail, say) would otherwise leave the MNA system singular.
    /// </summary>
    /// <remarks>
    /// Three decades looser than SPICE's 1e-12, because <see cref="Circuit.Step"/> has no gmin or
    /// source stepping to fall back on, so this floor has to carry the conditioning alone. The value
    /// was measured on ac-full-wave-rectifier-filter: 1e-10 does not converge, and above 3e-9 the
    /// shunt starts bending the answer. Gmin stepping would widen that window and let this come back
    /// toward SPICE's value.
    /// </remarks>
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

    /// <summary>
    /// <see cref="_voltage"/> as of the last <see cref="Commit"/>, which
    /// <see cref="Rollback"/> rewinds to when a substep is retried at a smaller step.
    /// </summary>
    private double _committedVoltage;

    public bool IsNonlinear => true;

    public Diode(int nodeAnode, int nodeCathode, double saturationCurrent = DefaultSaturationCurrent, double idealityFactor = DefaultIdealityFactor, double minimumConductance = DefaultMinimumConductance)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(saturationCurrent);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idealityFactor);
        // Zero is allowed: it acts as an unshunted Shockley model for a circuit that does not need
        // the floor and wants the exact reverse characteristic.
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

        // The shunt is a plain resistor, i = MinimumConductance * v, whose tangent at any bias passes
        // through the origin. Its Norton companion is therefore the conductance alone with no
        // current source: its MinimumConductance * _voltage appears once inside geq * _voltage and
        // once in the total current, and the two cancel in ieq. So the floor adds to geq only, and
        // ieq is the junction's companion current unchanged.
        double ieq = geq * _voltage - (junctionCurrent + MinimumConductance * _voltage);

        builder.AddConductance(NodeAnode, NodeCathode, geq);
        builder.AddCurrentSource(NodeCathode, NodeAnode, ieq);
    }

    public bool Relinearize(CircuitState state)
    {
        double proposed = state.Voltage(NodeAnode) - state.Voltage(NodeCathode);
        double limited = SpiceMath.Pnjlim(proposed, _voltage, Vt, VCrit, out bool wasLimited);

        // A damped step is not a converged one however small the move looks: the limiter chose
        // the step, not the Newton update, so the residual it was hiding is still there. SPICE
        // carries the same rule, feeding pnjlim's icheck straight into its non-convergence flag.
        bool converged = !wasLimited && Circuit.HasConverged(limited, _voltage);
        _voltage = limited;
        return converged;
    }

    /// <summary>
    /// No history to carry: the junction is memoryless. This only records the linearization point
    /// the converged solve was produced at, so <see cref="Rollback"/> has somewhere to go
    /// back to.
    /// </summary>
    public void Commit(CircuitState state, double deltaTime)
        => _committedVoltage = _voltage;

    public void Rollback()
        => _voltage = _committedVoltage;

    /// <summary>
    /// Current flowing from anode to cathode through the diode, evaluated at the solved voltage.
    /// Includes the <see cref="MinimumConductance"/> leakage, so this is the element's actual
    /// terminal current and sums with the rest of the circuit's under KCL.
    /// </summary>
    public double Current(CircuitState state)
    {
        double voltage = state.Voltage(NodeAnode) - state.Voltage(NodeCathode);
        return SaturationCurrent * (Math.Exp(voltage / Vt) - 1.0) + MinimumConductance * voltage;
    }
}
