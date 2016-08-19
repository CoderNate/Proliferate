using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proliferate
{
    internal static class Constants
    {
        public const string PingPipeNameSuffix = "Ping";
        public static readonly Guid NoResponseNeededId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        public static readonly Guid ShutdownId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        public const int PingIntervalMilliseconds = 5000;
    }
}
