using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ValidatorHelperMajorChangeChecker
{
    public static class ValidatorAssemblyLoader
    {
        public static Assembly GetAssembly(ResolveEventArgs args, string folderPath)
        {
            var missingAssemblyName = new AssemblyName(args.Name);
            var assemblyFilePath = Path.Combine(folderPath, missingAssemblyName.Name + ".dll");
            if (File.Exists(assemblyFilePath))
            {
                Console.WriteLine("MyResolveEventHandler|assemblyFilePath '" + assemblyFilePath + "' loading...");

                try
                {
                    var assembly = Assembly.LoadFile(assemblyFilePath);
                    Console.WriteLine("MyResolveEventHandler|assemblyFilePath '" + assemblyFilePath + "' loaded.");
                    return assembly;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("MyResolveEventHandler|assemblyFilePath '" + assemblyFilePath + "' could not be loaded. Exception thrown:" + Environment.NewLine + ex.Message + Environment.NewLine + "Trying again via bytes.");

                    try
                    {
                        var assembly = Assembly.Load(File.ReadAllBytes(assemblyFilePath));
                        return assembly;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("MyResolveEventHandler|assemblyFilePath '" + assemblyFilePath + "' could not be loaded via bytes. Exception thrown:" + Environment.NewLine + e);
                    }

                    // Ignore
                }
            }
            else
            {
                Console.WriteLine("MyResolveEventHandler|assemblyFilePath '" + assemblyFilePath + "' could not be loaded. The file doesn't exist.");
            }

            return null;
        }
    }
}
