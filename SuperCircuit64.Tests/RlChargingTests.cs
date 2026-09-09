using System;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests;

/// <summary>
/// Step-response tests for a series R/L driven by a DC source, checked against the closed-form
/// solution I(t) = (Vs / R) * (1 - e^(-t/tau)).
/// </summary>
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

        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(vs)));
        circuit.Add(new Resistor(1, 2, r));
        circuit.Add(new Inductor(2, Circuit.Ground, l));
        double t = 0.0;

        for (int i = 0; i < 20_000; i++)
        {
            var state = circuit.Step(dt);
            t += dt;

            // current through R equals current into the inductor: (Vs - V2) / R
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
