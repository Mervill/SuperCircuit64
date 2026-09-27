using System;
using System.IO;
using System.Linq;
using SuperCircuit64;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests.uSimmics;

public class AcResistorResistorTests
{
    [Fact]
    public void NodeVoltageAndProbeCurrent_MatchQucsStudioTransient()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "sources", "ac-resistor-resistor.dat");
        var dataset = QucsDatasetReader.Read(path);

        var time = dataset.Single(v => v.Name == "time");
        var nodeVoltage = dataset.Single(v => v.Name == "L1.Vt");
        var probeCurrent = dataset.Single(v => v.Name == "Pr1.It");

        // ac-resistor-resistor.sch: Vac V1 "5 V" "40 Hz" -> R1 "180 Ω" -> node "L1" -> Pr1 (ideal
        // ammeter, in series) -> R2 "1 kohm" -> ground. The R/L and R/C fixtures' wiring with a
        // plain resistor in place of the reactive element: a resistive divider with no stored
        // energy, so no transient, and the .sch's 0..25 ms sweep (one 40 Hz period) covers it.
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new SineWaveform(5.0, 40.0)));
        circuit.Add(new Resistor(1, 2, 180.0));
        var probe = new VoltageSource(2, 3, DcWaveform.Zero);
        circuit.Add(probe);
        circuit.Add(new Resistor(3, Circuit.Ground, 1_000.0));

        // No integration and no Newton iteration. Measured worst-case drift is ~1.3e-8 absolute /
        // ~7e-9 relative, so this asserts well inside the shared QucsComparison default, which is
        // sized for the diode fixtures and would hide a regression here.
        const double absoluteTolerance = 1e-7;
        const double relativeTolerance = 1e-6;

        double dt = time.Real[1] - time.Real[0];
        for (int i = 1; i < time.Real.Length; i++)
        {
            var state = circuit.Step(dt);
            QucsComparison.AssertMatches(time.Real[i], state.Time, "time", absoluteTolerance, relativeTolerance);
            QucsComparison.AssertMatches(nodeVoltage.Real[i], state.Voltage(2), "L1.Vt", absoluteTolerance, relativeTolerance);
            QucsComparison.AssertMatches(probeCurrent.Real[i], probe.Current(state), "Pr1.It", absoluteTolerance, relativeTolerance);
        }
    }
}
