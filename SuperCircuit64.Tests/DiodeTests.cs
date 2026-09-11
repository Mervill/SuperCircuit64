using SuperCircuit64;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests;

public class DiodeTests
{
    [Fact]
    public void ForwardBiased_SatisfiesKirchhoffAndShockleyLaw()
    {
        const double vs = 5.0;
        const double r = 1_000.0;

        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(vs)));
        circuit.Add(new Resistor(1, 2, r));

        var diode = new Diode(2, Circuit.Ground);
        circuit.Add(diode);
        var state = circuit.Step(1e-6);
        double diodeVoltage = state.Voltage(2);
        double diodeCurrent = diode.Current(state);
        double resistorCurrent = (vs - diodeVoltage) / r;

        Assert.Equal(resistorCurrent, diodeCurrent, 1e-6);

        Assert.InRange(diodeVoltage, 0.5, 0.8);
    }

    [Fact]
    public void ReverseBiased_CarriesOnlyLeakageCurrent()
    {
        const double vs = 5.0;
        const double r = 1_000.0;

        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(vs)));
        circuit.Add(new Resistor(1, 2, r));

        var diode = new Diode(Circuit.Ground, 2);
        circuit.Add(diode);
        var state = circuit.Step(1e-6);
        Assert.True(diode.Current(state) < 0.0);

        double expectedLeakage = Diode.DefaultSaturationCurrent + Diode.DefaultMinimumConductance * vs;
        Assert.InRange(diode.Current(state), -expectedLeakage * 1.01, 0.0);
        Assert.Equal(vs - expectedLeakage * r, state.Voltage(2), 1e-9);
    }
}
