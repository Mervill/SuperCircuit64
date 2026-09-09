using System;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests;

/// <summary>
/// Step-response tests for a series R/C driven by a DC source, checked against the closed-form
/// solution V(t) = Vs * (1 - e^(-t/tau)).
///
/// <para>
/// <b>tau (the time constant)</b> is the single number that characterises how fast a first-order
/// circuit settles. For a series R/C it is tau = R * C, in seconds. After t = tau the capacitor has
/// reached 63.2% of its final voltage; the remaining error decays by a further factor of e for every
/// additional tau: 1 tau leaves 36.8%, 2 tau leaves 13.5%, 3 tau leaves 5.0%, 5 tau leaves 0.67%,
/// 7 tau leaves 0.09%, and 10 tau leaves 0.0045%. There is no finite time at which the transient is
/// exactly over, so "settled" is always a statement about a chosen number of tau.
/// </para>
/// </summary>
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

        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(vs)));
        circuit.Add(new Resistor(1, 2, r));
        circuit.Add(new Capacitor(2, Circuit.Ground, c));
        double t = 0.0;

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

        for (int i = 0; i < 100_000; i++)
            state = circuit.Step(dt);

        Assert.Equal(vs, state.Voltage(2), 1e-3);
    }
}
