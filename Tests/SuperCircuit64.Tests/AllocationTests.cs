using SuperCircuit64;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests;

public class AllocationTests
{
    private static Circuit BuildRcCircuit()
    {
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(5.0)));
        circuit.Add(new Resistor(1, 2, 1_000.0));
        circuit.Add(new Capacitor(2, Circuit.Ground, 1e-6));
        return circuit;
    }

    [Fact]
    public void Step_AllocatesLessOnceTopologyIsStable()
    {
        var circuit = BuildRcCircuit();
        circuit.Step(1e-6); // cold: builds topology and establishes the sparse pattern
        long coldAllocated = Alloc.Measure(() => BuildRcCircuit().Step(1e-6));

        const int iterations = 100;
        long warmAllocatedPerStep = Alloc.Measure(() => circuit.Step(1e-6), iterations) / iterations;
        Assert.True(warmAllocatedPerStep < coldAllocated,
            $"expected a step on a stable topology ({warmAllocatedPerStep} bytes) to allocate less than a topology-building step ({coldAllocated} bytes)");
    }

    [Fact]
    public void Step_IsZeroAllocOnceTopologyIsStable()
    {
        var circuit = BuildRcCircuit();
        circuit.Step(1e-6); // cold: builds topology and establishes the sparse pattern

        const int iterations = 1000;
        long warmAllocatedPerStep = Alloc.Measure(() => circuit.Step(1e-6), iterations) / iterations;
        Assert.True(warmAllocatedPerStep == 0,
            $"expected a step on a stable topology to be zero-alloc, but it allocated {warmAllocatedPerStep} bytes");
    }

    [Fact]
    public void Step_IsZeroAllocOnceTopologyIsStable_WithNewtonIteration()
    {
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(5.0)));
        circuit.Add(new Resistor(1, 2, 1_000.0));
        circuit.Add(new Diode(2, Circuit.Ground));
        circuit.Step(1e-6); // cold: builds topology and runs the first Newton-Raphson solve

        const int iterations = 1000;
        long warmAllocatedPerStep = Alloc.Measure(() => circuit.Step(1e-6), iterations) / iterations;
        Assert.True(warmAllocatedPerStep == 0,
            $"expected a step on a stable topology to be zero-alloc, but it allocated {warmAllocatedPerStep} bytes");
    }

    [Fact]
    public void GrowingTopology_AllocatesLessThanBuildingFromScratch()
    {
        var circuit = BuildRcCircuit();
        circuit.Step(1e-6); // cold: establishes topology/pattern at the original 3-element size
        circuit.Add(new Resistor(2, Circuit.Ground, 2_000.0));
        circuit.Step(1e-6); // first growth: pays to reallocate MnaBuilder's arrays, but over-provisions capacity

        // Further single-element growth should now fit inside the already-doubled capacity, so it
        // shouldn't need to reallocate MnaBuilder's backing arrays at all.
        long grownAllocated = Alloc.Measure(() =>
        {
            circuit.Add(new Resistor(3, Circuit.Ground, 4_000.0));
            circuit.Step(1e-6);
        });
        long coldAllocated = Alloc.Measure(() => BuildRcCircuit().Step(1e-6));
        Assert.True(grownAllocated < coldAllocated,
            $"expected growing an already-warm topology by one element ({grownAllocated} bytes) to allocate " +
            $"less than building a whole circuit from scratch ({coldAllocated} bytes)");
    }
}