using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Quantum.QsCompiler;
using Microsoft.Quantum.QsCompiler.CompilationBuilder;
using Microsoft.Quantum.QsCompiler.DataTypes;
using Microsoft.Quantum.QsCompiler.Documentation;
using Microsoft.Quantum.QsCompiler.SyntaxTree;
using Microsoft.Quantum.QsCompiler.Transformations.Core;


namespace Kaiser.Quantum.CompilerExtensions
{
    internal class ExamplesInDocs : SyntaxTreeTransformation<List<string>>
    {
        public ExamplesInDocs() : base(new List<string>(), TransformationOptions.NoRebuild)
        {
            this.Namespaces = new NamespaceTransformation(this);
            this.Statements = new StatementTransformation<List<string>>(this, TransformationOptions.Disabled);
        }

        public static List<string> Extract(QsCompilation compilation)
        {
            var instance = new ExamplesInDocs();
            instance.Apply(compilation);
            return instance.SharedState;
        }

        private class NamespaceTransformation : NamespaceTransformation<List<string>>
        {
            public NamespaceTransformation(ExamplesInDocs parent) : base(parent, TransformationOptions.NoRebuild)
            { }

            public override ImmutableArray<string> OnDocumentation(ImmutableArray<string> doc)
            {
                var docComment = new DocComment(doc);
                var lines = docComment.Example
                    .Split('\n')
                    .SkipWhile(line => !line.Trim().StartsWith("```"))
                    .Skip(1)
                    .TakeWhile(line =>!line.Trim().StartsWith("```"));

                this.SharedState.Add(String.Join(Environment.NewLine, lines));
                return doc;
            }
        }
    }

    public class DocsToTest : IRewriteStep
    {
        private List<IRewriteStep.Diagnostic> Diagnostics;
        private const string TestNamespaceName = "Kaiser.Quantum.CompilerExtensions.DocsToTest";

        // interface properties

        public string Name => "DocToTest";
        public int Priority => 0; // doesn't matter

        public IDictionary<string, string> AssemblyConstants { get; }
        public IEnumerable<IRewriteStep.Diagnostic> GeneratedDiagnostics =>
            this.Diagnostics;

        public bool ImplementsPreconditionVerification => false;
        public bool ImplementsTransformation => true;
        public bool ImplementsPostconditionVerification => false;

        public DocsToTest()
        {
            this.AssemblyConstants = new Dictionary<string, string>();
            this.Diagnostics = new List<IRewriteStep.Diagnostic>();
        }

        public bool Transformation(QsCompilation compilation, out QsCompilation transformed)
        {
            transformed = compilation;
            var examples = ExamplesInDocs.Extract(compilation);
            var intro = $"namespace {DocsToTest.TestNamespaceName}";
            var sourceCode = intro + "{" + String.Join(Environment.NewLine, examples) + "}";
            var manager = new CompilationUnitManager();
            if (compilation.Namespaces.Any(ns => ns.Name.Value == DocsToTest.TestNamespaceName)) return false;

            // let's get using some references
            var refName = NonNullable<string>.New(Path.GetFullPath("__GeneratedReferencesForDocsToTest__"));
            var refHeaders = new References.Headers(refName, compilation.Namespaces);
            var refDict = new Dictionary<NonNullable<string>, References.Headers>();
            refDict.Add(refName, refHeaders);
            // todo: we need to change the line ending before building the references
            var references = new References(refDict.ToImmutableDictionary());
            manager.UpdateReferencesAsync(references);

            // get source code from examples
            var sourceName = NonNullable<string>.New(Path.GetFullPath("__GeneratedSourceForDocsToTest__.qs"));
            if (!CompilationUnitManager.TryGetUri(sourceName, out var sourceUri)) return false;
            var fileManager = CompilationUnitManager.InitializeFileManager(sourceUri, sourceCode);
            manager.AddOrUpdateSourceFileAsync(fileManager);

            var built = manager.Build();
            if (built.Diagnostics().Any()) // todo: only for errors 
            {
                this.Diagnostics.Add(new IRewriteStep.Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Message = $"Error while compiling modification for {this.Name}",
                    Stage = IRewriteStep.Stage.PreconditionVerification
                });
                return false;
            }

            //if (!built.SyntaxTree.TryGetValue(NonNullable<string>.New(DocsToTest.TestNamespaceName), out var testNs)) return false;
            //transformed = new QsCompilation(compilation.Namespaces.Add(testNs), compilation.EntryPoints);

            transformed = built.BuiltCompilation;
            return true;
        }


        public bool PostconditionVerification(QsCompilation compilation) =>
            throw new NotImplementedException();

        public bool PreconditionVerification(QsCompilation compilation) =>
            throw new NotImplementedException();

    }
}
