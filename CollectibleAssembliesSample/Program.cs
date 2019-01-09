using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CollectibleAssembliesSample
{
    class Program
    {
        private static Assembly SystemRuntime = Assembly.Load(new AssemblyName("System.Runtime"));

        // put entire UnloadableAssemblyLoadContext in a method to avoid Main
        // holding on to the reference
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ExecuteAssembly(int i)
        {
            var context = new UnloadableAssemblyLoadContext();
            var assemblyPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "SampleLibrary", "bin", "Debug", "netstandard2.0", "SampleLibrary.dll");
            using (var fs = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read))
            {
                var assembly = context.LoadFromStream(fs);

                var type = assembly.GetType("SampleLibrary.Greeter");
                var greetMethod = type.GetMethod("Hello");

                var instance = Activator.CreateInstance(type);
                greetMethod.Invoke(instance, new object[] { i });
            }

            context.Unload();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ExecuteInMemoryAssembly(int i)
        {
            var context = new UnloadableAssemblyLoadContext();
            var compilation = CSharpCompilation.Create(
                "DynamicAssembly", new[] { CSharpSyntaxTree.ParseText(@"
                public class Greeter
                {
                    public void Hello(int iteration)
                    {
                        System.Console.WriteLine($""Hello in memory {iteration}!"");
                    }
                }") }, 
                new[] 
                {
                    MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).GetTypeInfo().Assembly.Location),
                    MetadataReference.CreateFromFile(SystemRuntime.Location),
                },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                var diagnostics = compilation.GetDiagnostics();
                var cr = compilation.Emit(ms);
                ms.Seek(0, SeekOrigin.Begin);
                var assembly = context.LoadFromStream(ms);
                var createdType = assembly.ExportedTypes.FirstOrDefault(x => x.Name == "Greeter");
                var methodInfo = createdType.GetMethod("Hello", BindingFlags.Instance | BindingFlags.Public);
                var instance = Activator.CreateInstance(createdType);
                var result = methodInfo.Invoke(instance, new object[] { i });
            }

            context.Unload();
        }

        static void Main(string[] args)
        {
            for (var i = 0; i < 500; i++)
            {
                ExecuteAssembly(i);
            }

            for (var i = 0; i < 500; i++)
            {
                ExecuteInMemoryAssembly(i);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Console.ReadKey();
        }
    }
}
