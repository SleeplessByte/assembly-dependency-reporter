﻿using System;
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

            Console.WriteLine("Press a key to exit.");
            Console.ReadKey();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetAssemblyPath"></param>
        /// <returns></returns>
        internal static List<AssemblyInfo> FindReferences(String targetAssemblyPath)
        {
            List<AssemblyInfo> references = new List<AssemblyInfo>();
            Stack<AssemblyInfo> referenceStack = new Stack<AssemblyInfo>();

            var targetLocation = String.Empty;
            // Current assembly (target assembly)
            try
            {
                Assembly assembly = Assembly.LoadFrom(targetAssemblyPath);
                targetLocation = assembly.Location;

                AssemblyName[] currentReferences = assembly.GetReferencedAssemblies();

                // Top should be left most item
                foreach (var currentReference in currentReferences.Reverse())
                    referenceStack.Push(new AssemblyInfo(currentReference.FullName, 1));

                referenceStack.Push(new AssemblyInfo(assembly.FullName));
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
                            referenceStack.Push(new AssemblyInfo(currentReference.FullName, currentInfo.Depth + 1));

                        // Add reference as processed
                        references.Add(currentInfo);

                    }
                    catch (ArgumentNullException)
                    {
                        // Null reference
                        references.Add(new AssemblyInfo(currentInfo.Name, currentInfo.Depth, AssemblyRefStatus.Null));
                    }
                    catch (FileNotFoundException)
                    {
                        // Not found reference
                        references.Add(new AssemblyInfo(currentInfo.Name, currentInfo.Depth, AssemblyRefStatus.NotFound));
                    }
                    catch (BadImageFormatException)
                    {
                        // Assembly invalid
                        references.Add(new AssemblyInfo(currentInfo.Name, currentInfo.Depth, AssemblyRefStatus.BadImage));
                    }
                    catch (SecurityException)
                    {
                        // Permission problem
                        references.Add(new AssemblyInfo(currentInfo.Name, currentInfo.Depth, AssemblyRefStatus.NoPermission));
                    }
                }
            }

            return references;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetAssemblyPath"></param>
        internal static void ReferencesToConsole(String targetAssemblyPath)
        {
            List<AssemblyInfo> references = FindReferences(targetAssemblyPath);

            if (references == null)
            {
                Console.WriteLine("Could not load target assembly.");
                return;
            }
            
            foreach (AssemblyInfo assembly in references)
                Console.WriteLine(assembly.Name.PadLeft(assembly.Name.Length + assembly.Depth * 2, ' '));
        } 

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetAssemblyPath"></param>
        /// <param name="fileName"></param>
        internal static void ReferencesToXML(String targetAssemblyPath, String fileName)
        {
            // Find references
            List<AssemblyInfo> references = FindReferences(targetAssemblyPath);

            if (references == null)
            {
                Console.WriteLine("Could not load target assembly");
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
