using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FixDeclaredType
{
    internal abstract class AbstractFixDeclaredTypeCodeFixProvider<
        TReturnStatementSyntax,
        TReturnExpressionSyntax,
        TMethodDeclarationSyntax,
        TMethodReturnTypeSyntax>
        : CodeFixProvider
        where TReturnStatementSyntax : SyntaxNode
        where TReturnExpressionSyntax : SyntaxNode
        where TMethodDeclarationSyntax : SyntaxNode
        where TMethodReturnTypeSyntax : SyntaxNode
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var cancellationToken = context.CancellationToken;

            var document = context.Document;
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();

            var (existingMethodSyntax, methodReturnType) = TryGetContextInfo(root, context.Span, semanticModel, cancellationToken);

            if (existingMethodSyntax is null || methodReturnType is null
                || methodReturnType.TypeKind == TypeKind.Dynamic)
            {
                return;
            }

            var (returnStatementNode, statementType) = TryGetReturnInfo(
                root, context.Span, syntaxFacts, semanticModel, cancellationToken);

            if (returnStatementNode is null || statementType is null 
                || statementType.IsAnonymousType || statementType.TypeKind == TypeKind.Dynamic)
            {
                return;
            }

            var commonType = GetCommonBaseType(methodReturnType, statementType);

            var newReturnType = commonType.SpecialType == SpecialType.System_Object
                ? statementType
                : commonType;

            var newMethodSyntax = ChangeReturnType(semanticModel, existingMethodSyntax, newReturnType);

            var codeActions = GetCodeActions(document, root, existingMethodSyntax, newMethodSyntax);

            context.RegisterFixes(codeActions, context.Diagnostics);
        }

        protected abstract TMethodDeclarationSyntax ChangeReturnType(
            SemanticModel semanticModel, TMethodDeclarationSyntax currentMethod, INamedTypeSymbol newReturnType);

        protected abstract TMethodReturnTypeSyntax GetMethodReturnType(TMethodDeclarationSyntax methodDeclaration);

        private SyntaxToken GetToken(SyntaxNode root, TextSpan span)
        {
            var position = span.Start;
            var token = root.FindToken(position);

            if (!token.Span.IntersectsWith(position))
            {
                return default;
            }

            if (!span.IsEmpty && span != token.Span)
            {
                return default;
            }

            return token;
        }

        private (TMethodDeclarationSyntax, INamedTypeSymbol) TryGetContextInfo(
            SyntaxNode root, TextSpan span, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            var token = GetToken(root, span);

            for (var currentToken = token; currentToken.RawKind != 0; currentToken = currentToken.GetPreviousToken())
            {
                if (currentToken.Parent is TMethodDeclarationSyntax methodDeclaration)
                {
                    var returnTypeSyntax = GetMethodReturnType(methodDeclaration);
                    var returnType = semanticModel.GetTypeInfo(returnTypeSyntax, cancellationToken).Type as INamedTypeSymbol;

                    return (methodDeclaration, returnType);
                }
            }

            return default;
        }

        private (TReturnStatementSyntax, INamedTypeSymbol) TryGetReturnInfo(
            SyntaxNode root, TextSpan span, ISyntaxFactsService syntaxFacts, SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var token = GetToken(root, span);

            var returnStatementNode = token.Parent as TReturnStatementSyntax;
            if (returnStatementNode is null)
            {
                return default;
            }

            var returnExpression = syntaxFacts.GetExpressionOfReturnStatement(returnStatementNode);

            var returnType = semanticModel.GetTypeInfo(returnExpression, cancellationToken).Type as INamedTypeSymbol;

            return (returnStatementNode, returnType);
        }

        private INamedTypeSymbol GetCommonBaseType(INamedTypeSymbol methodReturnType, INamedTypeSymbol statementReturnType)
        {
            var methodReturnTypeBaseTypes = methodReturnType.GetBaseTypesAndThis().Concat(methodReturnType.AllInterfaces);
            var statementReturnTypeBaseTypes = statementReturnType.GetBaseTypesAndThis().Concat(statementReturnType.AllInterfaces);

            // We want to find the least-derived base of the method return type
            // that also suits the return statement return type.
            foreach (var methodReturnTypeBaseType in methodReturnTypeBaseTypes.Where(t => t.SpecialType != SpecialType.System_ValueType))
            {
                if (statementReturnTypeBaseTypes.Contains(t => SymbolEquivalenceComparer.Instance.Equals(t, methodReturnTypeBaseType)))
                {
                    return methodReturnTypeBaseType;
                }
            }

            return default;
        }

        private IEnumerable<CodeAction> GetCodeActions(
            Document document, SyntaxNode root, SyntaxNode node, SyntaxNode rewrittenNode)
        {
            if (rewrittenNode != node)
            {
                var newRoot = root.ReplaceNode(node, rewrittenNode);
                var newDocument = document.WithSyntaxRoot(newRoot);
                var codeAction = new MyCodeAction("", newDocument);

                return SpecializedCollections.SingletonEnumerable(codeAction);
            }

            return SpecializedCollections.EmptyEnumerable<CodeAction>();
        }

        private class MyCodeAction : CodeAction.DocumentChangeAction
        {
            public MyCodeAction(string title, Document document)
                : base(title, c => Task.FromResult(document))
            {
            }
        }
    }
}
