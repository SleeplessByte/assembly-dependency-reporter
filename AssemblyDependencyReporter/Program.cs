using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Security;
using System.Xml;

namespace AssemblyDependencyReporter
{
    class Program
    {
        static void Main(String[] args)
        {
            // Process args
            var fileName = String.Empty;
            if (args.Length == 0)
            {
                Console.WriteLine("Enter path to target assembly now!");
                fileName = Console.ReadLine().Replace("\n","").Replace("\r", "");
            }
            else
            {
                fileName = args[0];
            } 
            
            var output = args.Contains("-o");

            // output
            if (output)
                ReferencesToXML(fileName, "Results.xml");
            ReferencesToConsole(fileName);

            // Running from console without args
            if (args.Length == 0)
            {
                Console.WriteLine("Press a key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Finds all references of target assembly
        /// </summary>
        /// <param name="targetAssemblyPath">Path to target assembly</param>
        /// <returns></returns>
        public static List<AssemblyInfo> FindReferences(String targetAssemblyPath)
        {
            List<AssemblyInfo> references = new List<AssemblyInfo>();
            Stack<AssemblyInfo> referenceStack = new Stack<AssemblyInfo>();

            var targetLocation = String.Empty;
            // Current assembly (target assembly)
            try
            {
                // Load in context
                Assembly assembly = Assembly.LoadFile(targetAssemblyPath);
                AssemblyName[] currentReferences = assembly.GetReferencedAssemblies();

                // Top should be left most item (in context dependencies)
                foreach (var currentReference in currentReferences.Reverse())
                    referenceStack.Push(new AssemblyInfo(currentReference.FullName, currentReference.Name, 1));

                references.Add(new AssemblyInfo(assembly.FullName, assembly.GetName().Name));
                targetLocation = assembly.Location;
            }
            catch (Exception) // target assembly problem
            {
                return null;
            }

            // Pop stack until empty
            while (referenceStack.Count > 0)
            {
                // Check for cycles
                var currentInfo = referenceStack.Pop();
                if (!references.Contains(currentInfo))
                {

                    // Find children
                    try
                    {
                        Assembly currentAssembly = Assembly.Load(currentInfo.Name);
                        AssemblyName[] currentReferences = currentAssembly.GetReferencedAssemblies();

                        // Top should be left most item
                        foreach (var currentReference in currentReferences.Reverse())
                            referenceStack.Push(new AssemblyInfo(currentReference.FullName, currentReference.Name, currentInfo.Depth + 1));

                        // Add reference as processed
                        references.Add(currentInfo);
                    }
                    catch (ArgumentNullException)
                    {
                        // Null reference
                        references.Add(new AssemblyInfo(currentInfo.Name, currentInfo.DisplayName, currentInfo.Depth, AssemblyRefStatus.Null));
                    }
                    catch (FileNotFoundException)
                    {
                        // We didn't found the assembly in the PATH, COM or .NET so now we are rescueing
                        // and trying to find it in any of the subdirectories. This might be taking long 
                        // and might not find the thing you are looking for, but it's a short shot.
                        var path = targetLocation;
                        var fileStack = new Stack<String>();
                        var dirStack = new Stack<String>();

                        Assembly foundAssembly = null;
                        dirStack.Push(new FileInfo(path).Directory.FullName);

                        // Get some directory
                        while (dirStack.Count > 0)
                        {
                            var dir = dirStack.Pop();
                            // Push the files on the stack
                            foreach (var efile in Directory.EnumerateFiles(dir))
                                fileStack.Push(efile);

                            while (fileStack.Count > 0)
                            {
                                var file = fileStack.Pop();
                                
                                // If assembly capable
                                FileInfo info = new FileInfo(file);
                                if (new[] { ".dll", ".exe" }.Any(ext => ext == info.Extension))
                                {
                                    try
                                    {
                                        var testAssembly = Assembly.LoadFrom(file);
                                        if (testAssembly.FullName == currentInfo.Name)
                                        {
                                            foundAssembly = testAssembly;
                                            break;
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        continue;
                                    }
                                }
                            }

                            if (foundAssembly != null)
                                break;

                            foreach (var edir in Directory.EnumerateDirectories(dir))
                                dirStack.Push(edir);
                        }

                        // Yes found it!
                        if (foundAssembly != null)
                        {
                            AssemblyName[] currentReferences = foundAssembly.GetReferencedAssemblies();

                            // Top should be left most item
                            foreach (var currentReference in currentReferences.Reverse())
                                referenceStack.Push(new AssemblyInfo(currentReference.FullName, currentReference.Name, currentInfo.Depth + 1));

                            // Add reference as processed
                            references.Add(currentInfo);
                            continue;
                        }

                        // Not found reference
                        references.Add(new AssemblyInfo(currentInfo.Name, currentInfo.DisplayName, currentInfo.Depth, AssemblyRefStatus.NotFound));
                    }
                    catch (BadImageFormatException)
                    {
                        // Assembly invalid
                        references.Add(new AssemblyInfo(currentInfo.Name, currentInfo.DisplayName, currentInfo.Depth, AssemblyRefStatus.BadImage));
                    }
                    catch (SecurityException)
                    {
                        // Permission problem
                        references.Add(new AssemblyInfo(currentInfo.Name, currentInfo.DisplayName, currentInfo.Depth, AssemblyRefStatus.NoPermission));
                    }
                }
            }

            return references;
        }

        /// <summary>
        /// Finds references and outputs to console
        /// </summary>
        /// <param name="targetAssemblyPath"></param>
        internal static void ReferencesToConsole(String targetAssemblyPath, List<AssemblyInfo> references = null)
        {
            references = references == null ? FindReferences(targetAssemblyPath) : references;

            if (references == null)
            {
                Console.WriteLine("Could not load target assembly.");
                return;
            }

            // Tree
            foreach (AssemblyInfo assembly in references)
            {
                var status = assembly.DisplayName + (assembly.Status == AssemblyRefStatus.None ? String.Empty : " with status: " + assembly.Status);
                Console.WriteLine(status.PadLeft(status.Length + assembly.Depth * 2, ' '));
            }

            Console.WriteLine(String.Format("{0} assemblies", references.Count));

            // Errors
            foreach (AssemblyInfo assembly in references)
            {
                if (assembly.Status == AssemblyRefStatus.None )
                    continue;
                Console.WriteLine(String.Format("{0}: {1}", assembly.Status, assembly.Name));
            }
        } 

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetAssemblyPath"></param>
        /// <param name="fileName"></param>
        internal static void ReferencesToXML(String targetAssemblyPath, String fileName, List<AssemblyInfo> references = null)
        {
            // Find references
            references = references == null ? FindReferences(targetAssemblyPath) : references;

            if (references == null)
            {
                Console.WriteLine("Could not load target assembly.");
                return;
            }

            XmlTextWriter xtw = new XmlTextWriter(fileName, Encoding.Unicode);
            xtw.Formatting = Formatting.Indented;

            xtw.WriteStartDocument();
            xtw.WriteStartElement("Assemblies");

            Int32 pendingEndTags = 0;
            for (Int32 i = 0; i < references.Count - 1; ++i) // all but last node
            {
                AssemblyInfo current = references[i];
                AssemblyInfo next = references[i + 1];

                xtw.WriteStartElement("Assembly");
                xtw.WriteAttributeString("Name", current.Name);
                if (current.Status != AssemblyRefStatus.None)
                    xtw.WriteAttributeString("Status", current.Status.ToString());

                // Current has same parent as next
                if (current.Depth == next.Depth)
                {
                    xtw.WriteEndElement();
                }

                // Current has a child
                else if (current.Depth < next.Depth)
                {
                    ++pendingEndTags;
                }

                // Move up in the tree (no more childs or siblings)
                else
                {
                    xtw.WriteEndElement();

                    // Close until at level of next element
                    for (Int32 j = 0; j < current.Depth - next.Depth; ++j)
                    {
                        xtw.WriteEndElement();
                        --pendingEndTags;
                    }
                }
            }

            // Final node
            AssemblyInfo last = references.Last();
            xtw.WriteStartElement("Assembly");
            xtw.WriteAttributeString("Name", last.Name);
            if (last.Status != AssemblyRefStatus.None)
                xtw.WriteAttributeString("Status", last.Status.ToString());
            xtw.WriteEndElement();

            // Close all tags
            while (pendingEndTags-- > 0)
                xtw.WriteEndElement();

            xtw.WriteEndElement();
            xtw.Close();
        }
    }
}
