using System;
using SuperCircuit64;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests;

public class CircuitTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1e-6)]
    public void Step_RejectsNonPositiveDeltaTime(double deltaTime)
    {
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(5.0)));
        circuit.Add(new Resistor(1, Circuit.Ground, 1_000.0));

        Assert.Throws<ArgumentOutOfRangeException>(() => circuit.Step(deltaTime));
    }

    [Fact]
    public void ResistiveDivider_MatchesOhmsLaw()
    {
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(9.0)));
        circuit.Add(new Resistor(1, 2, 1_000.0));
        circuit.Add(new Resistor(2, Circuit.Ground, 2_000.0));

        var state = circuit.Step(1e-6);

        Assert.Equal(6.0, state.Voltage(2), 1e-9);
    }

    [Fact]
    public void CurrentSource_IntoGroundedResistor_MatchesOhmsLaw()
    {
        var circuit = new Circuit();
        circuit.Add(new CurrentSource(1, Circuit.Ground, new DcWaveform(0.002)));
        circuit.Add(new Resistor(1, Circuit.Ground, 1_000.0));

        var state = circuit.Step(1e-6);

        Assert.Equal(2.0, state.Voltage(1), 1e-9);
    }

    [Fact]
    public void ResistiveDivider_RecomputesAfterRemovingAndReplacingAnElement()
    {
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(9.0)));
        circuit.Add(new Resistor(1, 2, 1_000.0));

        var oldR2 = new Resistor(2, Circuit.Ground, 2_000.0);
        circuit.Add(oldR2);
        circuit.Step(1e-6);

        circuit.Remove(oldR2);
        circuit.Add(new Resistor(2, Circuit.Ground, 4_000.0));

        var state = circuit.Step(1e-6);

        Assert.Equal(7.2, state.Voltage(2), 1e-9);
    }
}
