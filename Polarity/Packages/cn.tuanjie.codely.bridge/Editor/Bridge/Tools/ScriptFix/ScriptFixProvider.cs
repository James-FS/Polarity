using Codely.Microsoft.CodeAnalysis;
using Codely.Microsoft.CodeAnalysis.CSharp;
using CodelySyntaxTree = Codely.Microsoft.CodeAnalysis.SyntaxTree;

namespace UnityTcp.Editor.Tools
{
    internal abstract class ScriptFixProvider
    {
        public abstract bool CanFix(Diagnostic diagnostic);

        // Returns true if a fix was applied.
        // `tree` is updated via ref; context.Imports / context.References may be mutated.
        public abstract bool ApplyFix(ref CodelySyntaxTree tree, Diagnostic diagnostic, ScriptFixContext context);
    }
}
