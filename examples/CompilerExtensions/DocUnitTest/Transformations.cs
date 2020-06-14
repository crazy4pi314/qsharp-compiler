using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Quantum.QsCompiler.DataTypes;
using Microsoft.Quantum.QsCompiler.Documentation;
using Microsoft.Quantum.QsCompiler.SyntaxTree;
using Core = Microsoft.Quantum.QsCompiler.Transformations.Core;


namespace Kaiser.Quantum.QsCompiler.Transformations
{
    public class ExamplesInDocs 
    : Core.SyntaxTreeTransformation<List<string>>
    {
        public static List<string> Extract(QsCompilation compilation)
        {
            var instance = new ExamplesInDocs();
            instance.Apply(compilation);
            return instance.SharedState;
        }

        private ExamplesInDocs() 
        : base(new List<string>(), Core.TransformationOptions.NoRebuild)
        {
            this.Namespaces = new NamespaceTransformation(this);
            this.Statements = new Core.StatementTransformation<List<string>>(this, Core.TransformationOptions.Disabled);
            this.Expressions = new Core.ExpressionTransformation<List<string>>(this, Core.TransformationOptions.Disabled);
            this.Types = new Core.TypeTransformation<List<string>>(this, Core.TransformationOptions.Disabled);
        }

        private class NamespaceTransformation 
        : Core.NamespaceTransformation<List<string>>
        {
            public NamespaceTransformation(ExamplesInDocs parent) 
            : base(parent, Core.TransformationOptions.NoRebuild)
            { }

            public override ImmutableArray<string> OnDocumentation(ImmutableArray<string> doc)
            {
                var example = new DocComment(doc).Example; 
                while (example.Any())
                {
                    example = String.Concat(example
                        .Substring(example.IndexOf("```") + 3)
                        .SkipWhile(c => !Char.IsWhiteSpace(c)));
                    var next = example.IndexOf("```");
                    this.SharedState.Add(example.Substring(0, next));
                    example = String.Concat(example
                        .Substring(next + 3)
                        .SkipWhile(c => !Char.IsWhiteSpace(c)));
                }
                return doc;
            }
        }
    }

    public class DllToQs 
    : Core.SyntaxTreeTransformation
    {
        private static readonly DllToQs Instance = new DllToQs();
        public static QsCompilation Rename(QsCompilation compilation) => 
            Instance.Apply(compilation);

        private DllToQs() 
        : base()
        {
            this.Namespaces = new NamespaceTransformation(this);
            this.Statements = new Core.StatementTransformation(this, Core.TransformationOptions.Disabled);
            this.Expressions = new Core.ExpressionTransformation(this, Core.TransformationOptions.Disabled);
            this.Types = new Core.TypeTransformation(this, Core.TransformationOptions.Disabled);
        }

        private class NamespaceTransformation : Core.NamespaceTransformation
        {
            public NamespaceTransformation(DllToQs parent) 
            : base(parent)
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