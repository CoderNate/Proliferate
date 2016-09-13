using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using System.Diagnostics;

namespace Proliferate
{
    public enum ExecutableType
    {
        Default,
        Force32Bit,
        Force64Bit
    }

    public class ExecutableGenerator
    {
        private static readonly ExecutableGenerator _instance = new ExecutableGenerator();
        public static ExecutableGenerator Instance { get { return _instance; } }

        private ExecutableGenerator()
        { }

        /// <summary>
        /// Generates an executable that does nothing but call a given method.
        /// </summary>
        public string GenerateExecutable(MethodInfo methodToCall, ExecutableType executableType,
                string assemblyName)
        {
            var saveDir = System.IO.Path.GetDirectoryName(typeof(ExecutableGenerator).Assembly.Location);
            var w = System.Diagnostics.Stopwatch.StartNew();
            //From http://stackoverflow.com/a/15602171
            var executableFileName = assemblyName + ".exe";
            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName(assemblyName), AssemblyBuilderAccess.Save, saveDir);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(
                assemblyName, executableFileName);
            TypeBuilder typeBuilder = moduleBuilder.DefineType("Program",
                TypeAttributes.Class | TypeAttributes.Public);
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(
                "Main", MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Static,
                typeof(void), new Type[] { typeof(string[]) });
            ILGenerator gen = methodBuilder.GetILGenerator();
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, methodToCall);
            gen.Emit(OpCodes.Ret);
            typeBuilder.CreateType();
            assemblyBuilder.SetEntryPoint(methodBuilder, PEFileKinds.ConsoleApplication);
            File.Delete(executableFileName);
            PortableExecutableKinds peKind;
            ImageFileMachine machine;
            if (executableType == ExecutableType.Force32Bit)
            {
                peKind = PortableExecutableKinds.Required32Bit;
                machine = ImageFileMachine.I386;
            }
            else if (executableType == ExecutableType.Force64Bit)
            {
                peKind = PortableExecutableKinds.PE32Plus;
                machine = ImageFileMachine.AMD64;
            }
            else
            {
                peKind = PortableExecutableKinds.ILOnly;
                machine = ImageFileMachine.I386;
            }
            assemblyBuilder.Save(executableFileName, peKind, machine);
            var elapsed = w.Elapsed.ToString();
            return System.IO.Path.Combine(saveDir, executableFileName);
        }

        //public void RunTheExe(string exeFilePath)
        //{
        //    var startInf = new System.Diagnostics.ProcessStartInfo(exeFilePath, "Hello")
        //    { UseShellExecute = false, CreateNoWindow = true };
        //    var p = Process.Start(startInf);
        //    p.WaitForExit();
        //}
    }
}
