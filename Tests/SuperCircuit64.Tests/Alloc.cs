using System;

namespace SuperCircuit64.Tests;

public static class Alloc
{
    public static long Measure(Action codeUnderTest)
    {
        codeUnderTest();

        long before = GC.GetAllocatedBytesForCurrentThread();
        codeUnderTest();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        return allocated;
    }

    public static long Measure(Action codeUnderTest, int iterations)
    {
        for (int i = 0; i < iterations; i++)
            codeUnderTest();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
            codeUnderTest();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        return allocated;
    }
}
