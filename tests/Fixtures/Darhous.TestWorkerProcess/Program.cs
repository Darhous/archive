using System;
using System.Threading;

namespace Darhous.TestWorkerProcess;

public static class Program
{
    public static int Main(string[] args)
    {
        var crash = Environment.GetEnvironmentVariable("TESTWORKER_CRASH");
        if (crash == "1")
        {
            return 1;
        }

        Thread.Sleep(60000);
        return 0;
    }
}
