using System;
using System.IO;
using System.Linq;
using SuperCircuit64;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests.uSimmics;

public class AcResistorDiodeTests
{
    [Fact]
    public void NodeVoltageAndProbeCurrent_MatchQucsStudioTransient()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "sources", "ac-resistor-diode.dat");
        var dataset = QucsDatasetReader.Read(path);

        var time = dataset.Single(v => v.Name == "time");
        var nodeVoltage = dataset.Single(v => v.Name == "L1.Vt");
        var probeCurrent = dataset.Single(v => v.Name == "Pr1.It");

        // ac-resistor-diode.sch: Vac V1 "5 V" "40 Hz" -> R1 "180 Ω" -> node "L1" -> Pr1 (ideal
        // ammeter, in series) -> D1 (anode toward Pr1) -> ground. D1 is "1e-15 A" saturation
        // current, ideality "1", which overrides Diode's 1e-14 A default.
        //
        // The diode is nonlinear but memoryless, so there is no transient, and the .sch's
        // 0..25 ms sweep (one 40 Hz period) covers the conducting and blocking half-cycles. Qucs's
        // diode carries a 10 fF junction capacitance that Diode does not model; through 180 ohm that
        // is a 1.8 ps time constant against a 12.5 us dt, so it cannot show at this step size.
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new SineWaveform(5.0, 40.0)));
        circuit.Add(new Resistor(1, 2, 180.0));
        var probe = new VoltageSource(2, 3, DcWaveform.Zero);
        circuit.Add(probe);
        circuit.Add(new Diode(3, Circuit.Ground, saturationCurrent: 1e-15, idealityFactor: 1.0));

        // Nonlinear, so the shared QucsComparison default applies. Measured worst-case drift is
        // 4.42e-7 on L1.Vt (18.7% of the band) and 2.46e-9 on Pr1.It (0.2%): one error seen twice,
        // since 4.42e-7 V across R1's 180 ohms is exactly 2.46e-9 A.
        double dt = time.Real[1] - time.Real[0];
        for (int i = 1; i < time.Real.Length; i++)
        {
            var state = circuit.Step(dt);
            QucsComparison.AssertMatches(time.Real[i], state.Time, "time");
            QucsComparison.AssertMatches(nodeVoltage.Real[i], state.Voltage(2), "L1.Vt");
            QucsComparison.AssertMatches(probeCurrent.Real[i], probe.Current(state), "Pr1.It");
        }
    }
}
