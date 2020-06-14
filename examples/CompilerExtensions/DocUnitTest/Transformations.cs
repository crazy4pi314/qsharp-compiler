using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Quantum.QsCompiler.DataTypes;
using Microsoft.Quantum.QsCompiler.Documentation;
using Microsoft.Quantum.QsCompiler.SyntaxTree;
using Microsoft.Quantum.QsCompiler.Transformations.Core;


namespace Kaiser.Quantum.CompileExtensions.DocToTest
{
    internal class ExamplesInDocs : SyntaxTreeTransformation<List<string>>
    {
        public ExamplesInDocs() : base(new List<string>(), TransformationOptions.NoRebuild)
        {
            this.Namespaces = new NamespaceTransformation(this);
            this.Statements = new StatementTransformation<List<string>>(this, TransformationOptions.Disabled);
            this.Expressions = new ExpressionTransformation<List<string>>(this, TransformationOptions.Disabled);
            this.Types = new TypeTransformation<List<string>>(this, TransformationOptions.Disabled);
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
                IEnumerable<string> examples = docComment.Example.Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries);
                while (examples.Any())
                {
                    var lines = examples
                        .SkipWhile(line => !line.Trim().StartsWith("```"))
                        .Skip(1)
                        .TakeWhile(line => !line.Trim().StartsWith("```"))
                        .ToArray();
                    examples = examples.Skip(lines.Length + 1);
                    this.SharedState.Add(String.Join(Environment.NewLine, lines));
                }
                return doc;
            }
        }
    }

    internal class DllToQs : SyntaxTreeTransformation
    {
        private static readonly DllToQs Instance = new DllToQs();
        public static QsCompilation Rename(QsCompilation compilation) => Instance.Apply(compilation);

        internal DllToQs() : base()
        {
            this.Namespaces = new RenameSources(this);
            this.Statements = new StatementTransformation(this, TransformationOptions.Disabled);
            this.Expressions = new ExpressionTransformation(this, TransformationOptions.Disabled);
            this.Types = new TypeTransformation(this, TransformationOptions.Disabled);
        }

        private class RenameSources : NamespaceTransformation
        {
            public RenameSources(DllToQs parent) : base(parent)
            { }

            public override NonNullable<string> OnSourceFile(NonNullable<string> f)
            {
                var dir = Path.GetDirectoryName(f.Value);
                var fileName = Path.GetFileNameWithoutExtension(f.Value);
                return NonNullable<string>.New(Path.Combine(dir, fileName + ".qs"));
            }
        }
    }
}