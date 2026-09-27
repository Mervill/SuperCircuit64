using System;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests;

/// <summary>
/// Step-response tests for a series R/C driven by a DC source, checked against the closed-form
/// solution V(t) = Vs * (1 - e^(-t/tau)), tau = R * C.
/// </summary>
/// <remarks>
/// <para>
/// tau is how fast a first-order circuit settles: each tau leaves 1/e of the transient (36.8% after
/// 1 tau, 5.0% after 3, 0.67% after 5). Every duration in these tests is a multiple of tau, because
/// that decides what is being exercised: a few tau tests the steep part of the curve, many tau pins
/// down steady state, and a run much shorter than tau tests almost nothing.
/// </para>
/// <para>
/// Every step size is a fraction of tau, because accuracy depends on h = dt / tau rather than on dt.
/// Backward Euler's deviation peaks near t = tau at about 0.18 * Vs * h (first order in h);
/// trapezoidal is second order. Each dt is picked so that predicted error sits an order of
/// magnitude under the assertion's tolerance.
/// </para>
/// </remarks>
public class RcChargingTests
{
    [Fact]
    public void CapacitorVoltage_BackwardEuler_TracksAnalyticExponential()
    {
        const double r = 1_000.0;
        const double c = 1e-6;
        const double vs = 5.0;
        const double dt = 1e-7; // small step so backward-Euler error stays tight
        const double tau = r * c; // 1 ms

        // h = dt / tau = 1e-4, so backward Euler's peak deviation is about 0.18 * vs * h ~= 9e-5,
        // roughly 10x under the 1e-3 assertion tolerance below.
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(vs)));
        circuit.Add(new Resistor(1, 2, r));
        circuit.Add(new Capacitor(2, Circuit.Ground, c, method: IntegrationMethod.BackwardEuler));
        double t = 0.0;

        // 20_000 steps * 1e-7 s = 2 ms = 2 tau, the steep part of the curve up to 86.5% charged.
        // CapacitorVoltage_ApproachesSourceAtSteadyState checks the final value.
        for (int i = 0; i < 20_000; i++)
        {
            var state = circuit.Step(dt);
            t += dt;
            double expected = vs * (1.0 - Math.Exp(-t / tau));
            Assert.Equal(expected, state.Voltage(2), 1e-3);
        }
    }

    [Fact]
    public void CapacitorVoltage_Trapezoidal_TracksAnalyticExponential()
    {
        const double r = 1_000.0;
        const double c = 1e-6;
        const double vs = 5.0;
        const double dt = 1e-7;
        const double tau = r * c; // 1 ms

        // Same h = dt / tau = 1e-4 as the backward-Euler case above, so the two tests differ only in
        // integration method (trapezoidal is O(h^2), backward Euler O(h)).
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(vs)));
        circuit.Add(new Resistor(1, 2, r));
        circuit.Add(new Capacitor(2, Circuit.Ground, c, method: IntegrationMethod.Trapezoidal));
        double t = 0.0;

        // 2 ms = 2 tau
        for (int i = 0; i < 20_000; i++)
        {
            var state = circuit.Step(dt);
            t += dt;
            double expected = vs * (1.0 - Math.Exp(-t / tau));
            Assert.Equal(expected, state.Voltage(2), 1e-3);
        }
    }

    [Fact]
    public void CapacitorVoltage_ApproachesSourceAtSteadyState()
    {
        const double r = 1_000.0;
        const double c = 1e-6;
        const double vs = 5.0;
        const double dt = 1e-6;

        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(vs)));
        circuit.Add(new Resistor(1, 2, r));
        circuit.Add(new Capacitor(2, Circuit.Ground, c));
        CircuitState state = null!;

        // 100 ms, tau = 1 ms => 100 tau. The analytic residual e^-100 is far below double precision,
        // so any error left at the end is the solver's. h = dt / tau = 1e-3 here; the looser step is
        // fine because only the final value is checked.
        for (int i = 0; i < 100_000; i++)
            state = circuit.Step(dt);

        Assert.Equal(vs, state.Voltage(2), 1e-3);
    }
}
