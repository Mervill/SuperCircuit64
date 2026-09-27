using System.Collections.Generic;

namespace SuperCircuit64.Elements.Controlled;

/// <summary>
/// An ideal current-controlled voltage source: holds
/// V(OutputPositive) - V(OutputNegative) at <see cref="Transresistance"/> times the control
/// current.
/// </summary>
/// <remarks>
/// The control current is sensed by a zero-volt source between <see cref="ControlPositive"/> and
/// <see cref="ControlNegative"/>, so the control port sits in series with the branch it measures
/// and shorts those two nodes together; it is positive flowing from
/// <see cref="ControlPositive"/> through the element to <see cref="ControlNegative"/>. Two branch
/// unknowns: that sensing current, and the output current the voltage constraint needs.
/// </remarks>
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
