using System;
using System.IO;
using System.Linq;
using SuperCircuit64;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests.uSimmics;

public class AcFullWaveRectifierTests
{
    [Fact]
    public void NodeVoltageAndProbeCurrents_MatchQucsStudioTransient()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "sources", "ac-full-wave-rectifier.dat");
        var dataset = QucsDatasetReader.Read(path);

        var time = dataset.Single(v => v.Name == "time");
        var sourceVoltage = dataset.Single(v => v.Name == "L1.Vt");
        var loadVoltage = dataset.Single(v => v.Name == "Pr3.dVt");
        var inputCurrent = dataset.Single(v => v.Name == "Pr1.It");
        var loadCurrent = dataset.Single(v => v.Name == "Pr2.It");

        // ac-full-wave-rectifier.sch: Vac V1 "5 V" "40 Hz" -> node "L1" -> Pr1 (ideal ammeter) ->
        // node "mid" (top of the diode bridge). "mid" and ground are the bridge's AC corners;
        // "leftRail"/"rightRail" (screen-layout names, not polarity) are its DC corners. D1 (anode
        // leftRail, cathode mid) and D2 (anode mid, cathode rightRail) connect "mid" to the two
        // rails; D3 (anode leftRail, cathode ground) and D4 (anode ground, cathode rightRail)
        // connect ground to the same two rails. The rails close the loop through the R1 "100 Ω"
        // load in series with Pr2 (ideal ammeter): rightRail -> Pr2 -> R1 -> leftRail. VProbe Pr3
        // is "+" on rightRail, "-" on leftRail, so Pr3.dVt is the voltage across R1 because Pr2 is
        // an ideal 0 V ammeter. All four diodes are "1e-15 A" saturation current, ideality "1",
        // which overrides Diode's 1e-14 A default.
        //
        // Nothing stores energy and there is no smoothing capacitor, so there is no transient, and
        // the .sch's 0..25 ms sweep (one 40 Hz period) covers both conduction pairs (D1/D4 and
        // D2/D3) once each.
        var circuit = new Circuit();
        const int l1 = 1;
        const int mid = 2;
        const int leftRail = 3;
        const int rightRail = 4;
        const int rTop = 5;

        circuit.Add(new VoltageSource(l1, Circuit.Ground, new SineWaveform(5.0, 40.0)));
        var probeIn = new VoltageSource(l1, mid, DcWaveform.Zero);
        circuit.Add(probeIn);
        circuit.Add(new Diode(leftRail, mid, saturationCurrent: 1e-15, idealityFactor: 1.0));
        circuit.Add(new Diode(mid, rightRail, saturationCurrent: 1e-15, idealityFactor: 1.0));
        circuit.Add(new Diode(leftRail, Circuit.Ground, saturationCurrent: 1e-15, idealityFactor: 1.0));
        circuit.Add(new Diode(Circuit.Ground, rightRail, saturationCurrent: 1e-15, idealityFactor: 1.0));
        var probeLoad = new VoltageSource(rightRail, rTop, DcWaveform.Zero);
        circuit.Add(probeLoad);
        circuit.Add(new Resistor(rTop, leftRail, 100.0));

        // Nonlinear, so the shared QucsComparison default applies. Pr3.dVt is asserted rather than
        // either rail on its own: the rails' common-mode level floats through the zero crossings,
        // but their difference is tied to the load current and stays well-conditioned.
        //
        // Measured worst-case drift is 1.89e-7 on Pr3.dVt (10.2% of the band), ~1.9e-9 on the
        // currents (0.2%) and 9e-13 on L1.Vt, the tightest of the four diode fixtures despite
        // having four junctions. Before PhysicalConstants.ThermalVoltage was corrected it was the
        // loosest (1.84e-6), since each junction in the loop added to that 1.04 ppm error.
        double dt = time.Real[1] - time.Real[0];
        for (int i = 1; i < time.Real.Length; i++)
        {
            var state = circuit.Step(dt);
            QucsComparison.AssertMatches(time.Real[i], state.Time, "time");
            QucsComparison.AssertMatches(sourceVoltage.Real[i], state.Voltage(l1), "L1.Vt");
            QucsComparison.AssertMatches(loadVoltage.Real[i], state.Voltage(rightRail) - state.Voltage(leftRail), "Pr3.dVt");
            QucsComparison.AssertMatches(inputCurrent.Real[i], probeIn.Current(state), "Pr1.It");
            QucsComparison.AssertMatches(loadCurrent.Real[i], probeLoad.Current(state), "Pr2.It");
        }
    }
}
