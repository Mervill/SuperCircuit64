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

        // KCL: the current through the resistor must equal the current through the diode, up to
        // the Newton-Raphson convergence tolerance.
        Assert.Equal(resistorCurrent, diodeCurrent, 1e-6);

        // A ~4 mA operating point ((5 V - ~0.7 V) / 1 kohm) on the default Is lands the forward drop
        // in the usual silicon range.
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

        // Anode grounded, cathode toward the source: reverse-biased once node 2 rises toward vs.
        var diode = new Diode(Circuit.Ground, 2);
        circuit.Add(diode);
        var state = circuit.Step(1e-6);
        Assert.True(diode.Current(state) < 0.0);

        // Reverse leakage is the GMIN shunt plus the saturation current, and at this bias the shunt
        // dominates Is by five orders of magnitude (1e-9 S * 5 V against a 1e-14 A Is), so this
        // asserts their sum, not Is alone. The estimate takes the reverse bias to be vs, although
        // node 2 actually sits one leakage-times-r below it: 5 nA through 1 kohm is 5 uV, one part
        // per million of the bias, so the approximation costs the estimate nothing measurable.
        double expectedLeakage = Diode.DefaultSaturationCurrent + Diode.DefaultMinimumConductance * vs;
        Assert.InRange(diode.Current(state), -expectedLeakage * 1.01, 0.0);
        Assert.Equal(vs - expectedLeakage * r, state.Voltage(2), 1e-9);
    }
}
