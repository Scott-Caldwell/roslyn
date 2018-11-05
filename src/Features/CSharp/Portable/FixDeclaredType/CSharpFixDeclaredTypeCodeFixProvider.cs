using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Text;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FixDeclaredType;

namespace Microsoft.CodeAnalysis.CSharp.FixDeclaredType
{
    [ExtensionOrder(After = PredefinedCodeFixProviderNames.ImplementInterface)]
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = PredefinedCodeFixProviderNames.FixDeclaredType)]
    [Shared]
    internal class CSharpFixDeclaredTypeCodeFixProvider : AbstractFixDeclaredTypeCodeFixProvider<
        ReturnStatementSyntax,
        ExpressionSyntax,
        MethodDeclarationSyntax,
        TypeSyntax>
    {
        private const string CS0127 = nameof(CS0127); // Since 'function' returns void, a return keyword must not be followed by an object expression

        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            CS0127);

        protected override MethodDeclarationSyntax ChangeReturnType(
            SemanticModel semanticModel, MethodDeclarationSyntax currentMethod, INamedTypeSymbol newReturnType)
        {
            var newTypeString = newReturnType.ToMinimalDisplayString(semanticModel, currentMethod.ReturnType.SpanStart);
            var newTypeSyntax = SyntaxFactory.ParseTypeName(newTypeString)
                                             .WithTriviaFrom(currentMethod.ReturnType);

            return currentMethod.Update(
                currentMethod.AttributeLists,
                currentMethod.Modifiers,
                newTypeSyntax,
                currentMethod.ExplicitInterfaceSpecifier,
                currentMethod.Identifier,
                currentMethod.TypeParameterList,
                currentMethod.ParameterList,
                currentMethod.ConstraintClauses,
                currentMethod.Body,
                currentMethod.SemicolonToken);
        }

        protected override TypeSyntax GetMethodReturnType(MethodDeclarationSyntax methodDeclaration)
            => methodDeclaration.ReturnType;
    }
}
