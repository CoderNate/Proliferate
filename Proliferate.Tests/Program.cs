using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proliferate.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            //Use the NUnit test runner to run the tests in this project. This
            //method is only used for debugging purposes so that visual studio will
            //break on exceptions instead of having to track things down from a stack trace.
            var srwTests = new StreamReaderWriterTests();
            srwTests.TestSimultaneousRequests();
        }
    }
}
