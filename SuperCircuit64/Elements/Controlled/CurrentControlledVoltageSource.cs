using System.Collections.Generic;

namespace SuperCircuit64.Elements.Controlled;

public sealed class CurrentControlledVoltageSource : ICircuitElement
{
    public int OutputPositive { get; }
    public int OutputNegative { get; }
    public int ControlPositive { get; }
    public int ControlNegative { get; }

    public double Transresistance { get; set; }

    private int _controlBranchIndex = -1;
    private int _outputBranchIndex = -1;

    public CurrentControlledVoltageSource(int outputPositive, int outputNegative, int controlPositive, int controlNegative, double transresistance)
    {
        OutputPositive = outputPositive;
        OutputNegative = outputNegative;
        ControlPositive = controlPositive;
        ControlNegative = controlNegative;
        Transresistance = transresistance;
    }

    public IEnumerable<int> Nodes
    {
        get
        {
            yield return OutputPositive;
            yield return OutputNegative;
            yield return ControlPositive;
            yield return ControlNegative;
        }
    }

    public int BranchCount => 2;

    public void AssignBranches(int firstBranchIndex)
    {
        _controlBranchIndex = firstBranchIndex;
        _outputBranchIndex = firstBranchIndex + 1;
    }

    public void Stamp(MnaBuilder builder, double time, double deltaTime)
    {
        builder.AddVoltageSource(ControlPositive, ControlNegative, _controlBranchIndex, 0.0);
        builder.AddCurrentControlledVoltageSource(OutputPositive, OutputNegative, _outputBranchIndex, _controlBranchIndex, Transresistance);
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
        => state.Branch(_outputBranchIndex);
}
