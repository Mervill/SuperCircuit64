namespace SuperCircuit64.Waveforms;

public sealed record DcWaveform(double Voltage) : IWaveform
{
    public static DcWaveform Zero { get; } = new DcWaveform(0.0);

    public double ValueAt(double time)
        => Voltage;
}
