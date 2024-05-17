﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace MultiKey.SourceGenerator;

[Generator]
public class MultiKeyDictionaryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register a syntax receiver that will be created for each generation pass
        context.RegisterPostInitializationOutput(RegisterAttributeSource);

        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(IsSyntaxTargetForGeneration, TransformSyntaxToClass)
            .Where(static m => m is not null);

        
        //Debugger.Launch();
        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses, (spc, source) =>
        {
            GenerateAndAddSource(spc, source.Left, source.Right!);
        });
    }

    private void RegisterAttributeSource(IncrementalGeneratorPostInitializationContext context)
    {
        var attributeSource = @"
// <auto-generated>
// This code was generated by a tool.
//
// Changes to this file may cause incorrect behavior and will be lost if
// the code is regenerated.
// </auto-generated>

using System;

namespace MultiKey
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class MultiKeyDictionaryAttribute<TPrimary, TSecondary, TItem> : Attribute
    {
        public MultiKeyDictionaryAttribute(string primarySelector, string secondarySelector)
        {
            PrimarySelector = primarySelector;
            SecondarySelector = secondarySelector;
        }

        public string PrimarySelector { get; }
        public string SecondarySelector { get; }
    }
}
";
        context.AddSource("MultiKeyDictionaryAttribute.g.cs", attributeSource);
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node, CancellationToken cancellationToken)
    {
        return node is RecordDeclarationSyntax recordDeclaration && recordDeclaration.AttributeLists.Count > 0;
    }

    private static RecordDeclarationSyntax? TransformSyntaxToClass(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var recordDeclaration = (RecordDeclarationSyntax)context.Node;

        foreach (var attributeList in recordDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (ModelExtensions.GetSymbolInfo(context.SemanticModel, attribute).Symbol is not IMethodSymbol attributeSymbol)
                    continue;
                
                if (attributeSymbol.ContainingType.ToDisplayString().StartsWith("MultiKey.MultiKeyDictionaryAttribute<"))
                {
                    return recordDeclaration;
                }
            }
        }

        return null;
    }

    private void GenerateAndAddSource(SourceProductionContext context, Compilation compilation, ImmutableArray<RecordDeclarationSyntax> recordDeclarations)
    {
        foreach (var recordDeclaration in recordDeclarations)
        {
            var semanticModel = compilation.GetSemanticModel(recordDeclaration.SyntaxTree);
        
            // Get the symbol representing the class
            var classSymbol = ModelExtensions.GetDeclaredSymbol(semanticModel, recordDeclaration) as INamedTypeSymbol;

            if (classSymbol is null)
                continue;

            // Extract attribute information
            var multiKeyAttribute = classSymbol.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass!.ToDisplayString().StartsWith("MultiKey.MultiKeyDictionaryAttribute<"));

            if (multiKeyAttribute == null) continue;

            var primaryType = multiKeyAttribute.AttributeClass!.TypeArguments[0];
            var secondaryType = multiKeyAttribute.AttributeClass.TypeArguments[1];
            var itemType = multiKeyAttribute.AttributeClass.TypeArguments[2];
            string primarySelector = (string)multiKeyAttribute.ConstructorArguments[0].Value!;
            string secondarySelector = (string)multiKeyAttribute.ConstructorArguments[1].Value!;

            // Collect namespaces and enclosing declarations
            var (outerNamespace, namespaces, enclosingDeclarations) = NamespaceCollector.CollectNamespacesAndEnclosingDeclarations(recordDeclaration, semanticModel);

            // Generate the source code
            var sourceCode = Template(classSymbol, primaryType, secondaryType, itemType, primarySelector, secondarySelector, namespaces, enclosingDeclarations, outerNamespace);

            var formattedSourceCode = FormatSourceCode(sourceCode);
            
            // Add the generated source to the compilation
            context.AddSource($"{classSymbol.Name}.g.cs", SourceText.From(formattedSourceCode, Encoding.UTF8));
        }
    }
    
    private string FormatSourceCode(string sourceCode)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        var workspace = new AdhocWorkspace();
        var options = workspace.Options;

        options = options.WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, "\n");
        options = options.WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, false);
        options = options.WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, 4);
        options = options.WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, 4);

        var formattedRoot = Formatter.Format(root, workspace, options);
        return formattedRoot.ToFullString();
    }

    private string Template(
        INamedTypeSymbol partialMultiKeyDictionary,
        ITypeSymbol primary,
        ITypeSymbol secondary,
        ITypeSymbol item,
        string primarySelector, 
        string secondarySelector, 
        HashSet<string> namespaces, 
        List<string> enclosingDeclarations, 
        string partialMultiKeyDictionaryNamespace)
    {
        string primaryType = primary.Name!;
        string secondaryType = secondary.Name!;
        string itemType = item.Name!;
        string dictName = partialMultiKeyDictionary.Name;

        // Combine with default namespaces and remove duplicates
        var defaultNamespaces = new HashSet<string> { 
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Collections.Immutable",
            "System.Text.Json",
            "SJson = System.Text.Json.Serialization"
        };
        namespaces.UnionWith(defaultNamespaces);

        var sortedNamespaces = namespaces.OrderBy(ns => ns).ToList();

        var namespaceLines = sortedNamespaces.Select(ns => $"using {ns};");

        var top = $$"""
            // <auto-generated>
            // This code was generated by a tool.
            //
            // Changes to this file may cause incorrect behavior and will be lost if
            // the code is regenerated.
            // </auto-generated>

            // This class serializes/deserializes to Json via System.Text.Json
            // To enable Newtonsoft.Json, define the following constant in your project: MULTIKEY_USE_NEWTONSOFT_JSON
            
            #nullable enable
            
            {{string.Join("\n", namespaceLines)}}
            #if MULTIKEY_USE_NEWTONSOFT_JSON
            using NJson = Newtonsoft.Json;
            #endif
            
            """;

        var @class = $$"""
            namespace {{partialMultiKeyDictionaryNamespace}}
            {
                /// <summary>
                /// Represents a specialized immutable dictionary of {{itemType}}.
                /// </summary>
                /// <remarks>
                /// The <see cref="{{dictName}}"/> class provides efficient lookup capabilities 
                /// for items using both primary and secondary keys. The primary key allows for direct access to individual items,
                /// while the secondary key allows for grouped access to items sharing the same secondary key.
                /// </remarks>
                /// <example>
                /// The following example demonstrates how to use the <see cref="{{dictName}}"/>:
                /// <code>
                /// {{dictName}} dictionary = new {{dictName}}();
                /// {{primaryType}} primaryKey = /* any {{primaryType}} value */ default;
                /// {{secondaryType}} secondaryKey = /* any {{secondaryType}} value */ default;
                /// {{itemType}} byPrimary = dictionary[primaryKey]; // Returns 1 item or throws exception
                /// IReadOnlyList<{{itemType}}> bySecondary = dictionary[secondaryKey]; // Returns 0+ items
                /// </code>
                /// </example>
            #if MULTIKEY_USE_NEWTONSOFT_JSON
            [NJson.JsonConverter(typeof({{dictName}}NewtonsoftConverter))]
            #endif
                [SJson.JsonConverter(typeof({{dictName}}SystemConverter))]
                public sealed partial record {{dictName}} : IReadOnlyDictionary<{{primaryType}}, {{itemType}}>
                {
                    private readonly ImmutableList<{{itemType}}?> _items;
            
                    private ImmutableDictionary<{{primaryType}}, int>? _primaryIndex; // Will be initialized lazily
            
                    private ImmutableDictionary<{{secondaryType}}, ImmutableHashSet<int>>? _secondaryIndex; // Will be initialized lazily
            
                    static {{dictName}}()
                    {
                        // These two assignments will be filled in by source generation, supplied by user
                        PrimarySelector = (Func<{{itemType}}, {{primaryType}}>)({{primarySelector}});
                        SecondarySelector = (Func<{{itemType}}, {{secondaryType}}>)({{secondarySelector}});
                    }
            
                    private static readonly Func<{{itemType}}, {{primaryType}}> PrimarySelector;
            
                    private static readonly Func<{{itemType}}, {{secondaryType}}> SecondarySelector;
            
                    private ImmutableDictionary<{{primaryType}}, int> PrimaryIndex =>
                        _primaryIndex ??= _items
                            .Select((item, index) => (item, index))
                            .Where(i => i.item is not null)
                            .ToImmutableDictionary(key => PrimarySelector(key.item!), value => value.index);
            
                    private ImmutableDictionary<{{secondaryType}}, ImmutableHashSet<int>> SecondaryIndex =>
                        _secondaryIndex ??= _items
                            .Select((item, index) => (item, index))
                            .Where(i => i.item != null)
                            .GroupBy(item => SecondarySelector(item.item!))
                            .ToImmutableDictionary(group => group.Key, group => group.Select(x => x.index).ToImmutableHashSet());
            
                    public {{dictName}}()
                    {
                        _items = new List<{{itemType}}?>().ToImmutableList();
                    }
            
                    public {{dictName}}(IEnumerable<{{itemType}}> items)
                    {
                        _items = items.ToImmutableList()!;
                    }
            
                    public {{itemType}} this[{{primaryType}} primary] => _items[PrimaryIndex[primary]]!;
            
                    public IEnumerable<{{primaryType}}> Keys => PrimaryIndex.Keys;
                    public IEnumerable<{{secondaryType}}> KeysSecondary => SecondaryIndex.Keys;
                    public IEnumerable<{{itemType}}> Values => _items.Where(i => i is not null)!;
            
                    public IReadOnlyList<{{itemType}}> this[{{secondaryType}} secondary] =>
                        SecondaryIndex.TryGetValue(secondary, out var result)
                            ? result.Select(i => _items[i]!).ToList().AsReadOnly()
                            : ((List<{{itemType}}>) []).AsReadOnly();
            
                    public bool ContainsKey({{primaryType}} key) =>
                        PrimaryIndex.ContainsKey(key);
            
                    public bool ContainsKeySecondary({{secondaryType}} key) =>
                        SecondaryIndex.ContainsKey(key);
            
                    public bool TryGetValue({{primaryType}} primaryKey, out {{itemType}} item)
                    {
                        if (PrimaryIndex.TryGetValue(primaryKey, out var index))
                        {
                            item = _items[index]!;
                            return true;
                        }
            
                        item = default!;
                        return false;
                    }
            
                    public bool TryGetValue({{secondaryType}} secondaryKey, out IReadOnlyList<{{itemType}}> items)
                    {
                        if (SecondaryIndex.TryGetValue(secondaryKey, out var indexes))
                        {
                            items = indexes.Select(i => _items[i]!).ToList().AsReadOnly();
                            return true;
                        }
            
                        items = default!;
                        return false;
                    }
            
                    public {{dictName}} Add({{itemType}} item)
                    {
                        var primaryKey = PrimarySelector(item);
            
                        if (PrimaryIndex.ContainsKey(primaryKey))
                            throw new ArgumentException("An item with the same primary key already exists in the dictionary.",
                                nameof(_primaryIndex));
            
                        return new(_items.Add(item)!);
                    }
            
                    public {{dictName}} Remove({{primaryType}} primary)
                    {
                        if (!PrimaryIndex.TryGetValue(primary, out var index))
                            return this;
            
                        return new(_items.Remove(_items[index])!);
                    }
            
                    public {{dictName}} Remove({{itemType}} item) =>
                        new(_items.Remove(item)!);
            
                    public {{dictName}} SetItem({{itemType}} item)
                    {
                        var primaryKey = PrimarySelector(item);
            
                        if (!PrimaryIndex.TryGetValue(primaryKey, out var index))
                        {
                            return Add(item);
                        }
            
                        return new(_items.SetItem(index, item)!);
                    }
            
                    public {{dictName}} Compact() =>
                        new(_items.Where(item => item is not null)!);
            
            #if MULTIKEY_USE_NEWTONSOFT_JSON
                public class {{dictName}}NewtonsoftConverter : NJson.JsonConverter
                {
                    public override bool CanConvert(Type objectType) => objectType == typeof({{dictName}});
            
                    public override void WriteJson(NJson.JsonWriter writer, object value, NJson.JsonSerializer serializer)
                    {
                        var dictionary = value as {{dictName}};
                        if (dictionary is null)
                        {
                            writer.WriteNull();
                            return;
                        }
            
                        serializer.Serialize(writer, dictionary.Values);
                    }
            
                    public override object ReadJson(NJson.JsonReader reader, Type objectType, object existingValue, NJson.JsonSerializer serializer)
                    {
                        if (reader.TokenType == NJson.JsonToken.Null)
                            return null;
            
                        var items = serializer.Deserialize<List<{{itemType}}>>(reader);
                        if (items is null)
                            return null;
            
                        return new {{dictName}}(items);
                    }
                }
            #endif
            
                    public class {{dictName}}SystemConverter : SJson.JsonConverter<{{dictName}}>
                    {
                        public override {{dictName}} Read(ref Utf8JsonReader reader, Type typeToConvert,
                            JsonSerializerOptions options)
                        {
                            if (reader.TokenType != JsonTokenType.StartArray)
                            {
                                throw new JsonException("Invalid {{dictName}} serialization");
                            }
            
                            List<{{itemType}}> primary = new();
            
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                var item = JsonSerializer.Deserialize<{{itemType}}>(ref reader, options);
                                primary.Add(item!);
                            }
            
                            return new {{dictName}}(primary);
                        }
            
                        public override void Write(Utf8JsonWriter writer, {{dictName}} value, JsonSerializerOptions options)
                        {
                            writer.WriteStartArray();
            
                            foreach (var item in value.Values)
                            {
                                JsonSerializer.Serialize(writer, item, options);
                            }
            
                            writer.WriteEndArray();
                        }
                    }
            
                    public int Count => PrimaryIndex.Count;
            
                    IEnumerator<KeyValuePair<{{primaryType}}, {{itemType}}>> IEnumerable<KeyValuePair<{{primaryType}}, {{itemType}}>>.GetEnumerator()
                    {
                        return PrimaryIndex
                            .Select(kvp => new KeyValuePair<{{primaryType}}, {{itemType}}>(kvp.Key, _items[kvp.Value]!))
                            .GetEnumerator();
                    }
            
                    public IEnumerator GetEnumerator()
                    {
                        return PrimaryIndex.GetEnumerator();
                    }
                }
                
                public static class {{dictName}}Extensions
                {
                    public static {{dictName}} To{{dictName}}(this IEnumerable<{{itemType}}> sequence)
                    {
                        return new(sequence);
                    }
                }
            """;

        var bottom = new StringBuilder();
        foreach (var decl in enclosingDeclarations)
        {
            bottom.AppendLine("}");
        }

        return top + @class + bottom + "\n #nullable restore";
    }

    // Helper class to find candidate classes for code generation
    private class SyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is ClassDeclarationSyntax { AttributeLists.Count: > 0 } classDeclarationSyntax)
            {
                CandidateClasses.Add(classDeclarationSyntax);
            }
        }
    }
}

public static class NamespaceCollector
{
    public static (string Namespace, HashSet<string> Namespaces, List<string> EnclosingDeclarations) CollectNamespacesAndEnclosingDeclarations(SyntaxNode node, SemanticModel semanticModel)
    {
        var namespaces = new HashSet<string>();
        var enclosingDeclarations = new List<string>();
        string outerNamespace = string.Empty;

        // Collect namespaces
        var symbols = node.DescendantNodes()
                          .OfType<IdentifierNameSyntax>()
                          .Select(identifier => ModelExtensions.GetSymbolInfo(semanticModel, identifier).Symbol)
                          .Where(symbol => symbol is not null)
                          .Distinct(SymbolEqualityComparer.Default);

        foreach (var symbol in symbols)
        {
            if (symbol!.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
            {
                namespaces.Add(symbol.ContainingNamespace.ToDisplayString());
            }
        }

        // Collect enclosing declarations and the namespace
        var current = node;
        while (current != null)
        {
            if (current is NamespaceDeclarationSyntax namespaceDeclaration)
            {
                if (string.IsNullOrEmpty(outerNamespace))
                {
                    outerNamespace = namespaceDeclaration.Name.ToString();
                }
            }
            else if (current is FileScopedNamespaceDeclarationSyntax fileScopedNamespaceDeclaration)
            {
                if (string.IsNullOrEmpty(outerNamespace))
                {
                    outerNamespace = fileScopedNamespaceDeclaration.Name.ToString();
                }
            }
            else if (current is ClassDeclarationSyntax classDeclaration)
            {
                var modifiers = string.Join(" ", classDeclaration.Modifiers.Select(m => m.Text));
                enclosingDeclarations.Add($"{modifiers} class {classDeclaration.Identifier}");
            }
            else if (current is StructDeclarationSyntax structDeclaration)
            {
                var modifiers = string.Join(" ", structDeclaration.Modifiers.Select(m => m.Text));
                enclosingDeclarations.Add($"{modifiers} struct {structDeclaration.Identifier}");
            }
            else if (current is RecordDeclarationSyntax recordDeclaration)
            {
                var modifiers = string.Join(" ", recordDeclaration.Modifiers.Select(m => m.Text));
                enclosingDeclarations.Add($"{modifiers} record {recordDeclaration.Identifier}");
            }

            current = current.Parent;
        }

        enclosingDeclarations.Reverse();
        return (outerNamespace, namespaces, enclosingDeclarations);
    }
}