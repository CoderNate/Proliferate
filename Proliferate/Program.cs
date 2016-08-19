using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proliferate
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length % 2 != 0)
                throw new ArgumentException("Expected a series of parameter name/value pairs (ex. -arg1 value1 -arg2 -value2) but got "
                    + string.Join(" ", args.ToArray()));
            var argsDict = Enumerable.Range(0, args.Length / 2).Select(n => n * 2)
                .ToDictionary(num => args[num], num => args[num + 1], StringComparer.OrdinalIgnoreCase);
            var assemblyFile = GetOrError(argsDict, "-assemblyFile");
            var assem = System.Reflection.Assembly.LoadFrom(assemblyFile);
            var typeName = GetOrError(argsDict, "-typeName");
            var type = assem.GetType(typeName);
            var methodName = GetOrError(argsDict, "-methodName");
            var method = type.GetMethod(methodName);
            if (method.GetParameters().Length != 1)
                throw new Exception(string.Format("The method '{0}' on type '{1}' was expected to have 1 argument.",
                    methodName, typeName));
            method.Invoke(null, new object[] { argsDict });
        }

        private static string GetOrError(IDictionary<string, string> dict, string key)
        {
            string result;
            if (!dict.TryGetValue(key, out result))
            {
                throw new Exception(string.Format("'{0}' is a required parameter.", key));
            }
            return result;
        }
    }
    
}
