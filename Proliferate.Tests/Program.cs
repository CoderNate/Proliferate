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
            var tests = new BinaryFormatterTests();
            tests.TestBinaryFormatterWithSmallMessage();
            tests.TestBinaryFormatterWithLargeMessage();
        }
    }
}
