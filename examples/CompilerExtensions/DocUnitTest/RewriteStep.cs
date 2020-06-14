using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Kaiser.Quantum.QsCompiler.Transformations;
using Microsoft.CodeAnalysis;
using Microsoft.Quantum.QsCompiler;
using Microsoft.Quantum.QsCompiler.CompilationBuilder;
using Microsoft.Quantum.QsCompiler.DataTypes;
using Microsoft.Quantum.QsCompiler.SyntaxTree;
using Microsoft.Quantum.QsCompiler.Transformations;
using Microsoft.Quantum.QsCompiler.Transformations.BasicTransformations;
using VS = Microsoft.VisualStudio.LanguageServer.Protocol;
using Constants = Microsoft.Quantum.QsCompiler.ReservedKeywords.AssemblyConstants;


namespace Kaiser.Quantum.QsCompiler.Extensions.DocsToTests
{
    public class DocsToTests : IRewriteStep
    {
        private const string TestNamespaceName = "Kaiser.Quantum.CompilerExtensions.DocsToTests";
        private const string CodeSource = "__GeneratedSourceForDocsToTests__.g.qs";
        private const string ReferenceSource = "__GeneratedReferencesForDocsToTests__.g.dll";

        private static readonly IEnumerable<string> OpenedForTesting = new[] {
            BuiltIn.CoreNamespace.Value,
            BuiltIn.IntrinsicNamespace.Value,
            BuiltIn.DiagnosticsNamespace.Value,
            BuiltIn.CanonNamespace.Value,
            BuiltIn.StandardArrayNamespace.Value
        }.ToImmutableArray();

        private readonly List<IRewriteStep.Diagnostic> Diagnostics =
            new List<IRewriteStep.Diagnostic>();

        private static readonly FilterBySourceFile FilterSourceFiles = 
            new FilterBySourceFile(source => source.Value.EndsWith(".qs"));

        private static bool ContainsNamespace(QsCompilation compilation, string nsName) =>
            compilation.Namespaces.Any(ns => ns.Name.Value == nsName);

        private string WrapInTestNamespace(IEnumerable<string> examples, QsCompilation compilation)
        {
            var (pre, post) = ($"namespace {TestNamespaceName}{{ {Environment.NewLine}", $"{Environment.NewLine}}}");
            var openDirs = OpenedForTesting
                .Where(nsName => ContainsNamespace(compilation, nsName))
                .Select(nsName => $"open {nsName};");            
            var content = String.Join(Environment.NewLine, openDirs.Concat(examples));
            var sourceCode = pre + content + post + Environment.NewLine;
            return sourceCode;
        }

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
            if (ContainsNamespace(compilation, TestNamespaceName)) return false;
            var manager = new CompilationUnitManager();

            // get source code from examples

            var examples = ExamplesInDocs.Extract(transformed).Where(ex => !String.IsNullOrWhiteSpace(ex));
            var sourceCode = WrapInTestNamespace(examples, compilation);
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
            this.Diagnostics.AddRange(diagnostics.Select(d => IRewriteStep.Diagnostic.Create(d, IRewriteStep.Stage.Transformation)));
            if (diagnostics.Any(d => d.Severity == VS.DiagnosticSeverity.Error)) return false;
            if (!built.SyntaxTree.TryGetValue(NonNullable<string>.New(TestNamespaceName), out var testNs)) return false;

            // mark all callables in the newly created namespace as unit tests to run on the QuantumSimulator and ResourcesEstimator

            static bool InTestNamespace(QsCallable c) => c.FullName.Namespace.Value == TestNamespaceName;
            var qsimAtt = Attributes.BuildAttribute(BuiltIn.Test.FullName, Attributes.StringArgument(Constants.QuantumSimulator));
            var restAtt = Attributes.BuildAttribute(BuiltIn.Test.FullName, Attributes.StringArgument(Constants.ResourcesEstimator));
            transformed = new QsCompilation(compilation.Namespaces.Add(testNs), compilation.EntryPoints);
            transformed = Attributes.AddToCallables(transformed, (qsimAtt, InTestNamespace), (restAtt, InTestNamespace));
            return true;
        }


        public bool PostconditionVerification(QsCompilation compilation) =>
            throw new NotImplementedException();

        public bool PreconditionVerification(QsCompilation compilation) =>
            throw new NotImplementedException();

    }
}
