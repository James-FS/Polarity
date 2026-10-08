using System;
using System.Collections.Generic;
using Codely.Microsoft.CodeAnalysis;

namespace UnityTcp.Editor.Tools
{
    internal class ScriptFixContext
    {
        public List<string> Imports { get; }
        public List<MetadataReference> References { get; }
        public HashSet<string> AddedLocations { get; }
        public Dictionary<string, HashSet<string>> RejectedNamespaces { get; }
        public List<(string TypeName, string Namespace)> LastAddedImports { get; }
        public List<string> LastAddedLocations { get; }

        public ScriptFixContext(
            List<string> imports,
            List<MetadataReference> references,
            HashSet<string> addedLocations)
        {
            Imports = imports;
            References = references;
            AddedLocations = addedLocations;
            RejectedNamespaces = new Dictionary<string, HashSet<string>>();
            LastAddedImports = new List<(string TypeName, string Namespace)>();
            LastAddedLocations = new List<string>();
        }

        public void NoteAddedReference(string location)
        {
            if (!string.IsNullOrEmpty(location))
                LastAddedLocations.Add(location);
        }

        public void AcceptLastAdded()
        {
            LastAddedImports.Clear();
            LastAddedLocations.Clear();
        }

        public void RejectLastAdded()
        {
            foreach (var added in LastAddedImports)
            {
                Imports.Remove(added.Namespace);
                if (!RejectedNamespaces.TryGetValue(added.TypeName, out var rejected))
                {
                    rejected = new HashSet<string>();
                    RejectedNamespaces[added.TypeName] = rejected;
                }
                rejected.Add(added.Namespace);
            }

            foreach (var location in LastAddedLocations)
            {
                AddedLocations.Remove(location);
                References.RemoveAll(reference =>
                    reference is PortableExecutableReference portable && portable.FilePath == location);
            }

            LastAddedImports.Clear();
            LastAddedLocations.Clear();
        }

        public static bool AddedTypeStillNamed(
            IEnumerable<string> diagnosticTexts,
            IReadOnlyList<(string TypeName, string Namespace)> lastAdded)
        {
            if (lastAdded == null || lastAdded.Count == 0)
                return false;

            foreach (var text in diagnosticTexts)
            {
                if (string.IsNullOrEmpty(text))
                    continue;
                foreach (var added in lastAdded)
                {
                    if (text.IndexOf("'" + added.TypeName + "'", StringComparison.Ordinal) >= 0
                        || text.IndexOf("." + added.TypeName + "'", StringComparison.Ordinal) >= 0)
                        return true;
                }
            }

            return false;
        }
    }
}
