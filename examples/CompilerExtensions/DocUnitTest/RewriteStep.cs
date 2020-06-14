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
using VS = Microsoft.VisualStudio.LanguageServer.Protocol;


namespace Kaiser.Quantum.QsCompiler.Extensions.DocsToTests
{
    public class DocsToTests : IRewriteStep
    {
        private const string TestNamespaceName = "Kaiser.Quantum.CompilerExtensions.DocsToTests";
        private const string CodeSource = "__GeneratedSourceForDocsToTests__.g.qs";
        private const string ReferenceSource = "__GeneratedReferencesForDocsToTests__.g.dll";

        private readonly List<IRewriteStep.Diagnostic> Diagnostics =
            new List<IRewriteStep.Diagnostic>();

        private static readonly FilterBySourceFile FilterSourceFiles = 
            new FilterBySourceFile(source => source.Value.EndsWith(".qs"));

        // interface properties

        public string Name => "DocsToTests";
        public int Priority => 0; // doesn't matter

        public IDictionary<string, string> AssemblyConstants => null;
        public IEnumerable<IRewriteStep.Diagnostic> GeneratedDiagnostics =>
            this.Diagnostics;

        public bool ImplementsPreconditionVerification => false;
        public bool ImplementsTransformation => true;
        public bool ImplementsPostconditionVerification => false;

        // interface methods

        public bool Transformation(QsCompilation compilation, out QsCompilation transformed)
        {
            transformed = FilterSourceFiles.Apply(compilation);
            if (compilation.Namespaces.Any(ns => ns.Name.Value == TestNamespaceName)) return false;
            var manager = new CompilationUnitManager();

            // get source code from examples

            var examples = ExamplesInDocs.Extract(transformed).Where(ex => !String.IsNullOrWhiteSpace(ex));
            var (pre, post) = ($"namespace {TestNamespaceName}{{ {Environment.NewLine}", $"{Environment.NewLine}}}");
            var sourceCode = pre + String.Join(Environment.NewLine, examples) + post + Environment.NewLine;

            var sourceName = NonNullable<string>.New(Path.GetFullPath(CodeSource));
            if (!CompilationUnitManager.TryGetUri(sourceName, out var sourceUri)) return false;
            var fileManager = CompilationUnitManager.InitializeFileManager(sourceUri, sourceCode);
            manager.AddOrUpdateSourceFileAsync(fileManager);

            // get everything contained in the compilation as references

            var refName = NonNullable<string>.New(Path.GetFullPath(ReferenceSource));
            var refHeaders = new References.Headers(refName, DllToQs.Rename(compilation).Namespaces);
            var refDict = new Dictionary<NonNullable<string>, References.Headers>{{ refName, refHeaders }};
            var references = new References(refDict.ToImmutableDictionary());
            manager.UpdateReferencesAsync(references);

            // compile the examples in the doc comments and add any diagnostics to the list of generated diagnostics

            var built = manager.Build();
            var diagnostics = built.Diagnostics();
            //this.Diagnostics.AddRange(diagnostics.Select(d => IRewriteStep.Diagnostic.Create(d, IRewriteStep.Stage.Transformation)));
            if (diagnostics.Any(d => d.Severity == VS.DiagnosticSeverity.Error)) return false;
            if (!built.SyntaxTree.TryGetValue(NonNullable<string>.New(TestNamespaceName), out var testNs)) return false;

            // Todo: we need to mark all elements in the newly created namespace with a suitable test attribute

            transformed = new QsCompilation(compilation.Namespaces.Add(testNs), compilation.EntryPoints);
            return true;
        }


        public bool PostconditionVerification(QsCompilation compilation) =>
            throw new NotImplementedException();

        public bool PreconditionVerification(QsCompilation compilation) =>
            throw new NotImplementedException();

    }
}
