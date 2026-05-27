using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Backend.Services;

public class AstExtractorService
{
    public AstExtractorService(IConfiguration configuration)
    {
        // future config usage (e.g., depth, filters)
    }

    public IEnumerable<AstDocument> ExtractFromCode(string code, string path = "")
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var docs = new List<AstDocument>();

        // Extract methods as documents
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var m in methods)
        {
            docs.Add(new AstDocument
            {
                Id = System.Guid.NewGuid().ToString(),
                Path = path,
                NodeKind = "Method",
                Name = m.Identifier.Text,
                Text = m.ToFullString()
            });
        }

        // Fallback: index the whole root
        if (docs.Count == 0)
        {
            docs.Add(new AstDocument
            {
                Id = System.Guid.NewGuid().ToString(),
                Path = path,
                NodeKind = root.Kind().ToString(),
                Name = string.Empty,
                Text = root.ToFullString()
            });
        }

        return docs;
    }

    public sealed class AstDocument
    {
        public string Id { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string NodeKind { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
    }
}
