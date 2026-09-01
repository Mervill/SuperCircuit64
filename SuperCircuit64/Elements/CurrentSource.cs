using System.Collections.Generic;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Elements;

public sealed class CurrentSource : ICircuitElement
{
    public int NodePositive { get; }
    public int NodeNegative { get; }

    public IWaveform Waveform { get; set; }

    public CurrentSource(int nodePositive, int nodeNegative, IWaveform waveform)
    {
        NodePositive = nodePositive;
        NodeNegative = nodeNegative;
        Waveform = waveform;
    }

    public IEnumerable<int> Nodes
    {
        get
        {
            yield return NodePositive;
            yield return NodeNegative;
        }
    }

    public void Stamp(MnaBuilder builder, double time, double deltaTime)
        => builder.AddCurrentSource(NodeNegative, NodePositive, Waveform.ValueAt(time));

    public double Current(CircuitState state)
        => Waveform.ValueAt(state.Time);
}
