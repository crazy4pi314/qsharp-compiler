using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Quantum.QsCompiler;
using Microsoft.Quantum.QsCompiler.DataTypes;
using Microsoft.Quantum.QsCompiler.Documentation;
using Microsoft.Quantum.QsCompiler.SyntaxTree;
using Core = Microsoft.Quantum.QsCompiler.Transformations.Core;
using BuiltIn = Microsoft.Quantum.QsCompiler.BuiltIn;


namespace Kaiser.Quantum.QsCompiler.Transformations
{
    using CallablePredicate = Func<QsCallable, bool>;
    using AttributeId = QsNullable<UserDefinedType>;
    using QsRangeInfo = QsNullable<Tuple<QsPositionInfo, QsPositionInfo>>;


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

    public class TestAttribute
    : Core.SyntaxTreeTransformation<CallablePredicate>
    {
        private static readonly AttributeId TypeId =
            AttributeId.NewValue(new UserDefinedType(BuiltIn.Test.FullName.Namespace, BuiltIn.Test.FullName.Name, QsRangeInfo.Null));

        private static TypedExpression Argument(string target) =>
            SyntaxGenerator.StringLiteral(NonNullable<string>.New(target), ImmutableArray<TypedExpression>.Empty);

        public static QsCompilation AddToCallables(
            QsCompilation compilation, // AttributeId attributeId, TypedExpression attributeArg,
            CallablePredicate predicate = null) =>
                new TestAttribute(predicate).Apply(compilation);

        private TestAttribute(CallablePredicate predicate = null)
        : base(predicate)
        {
            this.Namespaces = new NamespaceTransformation(this);
            this.Statements = new Core.StatementTransformation<CallablePredicate>(this, Core.TransformationOptions.Disabled);
            this.Expressions = new Core.ExpressionTransformation<CallablePredicate>(this, Core.TransformationOptions.Disabled);
            this.Types = new Core.TypeTransformation<CallablePredicate>(this, Core.TransformationOptions.Disabled);
        }

        private class NamespaceTransformation
        : Core.NamespaceTransformation<CallablePredicate>
        {
            public NamespaceTransformation(TestAttribute parent)
            : base(parent)
            { }

            public override QsCallable OnCallableDeclaration(QsCallable c)
            {
                if (!SharedState?.Invoke(c) ?? false) return c;
                var qsimAtt = new QsDeclarationAttribute(TypeId, Argument("QuantumSimulator"), null, QsComments.Empty); // FIXME: null
                var rsimAtt = new QsDeclarationAttribute(TypeId, Argument("ResourcesEstimator"), null, QsComments.Empty); // FIXME: null
                return c.AddAttribute(qsimAtt).AddAttribute(rsimAtt);
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