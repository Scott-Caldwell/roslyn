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

            var diagnostic = context.Diagnostics.First();
            var initialNode = root.FindNode(diagnostic.Location.SourceSpan);

            var (existingMethodSyntax, methodReturnType) = TryGetContextInfo(initialNode, semanticModel, cancellationToken);

            if (existingMethodSyntax is null || methodReturnType is null
                || methodReturnType.TypeKind == TypeKind.Dynamic)
            {
                return;
            }

            var (returnStatementNode, statementType) = TryGetReturnInfo(
                initialNode, syntaxFacts, semanticModel, cancellationToken);

            if (returnStatementNode is null || statementType is null
                || statementType.TypeKind == TypeKind.Dynamic)
            {
                return;
            }

            var commonType = GetCommonBaseType(methodReturnType, statementType);

            var candidateReturnType = commonType.SpecialType == SpecialType.System_Object && !statementType.IsAnonymousType
                ? statementType
                : commonType;

            var newReturnType = ResolveTypeForMethod(syntaxFacts, candidateReturnType);

            var newMethodSyntax = ChangeReturnType(semanticModel, existingMethodSyntax, candidateReturnType);

            var codeActions = GetCodeActions(document, root, existingMethodSyntax, newMethodSyntax);

            context.RegisterFixes(codeActions, context.Diagnostics);
        }

        protected abstract TMethodDeclarationSyntax ChangeReturnType(
            SemanticModel semanticModel, TMethodDeclarationSyntax currentMethod, INamedTypeSymbol newReturnType);

        protected abstract TMethodReturnTypeSyntax GetMethodReturnType(TMethodDeclarationSyntax methodDeclaration);

        protected abstract INamedTypeSymbol ResolveTypeForMethod(
            ISyntaxFactsService syntaxFacts, TMethodDeclarationSyntax method, INamedTypeSymbol candidateReturnType);

        private (TMethodDeclarationSyntax, INamedTypeSymbol) TryGetContextInfo(
            SyntaxNode initialNode, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            for (var currentNode = initialNode; currentNode != null; currentNode = currentNode.Parent)
            {
                if (currentNode is TMethodDeclarationSyntax methodDeclaration)
                {
                    var returnTypeSyntax = GetMethodReturnType(methodDeclaration);
                    var returnType = semanticModel.GetTypeInfo(returnTypeSyntax, cancellationToken).Type as INamedTypeSymbol;

                    return (methodDeclaration, returnType);
                }
            }

            return default;
        }

        private (TReturnStatementSyntax, INamedTypeSymbol) TryGetReturnInfo(
            SyntaxNode initialNode, ISyntaxFactsService syntaxFacts, SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var returnStatementNode = initialNode.GetAncestorOrThis<TReturnStatementSyntax>();
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
            var statementReturnTypeBaseTypes = statementReturnType.GetBaseTypesAndThis();

            // Need to get the least-derived base type of the method return type
            // that also suits the statement type.
            foreach (var currentType in methodReturnType.GetBaseTypesAndThis()
                                                        .Where(t => t.SpecialType != SpecialType.System_ValueType))
            {
                if (statementReturnTypeBaseTypes.Contains(t => SymbolEquivalenceComparer.Instance.Equals(currentType, t)))
                {
                    return currentType;
                }

                foreach (var currentInterface in currentType.Interfaces)
                {
                    if (statementReturnType.AllInterfaces.Contains(t => SymbolEquivalenceComparer.Instance.Equals(currentInterface, t)))
                    {
                        return currentInterface;
                    }
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
