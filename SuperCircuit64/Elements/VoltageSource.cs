using System.Collections.Generic;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Elements;

public sealed class VoltageSource : ICircuitElement
{
    public int NodePositive { get; }
    public int NodeNegative { get; }

    public IWaveform Waveform { get; set; }

    private int _branchIndex = -1;

    public VoltageSource(int nodePositive, int nodeNegative, IWaveform waveform)
    {
        NodePositive = nodePositive;
        NodeNegative = nodeNegative;
        Waveform = waveform;
    }

    public IEnumerable<int> Terminals
    {
        get
        {
            yield return NodePositive;
            yield return NodeNegative;
        }
    }

    public int BranchCount => 1;

    public void AssignBranches(int firstBranchIndex)
        => _branchIndex = firstBranchIndex;

    public void Stamp(MnaBuilder builder, double time, double deltaTime)
        => builder.AddVoltageSource(NodePositive, NodeNegative, _branchIndex, Waveform.ValueAt(time));

    public double Current(CircuitState state)
        => state.Branch(_branchIndex);

}
