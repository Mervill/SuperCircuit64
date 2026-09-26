using System.Collections.Generic;

namespace SuperCircuit64.Elements.Controlled;

public sealed class CurrentControlledCurrentSource : ICircuitElement
{
    public int OutputPositive { get; }
    public int OutputNegative { get; }
    public int ControlPositive { get; }
    public int ControlNegative { get; }

    public double Gain { get; set; }

    private int _controlBranchIndex = -1;

    public CurrentControlledCurrentSource(int outputPositive, int outputNegative, int controlPositive, int controlNegative, double gain)
    {
        OutputPositive = outputPositive;
        OutputNegative = outputNegative;
        ControlPositive = controlPositive;
        ControlNegative = controlNegative;
        Gain = gain;
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

    public int BranchCount => 1;

    public void AssignBranches(int firstBranchIndex)
        => _controlBranchIndex = firstBranchIndex;

    public void Stamp(MnaBuilder builder, double time, double deltaTime)
    {
        builder.AddVoltageSource(ControlPositive, ControlNegative, _controlBranchIndex, 0.0);
        builder.AddCurrentControlledCurrentSource(OutputPositive, OutputNegative, _controlBranchIndex, Gain);
    }

    /// <summary>
    /// Current flowing from the control positive to negative terminal through the sensing branch.
    /// </summary>
    public double ControlCurrent(CircuitState state)
        => state.Branch(_controlBranchIndex);

    /// <summary>
    /// Current flowing from the output positive to negative terminal through the source.
    /// </summary>
    public double Current(CircuitState state)
        => Gain * state.Branch(_controlBranchIndex);
}
