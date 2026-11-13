using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ManualHarmony;

[Generator]
public sealed class HarmonyPatchGenerator : IIncrementalGenerator
{
  public void Initialize(IncrementalGeneratorInitializationContext context)
  {
    var candidateTypes = context.SyntaxProvider
      .CreateSyntaxProvider(
        static (node, _) => node is ClassDeclarationSyntax,
        static (ctx, _) => GetCandidateType(ctx))
      .Where(static symbol => symbol is not null)
      .Select(static (symbol, _) => symbol!)
      .Collect();

    var source = context.CompilationProvider
      .Combine(candidateTypes)
      .Combine(context.AnalyzerConfigOptionsProvider);

    context.RegisterSourceOutput(source, static (spc, source) =>
    {
      var types = source.Left.Right;
      var optionsProvider = source.Right;

      var sourceText = HarmonySourceBuilder.Build(types, optionsProvider);

      spc.AddSource(
        "ManualHarmonyPatches.g.cs",
        SourceText.From(sourceText, System.Text.Encoding.UTF8));
    });
  }

  private static INamedTypeSymbol? GetCandidateType(GeneratorSyntaxContext context)
  {
    var classDeclaration = (ClassDeclarationSyntax)context.Node;

    if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol typeSymbol)
      return null;

    return !HasAttribute(typeSymbol, HarmonySymbolNames.HarmonyPatch) ? null : typeSymbol;
  }

  private static bool HasAttribute(INamedTypeSymbol typeSymbol, string fullName)
  {
    foreach (var attribute in typeSymbol.GetAttributes())
    {
      if (attribute.AttributeClass is null)
        continue;

      if (attribute.AttributeClass.ToDisplayString() == fullName)
        return true;
    }

    return false;
  }
}