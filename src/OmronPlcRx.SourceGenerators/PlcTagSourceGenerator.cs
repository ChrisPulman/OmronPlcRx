// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OmronPlcRx.SourceGenerators;

/// <summary>
/// Generates bindable PLC tag properties and observables from <c>PlcTagAttribute</c> fields.
/// </summary>
[Generator]
public sealed class PlcTagSourceGenerator : ISourceGenerator
{
    private const string AttributeMetadataName = "OmronPlcRx.PlcTagAttribute";
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    private static readonly DiagnosticDescriptor PartialTypeRule = new(
        "OPRX001",
        "PLC tag containing type must be partial",
        "Type '{0}' must be partial to receive generated PLC reactive stream members",
        "OmronPlcRx.SourceGeneration",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedTypeRule = new(
        "OPRX002",
        "PLC tag type is not supported",
        "Field '{0}' uses type '{1}', which is not supported by OmronPlcRx tag conversion",
        "OmronPlcRx.SourceGeneration",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor EmptyAddressRule = new(
        "OPRX003",
        "PLC tag address is empty",
        "Field '{0}' must specify a non-empty PLC address",
        "OmronPlcRx.SourceGeneration",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor PropertyCollisionRule = new(
        "OPRX004",
        "Generated PLC property collides with an existing member",
        "Field '{0}' would generate member '{1}', but that name already exists on '{2}'",
        "OmronPlcRx.SourceGeneration",
        DiagnosticSeverity.Error,
        true);

    /// <inheritdoc />
    public void Initialize(GeneratorInitializationContext context) =>
        context.RegisterForSyntaxNotifications(static () => new SyntaxReceiver());

    /// <inheritdoc />
    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver)
        {
            return;
        }

        var attributeSymbol = context.Compilation.GetTypeByMetadataName(AttributeMetadataName);
        if (attributeSymbol == null)
        {
            return;
        }

        var targets = new Dictionary<INamedTypeSymbol, List<TagField>>(SymbolEqualityComparer.Default);
        foreach (var fieldDeclaration in receiver.CandidateFields)
        {
            var semanticModel = context.Compilation.GetSemanticModel(fieldDeclaration.SyntaxTree);
            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                if (semanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol fieldSymbol)
                {
                    continue;
                }

                var attributeData = fieldSymbol.GetAttributes()
                    .FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol));
                if (attributeData == null)
                {
                    continue;
                }

                var containingType = fieldSymbol.ContainingType;
                var location = variable.GetLocation();
                if (!IsPartial(containingType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(PartialTypeRule, location, containingType.Name));
                    continue;
                }

                var address = GetAddress(attributeData);
                if (string.IsNullOrWhiteSpace(address))
                {
                    context.ReportDiagnostic(Diagnostic.Create(EmptyAddressRule, location, fieldSymbol.Name));
                    continue;
                }

                var tagType = GetObservableTagType(fieldSymbol.Type);
                if (!IsSupportedTagType(tagType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedTypeRule,
                        location,
                        fieldSymbol.Name,
                        tagType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                    continue;
                }

                var propertyName = ToPropertyName(fieldSymbol.Name);
                if (!SyntaxFacts.IsValidIdentifier(propertyName) || HasMemberCollision(containingType, fieldSymbol, propertyName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(PropertyCollisionRule, location, fieldSymbol.Name, propertyName, containingType.Name));
                    continue;
                }

                var tagName = GetNamedString(attributeData, "TagName") ?? propertyName;
                var register = GetNamedBoolean(attributeData, "Register", true);
                var observe = GetNamedBoolean(attributeData, "Observe", true);
                var writable = GetNamedBoolean(attributeData, "Writable", false);
                var field = new TagField(
                    fieldSymbol.Name,
                    propertyName,
                    ToBackingSubjectName(propertyName),
                    address!,
                    tagName,
                    fieldSymbol.Type.ToDisplayString(FullyQualifiedFormat),
                    tagType.ToDisplayString(FullyQualifiedFormat),
                    tagType.IsValueType,
                    register,
                    observe,
                    writable);

                if (!targets.TryGetValue(containingType, out var fields))
                {
                    fields = [];
                    targets.Add(containingType, fields);
                }

                fields.Add(field);
            }
        }

        foreach (var target in targets)
        {
            var source = GenerateSource(target.Key, target.Value);
            context.AddSource($"{GetHintName(target.Key)}.PlcReactiveStreams.g.cs", source);
        }
    }

    private static string GenerateSource(INamedTypeSymbol containingType, IReadOnlyList<TagField> fields)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("#pragma warning disable CS8601, CS8603");
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using System.Reactive.Linq;");
        builder.AppendLine("using System.Reactive.Subjects;");
        builder.AppendLine("#if NET8_0_OR_GREATER");
        builder.AppendLine("using ReactiveUI.Extensions.Async;");
        builder.AppendLine("#endif");
        builder.AppendLine();

        var namespaceName = containingType.ContainingNamespace.IsGlobalNamespace
            ? null
            : containingType.ContainingNamespace.ToDisplayString();

        if (namespaceName != null)
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine();
            builder.AppendLine("{");
        }

        var indent = namespaceName == null ? string.Empty : "    ";
        builder.Append(indent).Append(GetAccessibility(containingType)).Append(" partial class ").Append(containingType.Name).AppendLine();
        builder.Append(indent).AppendLine("{");

        foreach (var field in fields)
        {
            AppendProperty(builder, indent + "    ", field);
        }

        AppendRegisterMethod(builder, indent + "    ", fields);
        AppendBindMethod(builder, indent + "    ", fields);

        foreach (var field in fields.Where(static field => field.Writable))
        {
            AppendWriteMethod(builder, indent + "    ", field);
        }

        builder.Append(indent).AppendLine("}");
        if (namespaceName != null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static void AppendProperty(StringBuilder builder, string indent, TagField field)
    {
        builder.Append(indent).Append("private readonly global::System.Reactive.Subjects.BehaviorSubject<")
            .Append(field.PropertyType).Append("> ").Append(field.SubjectName).Append(" = new(default!);").AppendLine();
        builder.AppendLine();
        builder.Append(indent).Append("public ").Append(field.PropertyType).Append(' ').Append(field.PropertyName).AppendLine();
        builder.Append(indent).AppendLine("{");
        builder.Append(indent).Append("    get => ").Append(field.FieldName).AppendLine(";");
        builder.Append(indent).AppendLine("    private set");
        builder.Append(indent).AppendLine("    {");
        builder.Append(indent).Append("        if (!global::System.Collections.Generic.EqualityComparer<").Append(field.PropertyType).Append(">.Default.Equals(")
            .Append(field.FieldName).AppendLine(", value))");
        builder.Append(indent).AppendLine("        {");
        builder.Append(indent).Append("            ").Append(field.FieldName).AppendLine(" = value;");
        builder.Append(indent).Append("            ").Append(field.SubjectName).AppendLine(".OnNext(value);");
        builder.Append(indent).Append("            On").Append(field.PropertyName).AppendLine("Received(value);");
        builder.Append(indent).AppendLine("        }");
        builder.Append(indent).AppendLine("    }");
        builder.Append(indent).AppendLine("}");
        builder.AppendLine();
        builder.Append(indent).Append("public global::System.IObservable<").Append(field.PropertyType).Append("> ")
            .Append(field.PropertyName).AppendLine("Observable => ").Append(field.SubjectName).AppendLine(".AsObservable();");
        builder.AppendLine();
        builder.AppendLine("#if NET8_0_OR_GREATER");
        builder.Append(indent).Append("public global::ReactiveUI.Extensions.Async.IObservableAsync<").Append(field.PropertyType).Append("> ")
            .Append(field.PropertyName).AppendLine("ObservableAsync => ").Append(field.PropertyName).AppendLine("Observable.ToObservableAsync();");
        builder.AppendLine("#endif");
        builder.AppendLine();
        builder.Append(indent).Append("partial void On").Append(field.PropertyName).Append("Received(").Append(field.PropertyType).AppendLine(" value);");
        builder.AppendLine();
    }

    private static void AppendRegisterMethod(StringBuilder builder, string indent, IEnumerable<TagField> fields)
    {
        builder.Append(indent).AppendLine("public void RegisterPlcTags(global::OmronPlcRx.IOmronPlcRx plc)");
        builder.Append(indent).AppendLine("{");
        builder.Append(indent).AppendLine("    if (plc == null)");
        builder.Append(indent).AppendLine("    {");
        builder.Append(indent).AppendLine("        throw new global::System.ArgumentNullException(nameof(plc));");
        builder.Append(indent).AppendLine("    }");
        builder.AppendLine();

        foreach (var field in fields.Where(static field => field.Register))
        {
            builder.Append(indent).Append("    plc.AddUpdateTagItem<").Append(field.TagType).Append(">(\"")
                .Append(EscapeString(field.Address)).Append("\", \"").Append(EscapeString(field.TagName)).AppendLine("\");");
        }

        builder.Append(indent).AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendBindMethod(StringBuilder builder, string indent, IEnumerable<TagField> fields)
    {
        builder.Append(indent).AppendLine("public global::System.IDisposable BindPlcTags(global::OmronPlcRx.IOmronPlcRx plc)");
        builder.Append(indent).AppendLine("{");
        builder.Append(indent).AppendLine("    if (plc == null)");
        builder.Append(indent).AppendLine("    {");
        builder.Append(indent).AppendLine("        throw new global::System.ArgumentNullException(nameof(plc));");
        builder.Append(indent).AppendLine("    }");
        builder.AppendLine();
        builder.Append(indent).AppendLine("    RegisterPlcTags(plc);");
        builder.Append(indent).AppendLine("    var disposables = new global::System.Reactive.Disposables.CompositeDisposable();");

        foreach (var field in fields.Where(static field => field.Observe))
        {
            builder.Append(indent).Append("    disposables.Add(plc.Observe<").Append(field.TagType).Append(">(\"")
                .Append(EscapeString(field.TagName)).AppendLine("\").Subscribe(value =>");
            builder.Append(indent).AppendLine("    {");
            if (field.TagTypeIsValueType)
            {
                builder.Append(indent).Append("        ").Append(field.PropertyName).AppendLine(" = value;");
            }
            else
            {
                builder.Append(indent).Append("        ").Append(field.PropertyName).AppendLine(" = value;");
            }

            builder.Append(indent).AppendLine("    }));");
        }

        builder.AppendLine();
        builder.Append(indent).AppendLine("    return disposables;");
        builder.Append(indent).AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendWriteMethod(StringBuilder builder, string indent, TagField field)
    {
        builder.Append(indent).Append("public void Write").Append(field.PropertyName).Append("(global::OmronPlcRx.IOmronPlcRx plc, ")
            .Append(field.PropertyType).AppendLine(" value)");
        builder.Append(indent).AppendLine("{");
        builder.Append(indent).AppendLine("    if (plc == null)");
        builder.Append(indent).AppendLine("    {");
        builder.Append(indent).AppendLine("        throw new global::System.ArgumentNullException(nameof(plc));");
        builder.Append(indent).AppendLine("    }");
        builder.AppendLine();
        builder.Append(indent).Append("    plc.Value<").Append(field.TagType).Append(">(\"").Append(EscapeString(field.TagName)).AppendLine("\", value);");
        builder.Append(indent).AppendLine("}");
        builder.AppendLine();
    }

    private static bool IsPartial(INamedTypeSymbol typeSymbol)
    {
        foreach (var reference in typeSymbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is TypeDeclarationSyntax declaration &&
                declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private static ITypeSymbol GetObservableTagType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            namedType.TypeArguments.Length == 1)
        {
            return namedType.TypeArguments[0];
        }

        return typeSymbol;
    }

    private static bool IsSupportedTagType(ITypeSymbol typeSymbol)
    {
        return typeSymbol.SpecialType is
            SpecialType.System_Boolean or
            SpecialType.System_Byte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_String ||
            typeSymbol.ToDisplayString(FullyQualifiedFormat) is
                "global::OmronPlcRx.Core.Types.Bcd16" or
                "global::OmronPlcRx.Core.Types.BcdU16" or
                "global::OmronPlcRx.Core.Types.Bcd32" or
                "global::OmronPlcRx.Core.Types.BcdU32";
    }

    private static bool HasMemberCollision(INamedTypeSymbol containingType, IFieldSymbol fieldSymbol, string propertyName)
    {
        foreach (var member in containingType.GetMembers(propertyName))
        {
            if (!SymbolEqualityComparer.Default.Equals(member, fieldSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetAddress(AttributeData attributeData)
    {
        if (attributeData.ConstructorArguments.Length == 0)
        {
            return string.Empty;
        }

        return attributeData.ConstructorArguments[0].Value as string ?? string.Empty;
    }

    private static string? GetNamedString(AttributeData attributeData, string name)
    {
        foreach (var argument in attributeData.NamedArguments)
        {
            if (argument.Key == name)
            {
                return argument.Value.Value as string;
            }
        }

        return null;
    }

    private static bool GetNamedBoolean(AttributeData attributeData, string name, bool defaultValue)
    {
        foreach (var argument in attributeData.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Value is bool value)
            {
                return value;
            }
        }

        return defaultValue;
    }

    private static string ToPropertyName(string fieldName)
    {
        var trimmed = fieldName.TrimStart('_');
        if (trimmed.Length == 0)
        {
            return fieldName;
        }

        return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
    }

    private static string ToBackingSubjectName(string propertyName) =>
        "_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1) + "Subject";

    private static string GetAccessibility(INamedTypeSymbol symbol) =>
        symbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal",
        };

    private static string GetHintName(INamedTypeSymbol symbol)
    {
        var name = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty)
            .Replace('.', '_')
            .Replace('+', '_');
        return name;
    }

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class SyntaxReceiver : ISyntaxReceiver
    {
        public List<FieldDeclarationSyntax> CandidateFields { get; } = [];

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is FieldDeclarationSyntax fieldDeclaration &&
                fieldDeclaration.AttributeLists.Count > 0)
            {
                CandidateFields.Add(fieldDeclaration);
            }
        }
    }

    private sealed class TagField(
        string fieldName,
        string propertyName,
        string subjectName,
        string address,
        string tagName,
        string propertyType,
        string tagType,
        bool tagTypeIsValueType,
        bool register,
        bool observe,
        bool writable)
    {
        public string FieldName { get; } = fieldName;

        public string PropertyName { get; } = propertyName;

        public string SubjectName { get; } = subjectName;

        public string Address { get; } = address;

        public string TagName { get; } = tagName;

        public string PropertyType { get; } = propertyType;

        public string TagType { get; } = tagType;

        public bool TagTypeIsValueType { get; } = tagTypeIsValueType;

        public bool Register { get; } = register;

        public bool Observe { get; } = observe;

        public bool Writable { get; } = writable;
    }
}
