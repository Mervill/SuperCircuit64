using System;

namespace SuperCircuit64.Waveforms;

public sealed record SineWaveform(double Amplitude, double FrequencyHz, double PhaseRadians = 0.0, double Offset = 0.0) : IWaveform
{
    public double ValueAt(double time)
        => Offset + Amplitude * Math.Sin(2.0 * Math.PI * FrequencyHz * time + PhaseRadians);
}
