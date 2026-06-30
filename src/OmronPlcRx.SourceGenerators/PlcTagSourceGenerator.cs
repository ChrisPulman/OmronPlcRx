// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OmronPlcRx.SourceGenerators;

/// <summary>
/// Generates bindable PLC tag properties from <c>PlcTagAttribute</c> fields.
/// The generated members expose ReactiveUI.Primitives signals and binding helpers
/// without depending on System.Reactive.
/// </summary>
[Generator]
public sealed class PlcTagSourceGenerator : ISourceGenerator
{
    /// <summary>Metadata names for PLC tag attributes consumed by this generator.</summary>
    private static readonly string[] AttributeMetadataNames =
    [
        "OmronPlcRx.PlcTagAttribute",
        "OmronPlcRx.Reactive.PlcTagAttribute",
    ];

    /// <summary>Format used when generated code needs a fully qualified type name.</summary>
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>Diagnostic reported when the containing type is not partial.</summary>
    private static readonly DiagnosticDescriptor PartialTypeRule = new(
        "OPRX001",
        "PLC tag containing type must be partial",
        "Type '{0}' must be partial to receive generated PLC reactive stream members",
        "OmronPlcRx.SourceGeneration",
        DiagnosticSeverity.Error,
        true);

    /// <summary>Diagnostic reported when a field type cannot be mapped to a PLC tag type.</summary>
    private static readonly DiagnosticDescriptor UnsupportedTypeRule = new(
        "OPRX002",
        "PLC tag type is not supported",
        "Field '{0}' uses type '{1}', which is not supported by OmronPlcRx tag conversion",
        "OmronPlcRx.SourceGeneration",
        DiagnosticSeverity.Error,
        true);

    /// <summary>Diagnostic reported when the PLC address argument is missing or empty.</summary>
    private static readonly DiagnosticDescriptor EmptyAddressRule = new(
        "OPRX003",
        "PLC tag address is empty",
        "Field '{0}' must specify a non-empty PLC address",
        "OmronPlcRx.SourceGeneration",
        DiagnosticSeverity.Error,
        true);

    /// <summary>Diagnostic reported when generated property names collide with existing members.</summary>
    private static readonly DiagnosticDescriptor PropertyCollisionRule = new(
        "OPRX004",
        "Generated PLC property collides with an existing member",
        "Field '{0}' would generate member '{1}', but that name already exists on '{2}'",
        "OmronPlcRx.SourceGeneration",
        DiagnosticSeverity.Error,
        true);

    /// <inheritdoc />
        /// <param name="context">The context value.</param>
    public void Initialize(GeneratorInitializationContext context) =>
        context.RegisterForSyntaxNotifications(static () => new SyntaxReceiver());

    /// <inheritdoc />
        /// <param name="context">The context value.</param>
    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver)
        {
            return;
        }

        var attributeSymbols = ResolvePlcTagAttributeSymbols(context.Compilation);
        if (attributeSymbols.Count == 0)
        {
            return;
        }

        var targets = new Dictionary<INamedTypeSymbol, List<TagField>>(SymbolEqualityComparer.Default);
        CollectTargets(context, receiver.CandidateFields, attributeSymbols, targets);
        AddGeneratedSources(context, targets);
    }

    /// <summary>Resolves PLC tag attribute symbols available in the current compilation.</summary>
    /// <param name="compilation">Current compilation.</param>
    /// <returns>The resolved PLC tag attribute symbols.</returns>
    private static List<INamedTypeSymbol> ResolvePlcTagAttributeSymbols(Compilation compilation)
    {
        var symbols = new List<INamedTypeSymbol>(AttributeMetadataNames.Length);
        foreach (var metadataName in AttributeMetadataNames)
        {
            var symbol = compilation.GetTypeByMetadataName(metadataName);
            if (symbol is not null)
            {
                symbols.Add(symbol);
            }
        }

        return symbols;
    }

    /// <summary>Collects all valid PLC tag fields grouped by containing type.</summary>
    /// <param name="context">Current generator execution context.</param>
    /// <param name="fieldDeclarations">Field declarations collected by the syntax receiver.</param>
    /// <param name="attributeSymbols">Resolved PLC tag attribute symbols.</param>
    /// <param name="targets">Target map to populate.</param>
    private static void CollectTargets(
        GeneratorExecutionContext context,
        IEnumerable<FieldDeclarationSyntax> fieldDeclarations,
        IReadOnlyCollection<INamedTypeSymbol> attributeSymbols,
        Dictionary<INamedTypeSymbol, List<TagField>> targets)
    {
        foreach (var fieldDeclaration in fieldDeclarations)
        {
            var semanticModel = context.Compilation.GetSemanticModel(fieldDeclaration.SyntaxTree);
            CollectFields(context, semanticModel, attributeSymbols, fieldDeclaration, targets);
        }
    }

    /// <summary>Collects valid PLC tags from one field declaration.</summary>
    /// <param name="context">Current generator execution context.</param>
    /// <param name="semanticModel">Semantic model for the field declaration.</param>
    /// <param name="attributeSymbols">Resolved PLC tag attribute symbols.</param>
    /// <param name="fieldDeclaration">Field declaration to inspect.</param>
    /// <param name="targets">Target map to populate.</param>
    private static void CollectFields(
        GeneratorExecutionContext context,
        SemanticModel semanticModel,
        IReadOnlyCollection<INamedTypeSymbol> attributeSymbols,
        FieldDeclarationSyntax fieldDeclaration,
        Dictionary<INamedTypeSymbol, List<TagField>> targets)
    {
        foreach (var variable in fieldDeclaration.Declaration.Variables)
        {
            if (TryCreateTagField(
                semanticModel,
                attributeSymbols,
                variable,
                out var containingType,
                out var tagField,
                out var diagnostic))
            {
                AddTarget(targets, containingType!, tagField!);
                continue;
            }

            if (diagnostic is not null)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>Adds generated source for every target type.</summary>
    /// <param name="context">Current generator execution context.</param>
    /// <param name="targets">Collected targets grouped by containing type.</param>
    private static void AddGeneratedSources(
        GeneratorExecutionContext context,
        Dictionary<INamedTypeSymbol, List<TagField>> targets)
    {
        foreach (var target in targets)
        {
            context.AddSource(
                $"{GetHintName(target.Key)}.PlcReactiveStreams.g.cs",
                GenerateSource(target.Key, target.Value));
        }
    }

    /// <summary>Creates a tag field model for one attributed variable.</summary>
    /// <param name="semanticModel">Semantic model used to resolve the field symbol.</param>
    /// <param name="attributeSymbols">Resolved PLC tag attribute symbols.</param>
    /// <param name="variable">Variable declarator to inspect.</param>
    /// <param name="containingType">Containing type when a tag field is created.</param>
    /// <param name="tagField">Created tag field model.</param>
    /// <param name="diagnostic">Validation diagnostic when the field is invalid.</param>
    /// <returns>True when a valid tag field was created; otherwise false.</returns>
    private static bool TryCreateTagField(
        SemanticModel semanticModel,
        IReadOnlyCollection<INamedTypeSymbol> attributeSymbols,
        VariableDeclaratorSyntax variable,
        out INamedTypeSymbol? containingType,
        out TagField? tagField,
        out Diagnostic? diagnostic)
    {
        containingType = null;
        tagField = null;
        diagnostic = null;

        if (semanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol fieldSymbol)
        {
            return false;
        }

        var attributeData = GetPlcTagAttribute(fieldSymbol, attributeSymbols);
        if (attributeData is null)
        {
            return false;
        }

        containingType = fieldSymbol.ContainingType;
        var location = variable.GetLocation();
        diagnostic = ValidateTagField(containingType, fieldSymbol, attributeData, location, out var address, out var tagType, out var propertyName);
        if (diagnostic is not null)
        {
            return false;
        }

        tagField = CreateTagField(fieldSymbol, attributeData, address, tagType, propertyName);
        return true;
    }

    /// <summary>Finds the PLC tag attribute instance on a field symbol.</summary>
    /// <param name="fieldSymbol">Field symbol to inspect.</param>
    /// <param name="attributeSymbols">Resolved PLC tag attribute symbols.</param>
    /// <returns>The matching attribute data when present; otherwise null.</returns>
    private static AttributeData? GetPlcTagAttribute(IFieldSymbol fieldSymbol, IReadOnlyCollection<INamedTypeSymbol> attributeSymbols)
    {
        foreach (var attribute in fieldSymbol.GetAttributes())
        {
            foreach (var attributeSymbol in attributeSymbols)
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
                {
                    return attribute;
                }
            }
        }

        return null;
    }

    /// <summary>Validates an attributed field and extracts generated naming metadata.</summary>
    /// <param name="containingType">Type containing the field.</param>
    /// <param name="fieldSymbol">Attributed field symbol.</param>
    /// <param name="attributeData">PLC tag attribute data.</param>
    /// <param name="location">Source location used for diagnostics.</param>
    /// <param name="address">Extracted PLC address.</param>
    /// <param name="tagType">Resolved non-nullable tag type.</param>
    /// <param name="propertyName">Generated property name.</param>
    /// <returns>A diagnostic when validation fails; otherwise null.</returns>
    private static Diagnostic? ValidateTagField(
        INamedTypeSymbol containingType,
        IFieldSymbol fieldSymbol,
        AttributeData attributeData,
        Location location,
        out string address,
        out ITypeSymbol tagType,
        out string propertyName)
    {
        address = GetAddress(attributeData);
        tagType = GetObservableTagType(fieldSymbol.Type);
        propertyName = ToPropertyName(fieldSymbol.Name);

        if (!IsPartial(containingType))
        {
            return Diagnostic.Create(PartialTypeRule, location, containingType.Name);
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return Diagnostic.Create(EmptyAddressRule, location, fieldSymbol.Name);
        }

        return IsSupportedTagType(tagType)
            ? CreateCollisionDiagnostic(containingType, fieldSymbol, propertyName, location)
            : Diagnostic.Create(
                UnsupportedTypeRule,
                location,
                fieldSymbol.Name,
                tagType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
    }

    /// <summary>Creates a property collision diagnostic when generated members would conflict.</summary>
    /// <param name="containingType">Type containing the attributed field.</param>
    /// <param name="fieldSymbol">Attributed field symbol.</param>
    /// <param name="propertyName">Generated property name.</param>
    /// <param name="location">Source location used for diagnostics.</param>
    /// <returns>A diagnostic when the name is invalid or collides; otherwise null.</returns>
    private static Diagnostic? CreateCollisionDiagnostic(
        INamedTypeSymbol containingType,
        IFieldSymbol fieldSymbol,
        string propertyName,
        Location location)
    {
        return SyntaxFacts.IsValidIdentifier(propertyName) && !HasMemberCollision(containingType, fieldSymbol, propertyName)
            ? null
            : Diagnostic.Create(PropertyCollisionRule, location, fieldSymbol.Name, propertyName, containingType.Name);
    }

    /// <summary>Creates the immutable tag field model used during source emission.</summary>
    /// <param name="fieldSymbol">Attributed field symbol.</param>
    /// <param name="attributeData">PLC tag attribute data.</param>
    /// <param name="address">Validated PLC address.</param>
    /// <param name="tagType">Resolved non-nullable tag type.</param>
    /// <param name="propertyName">Generated property name.</param>
    /// <returns>The generated tag field model.</returns>
    private static TagField CreateTagField(
        IFieldSymbol fieldSymbol,
        AttributeData attributeData,
        string address,
        ITypeSymbol tagType,
        string propertyName)
    {
        return new TagField(
            fieldSymbol.Name,
            propertyName,
            ToBackingSubjectName(propertyName),
            address,
            GetNamedString(attributeData, "TagName") ?? propertyName,
            fieldSymbol.Type.ToDisplayString(FullyQualifiedFormat),
            tagType.ToDisplayString(FullyQualifiedFormat),
            GetNamedBoolean(attributeData, "Register", true),
            GetNamedBoolean(attributeData, "Observe", true),
            GetNamedBoolean(attributeData, "Writable", false));
    }

    /// <summary>Adds one tag field to the target map.</summary>
    /// <param name="targets">Target map grouped by containing type.</param>
    /// <param name="containingType">Type receiving generated members.</param>
    /// <param name="field">Tag field to add.</param>
    private static void AddTarget(Dictionary<INamedTypeSymbol, List<TagField>> targets, INamedTypeSymbol containingType, TagField field)
    {
        if (!targets.TryGetValue(containingType, out var fields))
        {
            fields = [];
            targets.Add(containingType, fields);
        }

        fields.Add(field);
    }

    /// <summary>Generates the source text for one target type.</summary>
    /// <param name="containingType">Type receiving generated PLC members.</param>
    /// <param name="fields">Collected tag fields for the type.</param>
    /// <returns>Generated C# source.</returns>
    private static string GenerateSource(INamedTypeSymbol containingType, IReadOnlyList<TagField> fields)
    {
        var builder = new StringBuilder();
        AppendHeader(builder);

        var namespaceName = containingType.ContainingNamespace.IsGlobalNamespace
            ? null
            : containingType.ContainingNamespace.ToDisplayString();

        if (namespaceName is not null)
        {
            Append(builder, "namespace ");
            AppendLine(builder, namespaceName);
            AppendLine(builder, "{");
        }

        var indent = namespaceName is null ? string.Empty : "    ";
        var plcFacadeType = GetPlcFacadeType(containingType);
        var observableAsyncBridgeType = GetObservableAsyncBridgeType(containingType);
        AppendTypeStart(builder, indent, containingType);
        AppendProperties(builder, indent, fields, observableAsyncBridgeType);
        AppendRegisterMethod(builder, indent + "    ", fields, plcFacadeType);
        AppendBindMethod(builder, indent + "    ", fields, plcFacadeType);
        AppendWriteMethods(builder, indent, fields, plcFacadeType);
        AppendLine(builder, indent + "}");

        if (namespaceName is not null)
        {
            AppendLine(builder, "}");
        }

        return builder.ToString();
    }

    /// <summary>Gets the generated PLC facade type for the target namespace.</summary>
    /// <param name="containingType">Type receiving generated PLC members.</param>
    /// <returns>The fully qualified PLC facade type name.</returns>
    private static string GetPlcFacadeType(INamedTypeSymbol containingType) =>
        IsReactiveNamespace(containingType)
            ? "global::OmronPlcRx.Reactive.IOmronPlcRx"
            : "global::OmronPlcRx.IOmronPlcRx";

    /// <summary>Gets the observable-to-async bridge type for the target namespace.</summary>
    /// <param name="containingType">Type receiving generated PLC members.</param>
    /// <returns>The fully qualified bridge type name.</returns>
    private static string GetObservableAsyncBridgeType(INamedTypeSymbol containingType) =>
        IsReactiveNamespace(containingType)
            ? "global::CP.IO.Ports.Reactive.ObservableAsyncBridgeExtensions"
            : "global::CP.IO.Ports.ObservableAsyncBridgeExtensions";

    /// <summary>Determines whether generated code targets the reactive package namespace.</summary>
    /// <param name="containingType">Type receiving generated PLC members.</param>
    /// <returns>True when the containing type lives under <c>OmronPlcRx.Reactive</c>.</returns>
    private static bool IsReactiveNamespace(INamedTypeSymbol containingType)
    {
        if (containingType.ContainingNamespace.IsGlobalNamespace)
        {
            return false;
        }

        var namespaceName = containingType.ContainingNamespace.ToDisplayString();
        return namespaceName == "OmronPlcRx.Reactive" ||
            namespaceName.StartsWith("OmronPlcRx.Reactive.", StringComparison.Ordinal);
    }

    /// <summary>Appends generated source header and using directives.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    private static void AppendHeader(StringBuilder builder)
    {
        AppendLine(builder, "// <auto-generated />");
        AppendLine(builder, "#nullable enable");
        AppendLine(builder, "#pragma warning disable CS8601, CS8603");
        AppendLine(builder, "using System;");
        AppendLine(builder, "using System.Collections.Generic;");
        AppendLine(builder, "using ReactiveUI.Primitives;");
        AppendLine(builder, "#if NET8_0_OR_GREATER");
        AppendLine(builder, "using ReactiveUI.Primitives.Async;");
        AppendLine(builder, "#endif");
        AppendLine(builder);
    }

    /// <summary>Appends the target partial type declaration.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Current indentation.</param>
    /// <param name="containingType">Type receiving generated PLC members.</param>
    private static void AppendTypeStart(StringBuilder builder, string indent, INamedTypeSymbol containingType)
    {
        Append(builder, indent);
        Append(builder, GetAccessibility(containingType));
        Append(builder, " partial class ");
        AppendLine(builder, containingType.Name);
        AppendLine(builder, indent + "{");
    }

    /// <summary>Appends all generated properties.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Target type indentation.</param>
    /// <param name="fields">Collected tag fields.</param>
    /// <param name="observableAsyncBridgeType">Fully qualified observable-to-async bridge type.</param>
    private static void AppendProperties(StringBuilder builder, string indent, IReadOnlyList<TagField> fields, string observableAsyncBridgeType)
    {
        foreach (var field in fields)
        {
            AppendProperty(builder, indent + "    ", field, observableAsyncBridgeType);
        }
    }

    /// <summary>Appends write helpers for writable fields.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Target type indentation.</param>
    /// <param name="fields">Collected tag fields.</param>
    /// <param name="plcFacadeType">Fully qualified PLC facade type.</param>
    private static void AppendWriteMethods(StringBuilder builder, string indent, IReadOnlyList<TagField> fields, string plcFacadeType)
    {
        foreach (var field in fields)
        {
            if (field.Writable)
            {
                AppendWriteMethod(builder, indent + "    ", field, plcFacadeType);
            }
        }
    }

    /// <summary>Appends a generated PLC property and its observable streams.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Current indentation.</param>
    /// <param name="field">Tag field to emit.</param>
    /// <param name="observableAsyncBridgeType">Fully qualified observable-to-async bridge type.</param>
    private static void AppendProperty(StringBuilder builder, string indent, TagField field, string observableAsyncBridgeType)
    {
        Append(builder, indent);
        Append(builder, "private readonly global::ReactiveUI.Primitives.Signals.BehaviorSignal<");
        Append(builder, field.PropertyType);
        Append(builder, "> ");
        Append(builder, field.SubjectName);
        AppendLine(builder, " = new(default!);");
        AppendLine(builder);

        Append(builder, indent);
        Append(builder, "public ");
        Append(builder, field.PropertyType);
        Append(builder, " ");
        AppendLine(builder, field.PropertyName);
        AppendLine(builder, indent + "{");
        Append(builder, indent);
        Append(builder, "    get => ");
        Append(builder, field.FieldName);
        AppendLine(builder, ";");
        AppendLine(builder, indent + "    private set");
        AppendLine(builder, indent + "    {");
        AppendPropertySetter(builder, indent, field);
        AppendLine(builder, indent + "    }");
        AppendLine(builder, indent + "}");
        AppendLine(builder);
        AppendObservableProperties(builder, indent, field, observableAsyncBridgeType);
        Append(builder, indent);
        Append(builder, "partial void On");
        Append(builder, field.PropertyName);
        Append(builder, "Received(");
        Append(builder, field.PropertyType);
        AppendLine(builder, " value);");
        AppendLine(builder);
    }

    /// <summary>Appends the generated property setter body.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Current indentation.</param>
    /// <param name="field">Tag field to emit.</param>
    private static void AppendPropertySetter(StringBuilder builder, string indent, TagField field)
    {
        Append(builder, indent);
        Append(builder, "        if (!global::System.Collections.Generic.EqualityComparer<");
        Append(builder, field.PropertyType);
        Append(builder, ">.Default.Equals(");
        Append(builder, field.FieldName);
        AppendLine(builder, ", value))");
        AppendLine(builder, indent + "        {");
        Append(builder, indent);
        Append(builder, "            ");
        Append(builder, field.FieldName);
        AppendLine(builder, " = value;");
        Append(builder, indent);
        Append(builder, "            ");
        Append(builder, field.SubjectName);
        AppendLine(builder, ".OnNext(value);");
        Append(builder, indent);
        Append(builder, "            On");
        Append(builder, field.PropertyName);
        AppendLine(builder, "Received(value);");
        AppendLine(builder, indent + "        }");
    }

    /// <summary>Appends observable properties for a generated PLC property.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Current indentation.</param>
    /// <param name="field">Tag field to emit.</param>
    /// <param name="observableAsyncBridgeType">Fully qualified observable-to-async bridge type.</param>
    private static void AppendObservableProperties(StringBuilder builder, string indent, TagField field, string observableAsyncBridgeType)
    {
        Append(builder, indent);
        Append(builder, "public global::System.IObservable<");
        Append(builder, field.PropertyType);
        Append(builder, "> ");
        Append(builder, field.PropertyName);
        Append(builder, "Observable => ");
        Append(builder, field.SubjectName);
        AppendLine(builder, ";");
        AppendLine(builder);
        AppendLine(builder, "#if NET8_0_OR_GREATER");
        Append(builder, indent);
        Append(builder, "public global::ReactiveUI.Primitives.Async.IObservableAsync<");
        Append(builder, field.PropertyType);
        Append(builder, "> ");
        Append(builder, field.PropertyName);
        Append(builder, "ObservableAsync => ");
        Append(builder, observableAsyncBridgeType);
        Append(builder, ".ToObservableAsync(");
        Append(builder, field.PropertyName);
        AppendLine(builder, "Observable);");
        AppendLine(builder, "#endif");
        AppendLine(builder);
    }

    /// <summary>Appends the generated PLC tag registration method.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Current indentation.</param>
    /// <param name="fields">Collected tag fields.</param>
    /// <param name="plcFacadeType">Fully qualified PLC facade type.</param>
    private static void AppendRegisterMethod(StringBuilder builder, string indent, IEnumerable<TagField> fields, string plcFacadeType)
    {
        AppendLine(builder, indent + "public void RegisterPlcTags(" + plcFacadeType + " plc)");
        AppendLine(builder, indent + "{");
        AppendNullGuard(builder, indent, "plc");
        AppendLine(builder);

        foreach (var field in fields)
        {
            if (field.Register)
            {
                AppendRegisterCall(builder, indent, field);
            }
        }

        AppendLine(builder, indent + "}");
        AppendLine(builder);
    }

    /// <summary>Appends one PLC tag registration call.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Current indentation.</param>
    /// <param name="field">Tag field to register.</param>
    private static void AppendRegisterCall(StringBuilder builder, string indent, TagField field)
    {
        Append(builder, indent);
        Append(builder, "    plc.AddUpdateTagItem<");
        Append(builder, field.TagType);
        Append(builder, ">(\"");
        Append(builder, EscapeString(field.Address));
        Append(builder, "\", \"");
        Append(builder, EscapeString(field.TagName));
        AppendLine(builder, "\");");
    }

    /// <summary>Appends the generated PLC binding method.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Current indentation.</param>
    /// <param name="fields">Collected tag fields.</param>
    /// <param name="plcFacadeType">Fully qualified PLC facade type.</param>
    private static void AppendBindMethod(StringBuilder builder, string indent, IEnumerable<TagField> fields, string plcFacadeType)
    {
        AppendLine(builder, indent + "public global::System.IDisposable BindPlcTags(" + plcFacadeType + " plc)");
        AppendLine(builder, indent + "{");
        AppendNullGuard(builder, indent, "plc");
        AppendLine(builder);
        AppendLine(builder, indent + "    RegisterPlcTags(plc);");
        AppendLine(builder, indent + "    var disposables = new global::ReactiveUI.Primitives.Disposables.MultipleDisposable();");

        foreach (var field in fields)
        {
            if (field.Observe)
            {
                AppendObserveSubscription(builder, indent, field);
            }
        }

        AppendLine(builder);
        AppendLine(builder, indent + "    return disposables;");
        AppendLine(builder, indent + "}");
        AppendLine(builder);
    }

    /// <summary>Appends one PLC observe subscription.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Current indentation.</param>
    /// <param name="field">Tag field to observe.</param>
    private static void AppendObserveSubscription(StringBuilder builder, string indent, TagField field)
    {
        Append(builder, indent);
        Append(builder, "    disposables.Add(plc.Observe<");
        Append(builder, field.TagType);
        Append(builder, ">(\"");
        Append(builder, EscapeString(field.TagName));
        AppendLine(builder, "\").SubscribeSafe(value =>");
        AppendLine(builder, indent + "    {");
        Append(builder, indent);
        Append(builder, "        ");
        Append(builder, field.PropertyName);
        AppendLine(builder, " = value;");
        AppendLine(builder, indent + "    }, static exception => throw exception));");
    }

    /// <summary>Appends a generated PLC write method.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Current indentation.</param>
    /// <param name="field">Writable tag field.</param>
    /// <param name="plcFacadeType">Fully qualified PLC facade type.</param>
    private static void AppendWriteMethod(StringBuilder builder, string indent, TagField field, string plcFacadeType)
    {
        Append(builder, indent);
        Append(builder, "public void Write");
        Append(builder, field.PropertyName);
        Append(builder, "(");
        Append(builder, plcFacadeType);
        Append(builder, " plc, ");
        Append(builder, field.PropertyType);
        AppendLine(builder, " value)");
        AppendLine(builder, indent + "{");
        AppendNullGuard(builder, indent, "plc");
        AppendLine(builder);
        Append(builder, indent);
        Append(builder, "    plc.Value<");
        Append(builder, field.TagType);
        Append(builder, ">(\"");
        Append(builder, EscapeString(field.TagName));
        AppendLine(builder, "\", value);");
        AppendLine(builder, indent + "}");
        AppendLine(builder);
    }

    /// <summary>Appends a generated argument null guard.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="indent">Current indentation.</param>
    /// <param name="argumentName">Argument name to guard.</param>
    private static void AppendNullGuard(StringBuilder builder, string indent, string argumentName)
    {
        AppendLine(builder, indent + $"    if ({argumentName} is null)");
        AppendLine(builder, indent + "    {");
        AppendLine(builder, indent + $"        throw new global::System.ArgumentNullException(nameof({argumentName}));");
        AppendLine(builder, indent + "    }");
    }

    /// <summary>Appends text to the builder while explicitly discarding the fluent return value.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="value">Value to append.</param>
    private static void Append(StringBuilder builder, string value) =>
        _ = builder.Append(value);

    /// <summary>Appends a line terminator to the builder.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    private static void AppendLine(StringBuilder builder) =>
        _ = builder.AppendLine();

    /// <summary>Appends text and a line terminator to the builder.</summary>
    /// <param name="builder">String builder receiving source text.</param>
    /// <param name="value">Value to append.</param>
    private static void AppendLine(StringBuilder builder, string value) =>
        _ = builder.AppendLine(value);

    /// <summary>Determines whether a type declaration is partial.</summary>
    /// <param name="typeSymbol">Type symbol to inspect.</param>
    /// <returns>True when every generated target can be added to a partial type.</returns>
    private static bool IsPartial(INamedTypeSymbol typeSymbol)
    {
        foreach (var reference in typeSymbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not TypeDeclarationSyntax declaration)
            {
                continue;
            }

            foreach (var modifier in declaration.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Gets the non-nullable PLC tag type represented by a field type.</summary>
    /// <param name="typeSymbol">Field type symbol.</param>
    /// <returns>The unwrapped nullable value type when present; otherwise the original type.</returns>
    private static ITypeSymbol GetObservableTagType(ITypeSymbol typeSymbol)
    {
        return typeSymbol is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            namedType.TypeArguments.Length == 1
            ? namedType.TypeArguments[0]
            : typeSymbol;
    }

    /// <summary>Determines whether a tag type can be read and written by OmronPlcRx.</summary>
    /// <param name="typeSymbol">Tag type symbol.</param>
    /// <returns>True when the type is supported; otherwise false.</returns>
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
            IsSupportedOmronType(typeSymbol.ToDisplayString(FullyQualifiedFormat));
    }

    /// <summary>Determines whether a fully qualified type name is an Omron-specific supported type.</summary>
    /// <param name="typeName">Fully qualified type name.</param>
    /// <returns>True when the type is supported; otherwise false.</returns>
    private static bool IsSupportedOmronType(string typeName)
    {
        return typeName is
            "global::OmronPlcRx.Core.Types.Bcd16" or
            "global::OmronPlcRx.Core.Types.BcdU16" or
            "global::OmronPlcRx.Core.Types.Bcd32" or
            "global::OmronPlcRx.Core.Types.BcdU32" or
            "global::OmronPlcRx.Reactive.Core.Types.Bcd16" or
            "global::OmronPlcRx.Reactive.Core.Types.BcdU16" or
            "global::OmronPlcRx.Reactive.Core.Types.Bcd32" or
            "global::OmronPlcRx.Reactive.Core.Types.BcdU32";
    }

    /// <summary>Determines whether a generated property would collide with an existing member.</summary>
    /// <param name="containingType">Type containing the attributed field.</param>
    /// <param name="fieldSymbol">Attributed field symbol.</param>
    /// <param name="propertyName">Generated property name.</param>
    /// <returns>True when a collision exists; otherwise false.</returns>
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

    /// <summary>Gets the PLC address from an attribute instance.</summary>
    /// <param name="attributeData">PLC tag attribute data.</param>
    /// <returns>The configured address, or an empty string when missing.</returns>
    private static string GetAddress(AttributeData attributeData)
    {
        return attributeData.ConstructorArguments.Length == 0
            ? string.Empty
            : ((attributeData.ConstructorArguments[0].Value as string) ?? string.Empty);
    }

    /// <summary>Gets a named string argument from an attribute instance.</summary>
    /// <param name="attributeData">Attribute data to inspect.</param>
    /// <param name="name">Named argument key.</param>
    /// <returns>The argument value when present; otherwise null.</returns>
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

    /// <summary>Gets a named boolean argument from an attribute instance.</summary>
    /// <param name="attributeData">Attribute data to inspect.</param>
    /// <param name="name">Named argument key.</param>
    /// <param name="defaultValue">Default value used when no argument is present.</param>
    /// <returns>The argument value when present; otherwise <paramref name="defaultValue"/>.</returns>
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

    /// <summary>Converts a backing field name into a public property name.</summary>
    /// <param name="fieldName">Field name to convert.</param>
    /// <returns>Generated property name.</returns>
    private static string ToPropertyName(string fieldName)
    {
        var trimmed = fieldName.TrimStart('_');
        if (trimmed.Length == 0)
        {
            return fieldName;
        }

        var suffix = trimmed.Length > 1 ? trimmed.Remove(0, 1) : string.Empty;
        return char.ToUpperInvariant(trimmed[0]) + suffix;
    }

    /// <summary>Creates a generated subject field name for a property.</summary>
    /// <param name="propertyName">Generated property name.</param>
    /// <returns>Generated subject field name.</returns>
    private static string ToBackingSubjectName(string propertyName)
    {
        var suffix = propertyName.Length > 1 ? propertyName.Remove(0, 1) : string.Empty;
        return "_" + char.ToLowerInvariant(propertyName[0]) + suffix + "Subject";
    }

    /// <summary>Gets source text for a type's declared accessibility.</summary>
    /// <param name="symbol">Type symbol to inspect.</param>
    /// <returns>C# accessibility text.</returns>
    private static string GetAccessibility(INamedTypeSymbol symbol)
    {
        return symbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal",
        };
    }

    /// <summary>Gets a deterministic hint name for generated source.</summary>
    /// <param name="symbol">Target type symbol.</param>
    /// <returns>Generated source hint name.</returns>
    private static string GetHintName(INamedTypeSymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty)
            .Replace('.', '_')
            .Replace('+', '_');
    }

    /// <summary>Escapes a value for use inside a generated string literal.</summary>
    /// <param name="value">Value to escape.</param>
    /// <returns>Escaped value.</returns>
    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>Collects candidate field declarations that have attributes.</summary>
    private sealed class SyntaxReceiver : ISyntaxReceiver
    {
        /// <summary>Gets field declarations that may contain PLC tag attributes.</summary>
        public List<FieldDeclarationSyntax> CandidateFields { get; } = new();

        /// <inheritdoc />
        /// <param name="syntaxNode">The syntax node value.</param>
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is not FieldDeclarationSyntax fieldDeclaration)
            {
                return;
            }

            if (fieldDeclaration.AttributeLists.Count == 0)
            {
                return;
            }

            CandidateFields.Add(fieldDeclaration);
        }
    }

    /// <summary>Describes one generated PLC tag property.</summary>
    private sealed class TagField
    {
        /// <summary>Initializes a new instance of the <see cref="TagField"/> class.</summary>
        /// <param name="fieldName">Backing field name.</param>
        /// <param name="propertyName">Generated property name.</param>
        /// <param name="subjectName">Generated signal field name.</param>
        /// <param name="address">PLC address.</param>
        /// <param name="tagName">Logical PLC tag name.</param>
        /// <param name="propertyType">Generated property type.</param>
        /// <param name="tagType">PLC observe/write type.</param>
        /// <param name="register">True when the tag should be registered.</param>
        /// <param name="observe">True when the tag should be observed.</param>
        /// <param name="writable">True when a write helper should be generated.</param>
        public TagField(
            string fieldName,
            string propertyName,
            string subjectName,
            string address,
            string tagName,
            string propertyType,
            string tagType,
            bool register,
            bool observe,
            bool writable)
        {
            FieldName = fieldName;
            PropertyName = propertyName;
            SubjectName = subjectName;
            Address = address;
            TagName = tagName;
            PropertyType = propertyType;
            TagType = tagType;
            Register = register;
            Observe = observe;
            Writable = writable;
        }

        /// <summary>Gets the field name value.</summary>
        public string FieldName { get; }

        /// <summary>Gets the property name value.</summary>
        public string PropertyName { get; }

        /// <summary>Gets the subject name value.</summary>
        public string SubjectName { get; }

        /// <summary>Gets the address value.</summary>
        public string Address { get; }

        /// <summary>Gets the tag name value.</summary>
        public string TagName { get; }

        /// <summary>Gets the property type value.</summary>
        public string PropertyType { get; }

        /// <summary>Gets the tag type value.</summary>
        public string TagType { get; }

        /// <summary>Gets the register value.</summary>
        public bool Register { get; }

        /// <summary>Gets the observe value.</summary>
        public bool Observe { get; }

        /// <summary>Gets the writable value.</summary>
        public bool Writable { get; }
    }
}
