using System;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests;

/// <summary>
/// Step-response tests for a series R/L driven by a DC source, checked against the closed-form
/// solution I(t) = (Vs / R) * (1 - e^(-t/tau)), tau = L / R.
/// </summary>
/// <remarks>
/// Durations and step sizes are chosen against tau exactly as in <see cref="RcChargingTests"/>,
/// which explains why. Note that R divides here: a larger resistance makes an R/L circuit settle
/// faster, not slower, so an R/L fixture's duration cannot be reasoned about by analogy with an R/C
/// one that uses the same resistor.
/// </remarks>
public class RlChargingTests
{
    [Fact]
    public void InductorCurrent_BackwardEuler_TracksAnalyticExponential()
    {
        const double r = 100.0;
        const double l = 1e-3;
        const double vs = 5.0;
        const double dt = 1e-8; // small step so backward-Euler error stays tight
        const double tau = l / r; // 10 us

        // tau is only 10 us here, a hundredth of the R/C fixture's 1 ms, and dt shrank only tenfold,
        // so h = dt / tau is 1e-3 rather than the R/C tests' 1e-4. At that h backward Euler's peak
        // deviation is about 0.18 * (vs / r) * h ~= 9e-6 A, still two orders of magnitude under the
        // 1e-3 A assertion tolerance.
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(vs)));
        circuit.Add(new Resistor(1, 2, r));
        circuit.Add(new Inductor(2, Circuit.Ground, l, method: IntegrationMethod.BackwardEuler));
        double t = 0.0;

        // 20_000 steps * 1e-8 s = 200 us = 20 tau. Unlike the R/C transient tests this run spans both the
        // transient and a long settled tail, so it checks the curve shape and the final value at
        // once; the analytic residual at 20 tau is e^-20 ~= 2e-9.
        for (int i = 0; i < 20_000; i++)
        {
            var state = circuit.Step(dt);
            t += dt;

            // The current through R equals the current into the inductor: (Vs - V2) / R.
            double current = (vs - state.Voltage(2)) / r;
            double expected = (vs / r) * (1.0 - Math.Exp(-t / tau));
            Assert.Equal(expected, current, 1e-3);
        }
    }

    [Fact]
    public void InductorCurrent_Trapezoidal_TracksAnalyticExponential()
    {
        const double r = 100.0;
        const double l = 1e-3;
        const double vs = 5.0;
        const double dt = 1e-8;
        const double tau = l / r; // 10 us

        // Same h = dt / tau = 1e-3 and 20 tau span as the backward-Euler case above, so the two tests
        // differ only in integration method.
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(vs)));
        circuit.Add(new Resistor(1, 2, r));
        circuit.Add(new Inductor(2, Circuit.Ground, l, method: IntegrationMethod.Trapezoidal));
        double t = 0.0;

        // 200 us = 20 tau
        for (int i = 0; i < 20_000; i++)
        {
            var state = circuit.Step(dt);
            t += dt;

            double current = (vs - state.Voltage(2)) / r;
            double expected = (vs / r) * (1.0 - Math.Exp(-t / tau));
            Assert.Equal(expected, current, 1e-3);
        }
    }
}
