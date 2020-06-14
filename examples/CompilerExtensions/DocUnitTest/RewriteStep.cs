using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Quantum.QsCompiler;
using Microsoft.Quantum.QsCompiler.CompilationBuilder;
using Microsoft.Quantum.QsCompiler.DataTypes;
using Microsoft.Quantum.QsCompiler.SyntaxTree;
using Microsoft.Quantum.QsCompiler.Transformations.BasicTransformations;


namespace Kaiser.Quantum.CompileExtensions.DocToTest
{
    public class DocToTest : IRewriteStep
    {
        private readonly List<IRewriteStep.Diagnostic> Diagnostics;
        private const string TestNamespaceName = "Kaiser.Quantum.CompilerExtensions.DocsToTest";
        private static readonly FilterBySourceFile FilterSourceFiles = new FilterBySourceFile(source => source.Value.EndsWith(".qs"));

        // interface properties

        public string Name => "DocToTest";
        public int Priority => 0; // doesn't matter

        public IDictionary<string, string> AssemblyConstants { get; }
        public IEnumerable<IRewriteStep.Diagnostic> GeneratedDiagnostics =>
            this.Diagnostics;

        public bool ImplementsPreconditionVerification => false;
        public bool ImplementsTransformation => true;
        public bool ImplementsPostconditionVerification => false;

        public DocToTest()
        {
            this.AssemblyConstants = new Dictionary<string, string>();
            this.Diagnostics = new List<IRewriteStep.Diagnostic>();
        }

        public bool Transformation(QsCompilation compilation, out QsCompilation transformed)
        {
            transformed = FilterSourceFiles.Apply(compilation);
            if (compilation.Namespaces.Any(ns => ns.Name.Value == TestNamespaceName)) return false;
            var manager = new CompilationUnitManager();

            // get source code from examples

            var examples = ExamplesInDocs.Extract(transformed).Where(ex => !String.IsNullOrWhiteSpace(ex));
            var (pre, post) = ($"namespace {TestNamespaceName}{{ {Environment.NewLine}", $"{Environment.NewLine}}}");
            var sourceCode = pre + String.Join(Environment.NewLine, examples) + post + Environment.NewLine;

            var sourceName = NonNullable<string>.New(Path.GetFullPath("__GeneratedSourceForDocsToTest__.qs"));
            if (!CompilationUnitManager.TryGetUri(sourceName, out var sourceUri)) return false;
            var fileManager = CompilationUnitManager.InitializeFileManager(sourceUri, sourceCode);
            manager.AddOrUpdateSourceFileAsync(fileManager);

            // get everything contained in the compilation as references

            var refName = NonNullable<string>.New(Path.GetFullPath("__GeneratedReferencesForDocsToTest__"));
            var refHeaders = new References.Headers(refName, DllToQs.Rename(compilation).Namespaces);
            var refDict = new Dictionary<NonNullable<string>, References.Headers>{{ refName, refHeaders }};
            var references = new References(refDict.ToImmutableDictionary());
            manager.UpdateReferencesAsync(references);

            var built = manager.Build();
            var diagnostics = built.Diagnostics();
            foreach (var d in diagnostics) Console.WriteLine(d.Message);

            if (built.Diagnostics().Any()) // todo: only for errors 
            {
                this.Diagnostics.Add(new IRewriteStep.Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Message = $"Error while compiling modification for {this.Name}",
                    Stage = IRewriteStep.Stage.Transformation
                });
                return false;
            }

            if (!built.SyntaxTree.TryGetValue(NonNullable<string>.New(TestNamespaceName), out var testNs)) return false;
            transformed = new QsCompilation(compilation.Namespaces.Add(testNs), compilation.EntryPoints);
            return true;
        }


        public bool PostconditionVerification(QsCompilation compilation) =>
            throw new NotImplementedException();

        public bool PreconditionVerification(QsCompilation compilation) =>
            throw new NotImplementedException();

    }
}
