using System;
using System.IO;
using System.Linq;
using SuperCircuit64;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests.uSimmics;

public class AcHalfWaveRectifierTests
{
    [Fact]
    public void NodeVoltageAndProbeCurrents_MatchQucsStudioTransient()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "sources", "ac-half-wave-rectifier.dat");
        var dataset = QucsDatasetReader.Read(path);

        var time = dataset.Single(v => v.Name == "time");
        var sourceVoltage = dataset.Single(v => v.Name == "L1.Vt");
        var loadVoltage = dataset.Single(v => v.Name == "Pr3.dVt");
        var inputCurrent = dataset.Single(v => v.Name == "Pr1.It");
        var loadCurrent = dataset.Single(v => v.Name == "Pr2.It");

        // ac-half-wave-rectifier.sch: Vac V1 "5 V" "40 Hz" -> node "L1" -> Pr1 (ideal ammeter) ->
        // D1 (anode toward Pr1) -> node "cathode" -> Pr2 (ideal ammeter) -> R1 "100 Ω" -> ground.
        // The full-wave fixture's source, load and probes with a single series diode in place of the
        // bridge. VProbe Pr3 is "+" on "cathode", "-" on ground, so Pr3.dVt = V(cathode), which is
        // the voltage across R1 because Pr2 is an ideal 0 V ammeter. D1 is "1e-15 A" saturation
        // current, ideality "1", which overrides Diode's 1e-14 A default.
        //
        // Nothing stores energy and there is no smoothing capacitor, so there is no transient, and
        // the .sch's 0..25 ms sweep (one 40 Hz period) covers the conducting and blocking
        // half-cycles once each.
        var circuit = new Circuit();
        const int l1 = 1;
        const int anode = 2;
        const int cathode = 3;
        const int rTop = 4;

        circuit.Add(new VoltageSource(l1, Circuit.Ground, new SineWaveform(5.0, 40.0)));
        var probeIn = new VoltageSource(l1, anode, DcWaveform.Zero);
        circuit.Add(probeIn);
        circuit.Add(new Diode(anode, cathode, saturationCurrent: 1e-15, idealityFactor: 1.0));
        var probeLoad = new VoltageSource(cathode, rTop, DcWaveform.Zero);
        circuit.Add(probeLoad);
        circuit.Add(new Resistor(rTop, Circuit.Ground, 100.0));

        // Nonlinear, so the shared QucsComparison default applies. Measured worst-case drift is
        // 2.77e-7 on Pr3.dVt (24.1% of the band); the currents agree to ~2.8e-9 (0.3%) and L1.Vt,
        // which is just the source, to 9e-13.
        double dt = time.Real[1] - time.Real[0];
        for (int i = 1; i < time.Real.Length; i++)
        {
            var state = circuit.Step(dt);
            QucsComparison.AssertMatches(time.Real[i], state.Time, "time");
            QucsComparison.AssertMatches(sourceVoltage.Real[i], state.Voltage(l1), "L1.Vt");
            QucsComparison.AssertMatches(loadVoltage.Real[i], state.Voltage(cathode), "Pr3.dVt");
            QucsComparison.AssertMatches(inputCurrent.Real[i], probeIn.Current(state), "Pr1.It");
            QucsComparison.AssertMatches(loadCurrent.Real[i], probeLoad.Current(state), "Pr2.It");
        }
    }
}
