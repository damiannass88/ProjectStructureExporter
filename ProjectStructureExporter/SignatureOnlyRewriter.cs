using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace ProjectStructureExporter
{
 // Rewriter that converts class/struct/interface/record members to signatures only (no bodies)
 public sealed class SignatureOnlyRewriter : CSharpSyntaxRewriter
 {
 public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
 {
 // Remove body, keep semicolon
 var updated = node
 .WithBody(null)
 .WithExpressionBody(null)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
 return updated;
 }

 public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
 {
 var updated = node
 .WithBody(null)
 .WithInitializer(node.Initializer) // keep initializer
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
 return updated;
 }

 public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node)
 {
 var updated = node
 .WithBody(null)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
 return updated;
 }

 public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
 {
 var updated = node
 .WithBody(null)
 .WithExpressionBody(null)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
 return updated;
 }

 public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
 {
 var updated = node
 .WithBody(null)
 .WithExpressionBody(null)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
 return updated;
 }

 public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
 {
 // If property has accessors, convert each to semicolon-only
 if (node.AccessorList != null)
 {
 var newAccessors = SyntaxFactory.AccessorList(
 SyntaxFactory.List(
 node.AccessorList.Accessors.Select(a => a
 .WithBody(null)
 .WithExpressionBody(null)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
 )));
 return node.WithAccessorList(newAccessors).WithExpressionBody(null).WithSemicolonToken(default);
 }

 // If expression-bodied property or field-like property without accessors -> create auto get; set;
 var accessors = SyntaxFactory.AccessorList(
 SyntaxFactory.List(new[] {
 SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
 SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
 }));
 return node.WithAccessorList(accessors).WithExpressionBody(null).WithSemicolonToken(default).WithInitializer(null);
 }

 public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
 {
 if (node.AccessorList != null)
 {
 var newAccessors = SyntaxFactory.AccessorList(
 SyntaxFactory.List(
 node.AccessorList.Accessors.Select(a => a
 .WithBody(null)
 .WithExpressionBody(null)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
 )));
 return node.WithAccessorList(newAccessors).WithExpressionBody(null).WithSemicolonToken(default);
 }
 // no accessor list -> add get; set;
 var accessors = SyntaxFactory.AccessorList(
 SyntaxFactory.List(new[] {
 SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
 SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
 }));
 return node.WithAccessorList(accessors).WithExpressionBody(null).WithSemicolonToken(default);
 }

 public override SyntaxNode? VisitEventDeclaration(EventDeclarationSyntax node)
 {
 if (node.AccessorList != null)
 {
 var newAccessors = SyntaxFactory.AccessorList(
 SyntaxFactory.List(
 node.AccessorList.Accessors.Select(a => a
 .WithBody(null)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
 )));
 return node.WithAccessorList(newAccessors);
 }
 return node;
 }

 public override SyntaxNode? VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
 {
 return node; // already a field-style event
 }

 public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
 {
 return node
 .WithBody(null)
 .WithExpressionBody(null)
 .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
 }

 public static string StripToSignatures(string source)
 {
 var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
 var root = tree.GetRoot();
 var rewritten = (CompilationUnitSyntax)new SignatureOnlyRewriter().Visit(root)!;
 return rewritten.NormalizeWhitespace().ToFullString();
 }
 }
}
