using System.Collections.Generic;

namespace SuperCircuit64.Elements.Controlled;

public sealed class VoltageControlledVoltageSource : ICircuitElement
{
    public int OutputPositive { get; }
    public int OutputNegative { get; }
    public int ControlPositive { get; }
    public int ControlNegative { get; }

    public double Gain { get; set; }

    private int _branchIndex = -1;

    public VoltageControlledVoltageSource(int outputPositive, int outputNegative, int controlPositive, int controlNegative, double gain)
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
        => _branchIndex = firstBranchIndex;

    public void Stamp(MnaBuilder builder, double time, double deltaTime)
        => builder.AddVoltageControlledVoltageSource(OutputPositive, OutputNegative, ControlPositive, ControlNegative, _branchIndex, Gain);

    /// <summary>
    /// Current flowing from the output positive to negative terminal through the source.
    /// </summary>
    public double Current(CircuitState state)
        => state.Branch(_branchIndex);
}
