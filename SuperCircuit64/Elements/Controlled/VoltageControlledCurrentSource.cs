using System.Collections.Generic;

namespace SuperCircuit64.Elements.Controlled;

/// <summary>
/// An ideal voltage-controlled current source: drives
/// <see cref="Transconductance"/> * (V(ControlPositive) - V(ControlNegative)) out of
/// <see cref="OutputPositive"/> into the circuit, returning through <see cref="OutputNegative"/>.
/// The control terminals draw no current.
/// </summary>
/// <remarks>
/// The only controlled source that needs no branch unknown: it is four conductance-matrix entries
/// coupling the output rows to the control columns.
/// </remarks>
public sealed class VoltageControlledCurrentSource : ICircuitElement
{
    public int OutputPositive { get; }
    public int OutputNegative { get; }
    public int ControlPositive { get; }
    public int ControlNegative { get; }

    public double Transconductance { get; set; }

    public VoltageControlledCurrentSource(int outputPositive, int outputNegative, int controlPositive, int controlNegative, double transconductance)
    {
        OutputPositive = outputPositive;
        OutputNegative = outputNegative;
        ControlPositive = controlPositive;
        ControlNegative = controlNegative;
        Transconductance = transconductance;
    }

    public IEnumerable<int> Terminals
    {
        get
        {
            yield return OutputPositive;
            yield return OutputNegative;
            yield return ControlPositive;
            yield return ControlNegative;
        }
    }

    public void Stamp(MnaBuilder builder, double time, double deltaTime)
        => builder.AddTransconductance(OutputPositive, OutputNegative, ControlPositive, ControlNegative, Transconductance);

    /// <summary>
    /// Current driven out of <see cref="OutputPositive"/> into the circuit, which is the current
    /// flowing from the output negative to positive terminal through the source.
    /// </summary>
    public double Current(CircuitState state)
        => Transconductance * (state.Voltage(ControlPositive) - state.Voltage(ControlNegative));
}
