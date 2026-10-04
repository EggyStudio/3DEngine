using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine;

/// <summary>Roslyn incremental generator scanning [Engine.Behavior] structs and emitting stage systems plus a registration function discoverable at runtime.</summary>
/// <remarks>
/// <para>
/// Produces two kinds of source outputs:
/// <list type="number">
///   <item><description>Per-behavior <c>{Name}_Generated.g.cs</c> files containing one system function per stage method.</description></item>
///   <item><description>A single <c>BehaviorsRegistration.g.cs</c> file marked with <c>[GeneratedBehaviorRegistration]</c>,
///     discoverable by <c>BehaviorsPlugin</c> at runtime via reflection.</description></item>
/// </list>
/// </para>
/// <para>
/// Each stage method is a system of its own, named after the behavior, the stage and the method,
/// so a behavior may have any number of methods on one stage, each with its own <c>[RunIf]</c>
/// and <c>[ToggleKey]</c>. <c>[OnEnter]</c>, <c>[OnExit]</c> and <c>[OnTransition]</c> stand in
/// for a stage and register the method on a state transition, and <c>[InState]</c> adds a run
/// condition. An instance method may take its entity's other components after its context, by ref
/// to write one or by in to read it, and runs only for entities with them. A method the generator
/// cannot call is reported (E3D001 to E3D005, and E3D008 for a parameter that cannot be a
/// component) and left out, so the error is on the method rather than in generated code. A field holding a reference other
/// than a string is warned of (E3D006), since every copy of the behavior shares it. An enum with
/// <c>[SubStateOf]</c> and a method with <c>[ComputedState]</c> are added as states by the same
/// registration, ahead of the behaviors, and one that cannot be is reported (E3D007).
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class BehaviorGenerator : IIncrementalGenerator
{
    // The attributes this generator reads, by full name. The code below matches against these
    // tables, and the tests run a case for every name in Attributes, so an attribute added here
    // without one fails them.
    private const string Behavior = "Engine.BehaviorAttribute";
    private const string InState = "Engine.InStateAttribute";
    private const string RunIf = "Engine.RunIfAttribute";
    private const string ToggleKey = "Engine.ToggleKeyAttribute";

    private static readonly (string Name, Stage Stage)[] StageAttributes =
    [
        ("Engine.OnStartupAttribute", Stage.Startup),
        ("Engine.OnFirstAttribute", Stage.First),
        ("Engine.OnPreUpdateAttribute", Stage.PreUpdate),
        ("Engine.OnFixedUpdateAttribute", Stage.FixedUpdate),
        ("Engine.OnUpdateAttribute", Stage.Update),
        ("Engine.OnPostUpdateAttribute", Stage.PostUpdate),
        ("Engine.OnRenderAttribute", Stage.Render),
        ("Engine.OnLastAttribute", Stage.Last),
        ("Engine.OnCleanupAttribute", Stage.Cleanup),
        ("Engine.OnEnterAttribute", Stage.OnEnter),
        ("Engine.OnExitAttribute", Stage.OnExit),
        ("Engine.OnTransitionAttribute", Stage.OnTransition),
    ];

    private const string With = "Engine.WithAttribute";
    private const string Without = "Engine.WithoutAttribute";
    private const string Changed = "Engine.ChangedAttribute";
    private const string Added = "Engine.AddedAttribute";

    /// <summary>Every attribute this generator reads, by full name.</summary>
    public static IReadOnlyList<string> Attributes { get; } =
        [Behavior, .. StageAttributes.Select(s => s.Name), InState, With, Without, Changed, Added, RunIf, ToggleKey, SubStateOf, ComputedState];

    private static readonly DiagnosticDescriptor BadSignature = new(
        "E3D001",
        "A behavior stage method has the wrong signature",
        "'{0}' must return void and take a BehaviorContext parameter first to run as a {1} system",
        "Behaviors", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SeveralStages = new(
        "E3D002",
        "A behavior method names more than one stage",
        "'{0}' carries more than one stage attribute, and a method runs in one stage",
        "Behaviors", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor BadRunIf = new(
        "E3D003",
        "A [RunIf] member is missing or has the wrong shape",
        "[RunIf(\"{0}\")] on '{1}' must name a static bool field or property, or a static bool method taking a World, on the same behavior",
        "Behaviors", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor BadState = new(
        "E3D004",
        "A state attribute does not name an enum value",
        "[{0}] on '{1}' must name a value of an enum, such as Screen.Playing",
        "Behaviors", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor BadFilterType = new(
        "E3D005",
        "A filter names a type no entity can have",
        "[{0}(typeof({1}))] on '{2}' names {3}, which no entity can have as a component",
        "Behaviors", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor BadStateDeclaration = new(
        "E3D007",
        "A state declaration cannot be registered",
        "'{0}' cannot declare a state: {1}",
        "Behaviors", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor BadComponentParameter = new(
        "E3D008",
        "A behavior method's parameter is not one of its entity's components",
        "'{0}' cannot take '{1}': {2}",
        "Behaviors", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private const string SubStateOf = "Engine.SubStateOfAttribute";
    private const string ComputedState = "Engine.ComputedStateAttribute";

    private static readonly DiagnosticDescriptor SharedField = new(
        "E3D006",
        "A behavior's field holds a reference",
        "'{0}.{1}' holds a {2}, which every copy of the behavior shares, so entities run in parallel can change it at once; keep the data in the struct, or in a resource",
        "Behaviors", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    /// <summary>Configures syntax providers, collects candidate structs, and registers source outputs.</summary>
    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        var candidates = ctx.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is StructDeclarationSyntax sds && sds.AttributeLists.Count > 0,
                static (context, _) =>
                {
                    var sds = (StructDeclarationSyntax)context.Node;
                    var type = context.SemanticModel.GetDeclaredSymbol(sds);
                    if (type is null) return null;
                    foreach (var a in type.GetAttributes())
                        if (a.AttributeClass?.ToDisplayString() == Behavior)
                            return type;
                    return null;
                })
            .Where(s => s is not null)
            .Collect();

        // Enums declared sub-states, and static methods declaring computed states.
        var states = ctx.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is EnumDeclarationSyntax { AttributeLists.Count: > 0 } or MethodDeclarationSyntax { AttributeLists.Count: > 0 },
                static (context, _) =>
                {
                    var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node);
                    if (symbol is null) return null;
                    foreach (var a in symbol.GetAttributes())
                        if (a.AttributeClass?.ToDisplayString() is SubStateOf or ComputedState)
                            return symbol;
                    return null;
                })
            .Where(s => s is not null)
            .Collect();

        ctx.RegisterSourceOutput(ctx.CompilationProvider.Combine(candidates).Combine(states), (spc, pair) =>
        {
            var behaviors = new List<BehaviorModel>();
            foreach (var type in pair.Left.Right.OfType<INamedTypeSymbol>().Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default))
            {
                var model = BuildModel(type, spc);
                behaviors.Add(model);
                spc.AddSource($"{model.SafeName}.g.cs", GenBehaviorSystems(model));
            }

            var stateCalls = new List<string>();
            foreach (var symbol in pair.Right.OfType<ISymbol>().Distinct<ISymbol>(SymbolEqualityComparer.Default))
                if (StateRegistration(symbol, spc) is { } call)
                    stateCalls.Add(call);

            if (behaviors.Count > 0 || stateCalls.Count > 0)
                spc.AddSource("BehaviorsRegistration.g.cs", GenRegistration(behaviors, stateCalls));
        });
    }

    // -- Model extraction

    /// <summary>Builds a behavior model from a type symbol, reporting and skipping methods that cannot run.</summary>
    private static BehaviorModel BuildModel(INamedTypeSymbol type, SourceProductionContext spc)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? "Engine" : type.ContainingNamespace.ToDisplayString();
        var methods = new List<StageMethod>();

        // A string is shared too, but nothing can change it.
        foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
            if (!field.IsStatic && !field.IsConst && field.Type.IsReferenceType && field.Type.SpecialType != SpecialType.System_String)
                spc.ReportDiagnostic(Diagnostic.Create(SharedField, field.Locations.FirstOrDefault(),
                    type.Name, field.Name, field.Type.ToDisplayString()));

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var stages = GetStages(method);
            if (stages.Count == 0) continue;

            var location = method.Locations.FirstOrDefault();
            if (stages.Count > 1)
            {
                spc.ReportDiagnostic(Diagnostic.Create(SeveralStages, location, method.Name));
                continue;
            }

            if (!method.ReturnsVoid || method.Parameters.Length == 0 ||
                method.Parameters[0].Type.ToDisplayString() != "Engine.BehaviorContext")
            {
                spc.ReportDiagnostic(Diagnostic.Create(BadSignature, location, method.Name, stages[0]));
                continue;
            }

            var components = ComponentParameters(method, type, out var badParameter);
            if (components is null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(BadComponentParameter, badParameter!.Value.Location ?? location,
                    method.Name, badParameter.Value.Parameter, badParameter.Value.Why));
                continue;
            }

            if (ImpossibleFilter(method) is { } impossible)
            {
                spc.ReportDiagnostic(Diagnostic.Create(BadFilterType, location, impossible.Attribute, impossible.Type, method.Name, impossible.Why));
                continue;
            }

            string? transition = null;
            if (stages[0] is Stage.OnTransition)
            {
                var from = GetStateValue(method, "Engine.OnTransitionAttribute", 0, out var fromType);
                var to = GetStateValue(method, "Engine.OnTransitionAttribute", 1, out var toType);
                if (from is null || to is null || !SymbolEqualityComparer.Default.Equals(fromType, toType))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(BadState, location, "OnTransition", method.Name));
                    continue;
                }
                transition = $"{from}, {to}";
            }
            else if (stages[0] is Stage.OnEnter or Stage.OnExit)
            {
                var attributeName = stages[0] == Stage.OnEnter ? "OnEnter" : "OnExit";
                transition = GetStateValue(method, $"Engine.{attributeName}Attribute");
                if (transition is null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(BadState, location, attributeName, method.Name));
                    continue;
                }
            }

            string? inState = null;
            if (HasAttribute(method, InState))
            {
                inState = GetStateValue(method, InState);
                if (inState is null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(BadState, location, "InState", method.Name));
                    continue;
                }
            }

            var runIfName = GetRunIfName(method);
            (string Name, MemberKind Kind)? runIf = null;
            if (runIfName is not null)
            {
                var kind = ResolveRunIf(runIfName, type);
                if (kind is null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(BadRunIf, location, runIfName, method.Name));
                    continue;
                }
                runIf = (runIfName, kind.Value);
            }

            methods.Add(new StageMethod
            {
                Stage = stages[0],
                IsStatic = method.IsStatic,
                IsReadOnly = method.IsReadOnly,
                MethodContainer = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                MethodName = method.Name,
                Filters = GetFilters(method),
                Components = components,
                RunIf = runIf,
                ToggleKey = GetToggleKey(method),
                Transition = transition,
                InState = inState,
            });
        }

        return new BehaviorModel
        {
            Namespace = ns,
            Name = type.Name,
            SafeName = type.Name + "_Generated",
            BehaviorFqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            StageMethods = methods,
        };
    }

    // The components a method takes after its context, each with whether it writes it, or null
    // with the first parameter that cannot be one and why. A parameter is the entity's component
    // when it is a struct taken by ref, which the method may write, or by in or ref readonly, which
    // it only reads.
    private static List<ComponentParameter>? ComponentParameters(IMethodSymbol method, INamedTypeSymbol behavior,
        out (string Parameter, string Why, Location? Location)? problem)
    {
        problem = null;
        var components = new List<ComponentParameter>();
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var parameter in method.Parameters.Skip(1))
        {
            var type = parameter.Type;
            string? why =
                method.IsStatic ? "a static method runs once for no entity, so it takes no components"
                : parameter.RefKind is not (RefKind.Ref or RefKind.In or RefKind.RefReadOnlyParameter)
                    ? "a component is taken by ref to write it, or by in to read it"
                : type.TypeKind != TypeKind.Struct || type.IsRefLikeType || type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
                    ? $"{type.ToDisplayString()} is not a struct an entity can hold"
                : SymbolEqualityComparer.Default.Equals(type, behavior)
                    ? "the behavior is its own component, which the method reaches through this"
                : !seen.Add(type)
                    ? $"it takes {type.ToDisplayString()} twice, and one component cannot be handed out twice"
                : null;
            if (why is not null)
            {
                problem = ($"{parameter.Type.ToDisplayString()} {parameter.Name}", why, parameter.Locations.FirstOrDefault());
                return null;
            }
            components.Add(new ComponentParameter(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), parameter.RefKind == RefKind.Ref));
        }
        return components;
    }

    // The first filter on a method naming a type no store can hold, as an interface, a static class
    // or an open generic, which would compile and never match.
    private static (string Attribute, string Type, string Why)? ImpossibleFilter(IMethodSymbol m)
    {
        foreach (var a in m.GetAttributes())
        {
            var name = a.AttributeClass?.ToDisplayString();
            if (name is not (With or Without or Changed or Added) || a.ConstructorArguments.Length == 0) continue;
            foreach (var v in a.ConstructorArguments[0].Values)
            {
                if (v.Value is not ITypeSymbol t) continue;
                var why = t.TypeKind == TypeKind.Interface ? "an interface"
                    : t.IsStatic ? "a static class"
                    : t is INamedTypeSymbol { IsUnboundGenericType: true } ? "an open generic type"
                    : null;
                if (why is not null)
                    return (a.AttributeClass!.Name.Replace("Attribute", ""), t.ToDisplayString(), why);
            }
        }
        return null;
    }

    /// <summary>Maps a method's attributes to the stages they name.</summary>
    private static List<Stage> GetStages(IMethodSymbol m)
    {
        var stages = new List<Stage>();
        foreach (var a in m.GetAttributes())
        {
            var name = a.AttributeClass?.ToDisplayString();
            foreach (var (attribute, stage) in StageAttributes)
                if (attribute == name) stages.Add(stage);
        }

        return stages;
    }

    private static bool HasAttribute(IMethodSymbol m, string name) =>
        m.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == name);

    /// <summary>
    /// The enum value a state attribute names, as source naming the member, or null when its
    /// argument is not an enum value.
    /// </summary>
    /// <remarks>
    /// The attribute takes an <c>object</c>, so the compiler accepts any constant and this is
    /// where a number or a string is caught. A value with no member of its own, such as a cast
    /// number, is written as a cast.
    /// </remarks>
    private static string? GetStateValue(IMethodSymbol m, string attributeName) => GetStateValue(m, attributeName, 0, out _);

    // The attribute's argument at index as an enum value in source, with its enum type, or null
    // when it is not an enum value.
    private static string? GetStateValue(IMethodSymbol m, string attributeName, int index, out INamedTypeSymbol? type)
    {
        type = null;
        var a = m.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == attributeName);
        if (a is null || a.ConstructorArguments.Length <= index) return null;

        var arg = a.ConstructorArguments[index];
        if (arg.Kind != TypedConstantKind.Enum || arg.Type is not INamedTypeSymbol enumType || arg.Value is null) return null;
        type = enumType;

        var fqn = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
            if (member.HasConstantValue && Equals(member.ConstantValue, arg.Value))
                return $"{fqn}.{member.Name}";

        return $"(({fqn})({System.Convert.ToString(arg.Value, System.Globalization.CultureInfo.InvariantCulture)}))";
    }

    /// <summary>Extracts With/Without/Changed/Added filters from method attributes.</summary>
    private static Filters GetFilters(IMethodSymbol m)
    {
        var with = new List<string>();
        var without = new List<string>();
        var changed = new List<string>();
        var added = new List<string>();
        foreach (var a in m.GetAttributes())
        {
            var bucket = a.AttributeClass?.ToDisplayString() switch
            {
                With => with,
                Without => without,
                Changed => changed,
                Added => added,
                _ => null,
            };
            if (bucket is null || a.ConstructorArguments.Length == 0) continue;
            foreach (var v in a.ConstructorArguments[0].Values)
                if (v.Value is ITypeSymbol ts)
                    bucket.Add(ts.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return new Filters(with, without, changed, added);
    }

    /// <summary>The member name a method's [RunIf] names, or null.</summary>
    private static string? GetRunIfName(IMethodSymbol method)
    {
        foreach (var a in method.GetAttributes())
            if (a.AttributeClass?.ToDisplayString() == RunIf &&
                a.ConstructorArguments.Length > 0 &&
                a.ConstructorArguments[0].Value is string name)
                return name;
        return null;
    }

    /// <summary>
    /// Classifies the member a [RunIf] names, or returns null when it is not a static bool field or
    /// property, or a static bool method taking a World, which are the shapes the emitted call fits.
    /// </summary>
    private static MemberKind? ResolveRunIf(string name, INamedTypeSymbol behaviorType)
    {
        foreach (var member in behaviorType.GetMembers(name))
        {
            if (!member.IsStatic) continue;
            switch (member)
            {
                case IMethodSymbol m when m.ReturnType.SpecialType == SpecialType.System_Boolean &&
                                          m.Parameters.Length == 1 &&
                                          m.Parameters[0].Type.ToDisplayString() == "Engine.World":
                    return MemberKind.Method;
                case IPropertySymbol p when p.Type.SpecialType == SpecialType.System_Boolean:
                    return MemberKind.Property;
                case IFieldSymbol f when f.Type.SpecialType == SpecialType.System_Boolean:
                    return MemberKind.Field;
            }
        }

        return null;
    }

    /// <summary>Classifies the kind of member referenced by a [RunIf] attribute.</summary>
    private enum MemberKind
    {
        Method,
        Property,
        Field
    }

    /// <summary>Extracts the [ToggleKey] key+modifier pair as raw integers, or null if absent.</summary>
    private static (int Key, int Modifier, bool DefaultEnabled)? GetToggleKey(IMethodSymbol m)
    {
        foreach (var a in m.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() != ToggleKey) continue;
            var key = a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is int k ? k : 0;
            var mod = a.ConstructorArguments.Length > 1 && a.ConstructorArguments[1].Value is int mo ? mo : 0;
            var def = true;
            foreach (var na in a.NamedArguments)
                if (na.Key == "DefaultEnabled" && na.Value.Value is bool bb)
                    def = bb;
            return (key, mod, def);
        }

        return null;
    }

    // -- Source emission

    private static string SystemName(BehaviorModel b, StageMethod m) => $"{b.SafeName}_{m.Stage}_{m.MethodName}";

    /// <summary>Generates one system function per stage method and a static Register helper for one behavior.</summary>
    private static string GenBehaviorSystems(BehaviorModel b)
    {
        var registerCalls = string.Concat(b.StageMethods.Select(m => m.Stage switch
        {
            Stage.OnEnter => $"        app.OnEnter({m.Transition}, {BuildDescriptor(b, m)});\n",
            Stage.OnExit => $"        app.OnExit({m.Transition}, {BuildDescriptor(b, m)});\n",
            Stage.OnTransition => $"        app.OnTransition({m.Transition}, {BuildDescriptor(b, m)});\n",
            _ => $"        app.AddSystem(Engine.Stage.{m.Stage}, {BuildDescriptor(b, m)});\n",
        }));

        var stageMethods = string.Concat(b.StageMethods.Select(m => "\n" + GenStageMethod(b, m)));

        return
            $$"""
              // <auto-generated />
              namespace {{b.Namespace}};

              internal static class {{b.SafeName}}
              {
                  public static void Register(Engine.App app)
                  {
              {{registerCalls}}    }
              {{stageMethods}}
              }

              """;
    }

    /// <summary>Builds the <c>SystemDescriptor</c> chained-call expression for one stage method.</summary>
    /// <remarks>
    /// Fine-grained resource access: an instance method writes only to its own component store
    /// type, and a static method declares a read on EcsWorld. This prevents false write/write
    /// conflicts between unrelated behavior types, allowing the parallel scheduler to batch them
    /// together. A [ToggleKey] and a [RunIf] on one method both apply.
    /// </remarks>
    private static string BuildDescriptor(BehaviorModel b, StageMethod m)
    {
        var systemId = SystemName(b, m);
        var descriptor = $"new global::Engine.SystemDescriptor({systemId}, \"{systemId}\")";

        if (m.ToggleKey is { } tk)
        {
            var def = tk.DefaultEnabled ? "true" : "false";
            descriptor += $".RunIf(global::Engine.BehaviorConditions.KeyToggle(\"{systemId}\", (global::Engine.Key){tk.Key}, (global::Engine.KeyModifier){tk.Modifier}, {def}))";
        }

        if (m.InState is { } state)
            descriptor += $".RunIf(global::Engine.BehaviorConditions.InState({state}))";

        if (m.RunIf is { } ri)
        {
            var expr = ri.Kind == MemberKind.Method
                ? $"{b.BehaviorFqn}.{ri.Name}"
                : $"_ => {b.BehaviorFqn}.{ri.Name}";
            descriptor += $".RunIf({expr})";
        }

        // The components it takes are written or read as it takes them, so the scheduler keeps a
        // system writing one apart from others using it.
        foreach (var c in m.Components)
            descriptor += c.Writes ? $".Write<{c.Type}>()" : $".Read<{c.Type}>()";
        return descriptor + (m.IsStatic ? ".Read<global::Engine.EcsWorld>()" : $".Write<{b.BehaviorFqn}>()");
    }

    /// <summary>Generates one system method body (static dispatch or chunked parallel iteration).</summary>
    /// <remarks>
    /// Non-static optimizations applied (Arch/Bevy ECS patterns):
    /// <list type="number">
    ///   <item><description>Pre-resolve all World resources once (avoid ConcurrentDictionary lookups per thread/chunk).</description></item>
    ///   <item><description>Direct array access + ref var (no struct copies, no method call overhead).</description></item>
    ///   <item><description>Hoisted filter store lookups (avoid GetStore indirection per entity).</description></item>
    ///   <item><description>Chunked range partitioning via Parallel.ForEach + Partitioner.Create.</description></item>
    /// </list>
    /// </remarks>
    private static string GenStageMethod(BehaviorModel b, StageMethod m)
    {
        var name = SystemName(b, m);

        if (m.IsStatic)
        {
            return
                $$"""
                      private static void {{name}}(Engine.World world)
                      {
                          var ctx = new Engine.BehaviorContext(world);
                          {{m.MethodContainer}}.{{m.MethodName}}(ctx);
                      }
                  """;
        }

        var hasFilters = m.Filters.With.Count + m.Filters.Without.Count + m.Filters.Changed.Count + m.Filters.Added.Count > 0;
        var hoist = (hasFilters ? GenFilterHoist(m.Filters, "        ") : "") + GenComponentHoist(m.Components, "        ");
        var parChecks = (hasFilters ? GenFilterChecks(m.Filters, "                        ") : "") + GenComponentLookups(m.Components, "                        ", threadSafe: true);
        var seqChecks = (hasFilters ? GenFilterChecks(m.Filters, "                ") : "") + GenComponentLookups(m.Components, "                ", threadSafe: false);
        var arguments = string.Concat(m.Components.Select((c, i) => $", {(c.Writes ? "ref" : "in")} __c{i}.ComponentRefByDenseIndex(__d{i})"));
        // A method that may write the behavior's fields marks it changed, as GetRef marks what it
        // hands out, so a [Changed] filter on the behavior sees it. Parallel runs set the bit
        // atomically, since neighbouring entities share a word of bits.
        var parMark = m.IsReadOnly ? "" : "                        __store.MarkChangedByDenseIndexThreadSafe(__i);";
        var seqMark = m.IsReadOnly ? "" : "                __store.MarkChangedByDenseIndex(__i, 0);";

        return
            $$"""
                  private static void {{name}}(Engine.World world)
                  {
                      var ecs = world.Resource<Engine.EcsWorld>();
                      var __cmd = world.Resource<Engine.EcsCommands>();
                      var __time = world.Resource<Engine.Time>();
                      var __input = world.Resource<Engine.Input>();
                      var __store = ecs.GetStorePublic<{{b.BehaviorFqn}}>();
                      var __count = __store.Count;
                      if (__count == 0) return;
                      var __entities = __store.EntitiesArray;
                      var __components = __store.ComponentsArray;
              {{hoist}}
                      if (__count >= 4096)
                      {
                          System.Threading.Tasks.Parallel.ForEach(
                              System.Collections.Concurrent.Partitioner.Create(0, __count,
                                  System.Math.Max(256, __count / (System.Environment.ProcessorCount * 4))),
                              __range =>
                              {
                                  var ctx = new Engine.BehaviorContext(world, ecs, __cmd, __time, __input);
                                  for (int __i = __range.Item1; __i < __range.Item2; __i++)
                                  {
                                      int entity = __entities[__i];
              {{parChecks}}
                                      ctx.EntityId = entity;
                                      ref var behv = ref __components[__i];
              {{parMark}}
                                      behv.{{m.MethodName}}(ctx{{arguments}});
                                  }
                              }
                          );
                      }
                      else
                      {
                          var ctx = new Engine.BehaviorContext(world, ecs, __cmd, __time, __input);
                          for (int __i = 0; __i < __count; __i++)
                          {
                              int entity = __entities[__i];
              {{seqChecks}}
                              ctx.EntityId = entity;
                              ref var behv = ref __components[__i];
              {{seqMark}}
                              behv.{{m.MethodName}}(ctx{{arguments}});
                          }
                      }
                  }
              """;
    }

    /// <summary>Emits variable declarations that hoist filter store lookups out of the hot loop.</summary>
    private static string GenFilterHoist(Filters f, string indent)
    {
        var lines = new List<string>();
        for (int i = 0; i < f.With.Count; i++)
            lines.Add($"{indent}var __fWith{i} = ecs.GetStorePublic<{f.With[i]}>();");
        for (int i = 0; i < f.Without.Count; i++)
            lines.Add($"{indent}var __fWout{i} = ecs.GetStorePublic<{f.Without[i]}>();");
        for (int i = 0; i < f.Changed.Count; i++)
            lines.Add($"{indent}var __fChg{i} = ecs.GetStorePublic<{f.Changed[i]}>();");
        for (int i = 0; i < f.Added.Count; i++)
            lines.Add($"{indent}var __fAdd{i} = ecs.GetStorePublic<{f.Added[i]}>();");
        return string.Join("\n", lines);
    }

    /// <summary>Emits the stores of the components a method takes, hoisted out of the loop.</summary>
    private static string GenComponentHoist(IReadOnlyList<ComponentParameter> components, string indent) =>
        string.Concat(components.Select((c, i) => $"\n{indent}var __c{i} = ecs.GetStorePublic<{c.Type}>();"));

    /// <summary>
    /// Emits the lookup of each component a method takes, skipping an entity without one, and marks
    /// one taken by ref changed, as GetRef marks what it hands out.
    /// </summary>
    private static string GenComponentLookups(IReadOnlyList<ComponentParameter> components, string indent, bool threadSafe)
    {
        var lines = new List<string>();
        for (int i = 0; i < components.Count; i++)
        {
            lines.Add($"{indent}var __d{i} = __c{i}.DenseIndexOf(entity);");
            lines.Add($"{indent}if (__d{i} < 0) continue;");
        }
        for (int i = 0; i < components.Count; i++)
            if (components[i].Writes)
                lines.Add(threadSafe
                    ? $"{indent}__c{i}.MarkChangedByDenseIndexThreadSafe(__d{i});"
                    : $"{indent}__c{i}.MarkChangedByDenseIndex(__d{i}, 0);");
        return lines.Count == 0 ? "" : "\n" + string.Join("\n", lines);
    }

    /// <summary>Emits per-entity filter checks using the hoisted store variables from <see cref="GenFilterHoist"/>.</summary>
    private static string GenFilterChecks(Filters f, string indent, string skip = "continue")
    {
        var lines = new List<string>();
        for (int i = 0; i < f.With.Count; i++)
            lines.Add($"{indent}if (!__fWith{i}.Has(entity)) {skip};");
        for (int i = 0; i < f.Without.Count; i++)
            lines.Add($"{indent}if (__fWout{i}.Has(entity)) {skip};");
        for (int i = 0; i < f.Changed.Count; i++)
            lines.Add($"{indent}if (!__fChg{i}.Changed(entity)) {skip};");
        for (int i = 0; i < f.Added.Count; i++)
            lines.Add($"{indent}if (!__fAdd{i}.Added(entity)) {skip};");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// The call that adds a declared state: a sub-state for an enum with [SubStateOf], a computed
    /// state for a method with [ComputedState], or null with the reason reported.
    /// </summary>
    private static string? StateRegistration(ISymbol symbol, SourceProductionContext spc)
    {
        var location = symbol.Locations.FirstOrDefault();
        string? Fail(string why)
        {
            spc.ReportDiagnostic(Diagnostic.Create(BadStateDeclaration, location, symbol.Name, why));
            return null;
        }

        if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Enum } sub)
        {
            var attribute = sub.GetAttributes().First(a => a.AttributeClass?.ToDisplayString() == SubStateOf);
            if (attribute.ConstructorArguments.Length != 1 || EnumValue(attribute.ConstructorArguments[0]) is not { } parent)
                return Fail("[SubStateOf] names a value of the parent state's enum");
            var subName = sub.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string? initial;
            var named = attribute.NamedArguments.FirstOrDefault(n => n.Key == "Initial");
            if (named.Key is null)
            {
                var first = sub.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => f.HasConstantValue);
                if (first is null) return Fail("a sub-state's enum has at least one member");
                initial = $"{subName}.{first.Name}";
            }
            else if (EnumValue(named.Value) is { } value && SymbolEqualityComparer.Default.Equals(named.Value.Type, sub))
                initial = value.Source;
            else
                return Fail("Initial is one of the sub-state's own values");
            return $"        app.AddSubState<{subName}, {parent.Type}>({parent.Source}, {initial});\n";
        }

        if (symbol is IMethodSymbol method)
        {
            if (!method.IsStatic || method.Parameters.Length != 1 || method.Parameters[0].Type.TypeKind != TypeKind.Enum
                || method.ReturnType is not INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                || nullable.TypeArguments[0].TypeKind != TypeKind.Enum)
                return Fail("[ComputedState] is on a static method taking the source state's enum and returning the computed one's, nullable");
            if (method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                return Fail("[ComputedState] is on a public or internal method, which the registration calls");
            var computed = nullable.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var source = method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var owner = method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"        app.AddComputedState<{computed}, {source}>({owner}.{method.Name});\n";
        }
        return null;
    }

    // An enum constant as source, with its enum type, or null when the constant is not an enum value.
    private static (string Source, string Type)? EnumValue(TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Enum || constant.Type is not INamedTypeSymbol enumType || constant.Value is null) return null;
        var fqn = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
            if (member.HasConstantValue && Equals(member.ConstantValue, constant.Value))
                return ($"{fqn}.{member.Name}", fqn);
        return ($"(({fqn})({System.Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture)}))", fqn);
    }

    /// <summary>
    /// Emits a registration method marked with [GeneratedBehaviorRegistration] that adds every
    /// declared state, then registers every discovered behavior.
    /// </summary>
    private static string GenRegistration(IEnumerable<BehaviorModel> behaviors, IEnumerable<string> stateCalls)
    {
        var calls = string.Concat(stateCalls) + string.Concat(behaviors.Select(b =>
            $"        global::{b.Namespace}.{b.SafeName}.Register(app);\n"));

        return
            $$"""
              // <auto-generated />
              namespace Engine;

              public static class BehaviorRegistration
              {
                  [global::Engine.GeneratedBehaviorRegistration]
                  public static void Register(global::Engine.App app)
                  {
              {{calls}}    }

                  // Adds the registration to the engine's list as the assembly loads, so the
                  // behaviors plugin finds it with no search of the assembly's types.
                  [global::System.Runtime.CompilerServices.ModuleInitializer]
                  internal static void AddToEngine() => global::Engine.GeneratedBehaviors.Add(Register);
              }

              """;
    }

    // -- Intermediate representation

    /// <summary>Scheduling stage for generated system registration.</summary>
    private enum Stage
    {
        Startup,
        First,
        PreUpdate,
        FixedUpdate,
        Update,
        PostUpdate,
        Render,
        Last,
        Cleanup,

        // Not stages of the engine. A method carrying one runs on a state transition instead.
        OnEnter,
        OnExit,
        OnTransition,
    }

    /// <summary>Component filter configuration extracted from [With], [Without], [Changed] and [Added] attributes.</summary>
    private sealed record Filters(
        IReadOnlyList<string> With,
        IReadOnlyList<string> Without,
        IReadOnlyList<string> Changed,
        IReadOnlyList<string> Added);

    /// <summary>A component a stage method takes after its context, by its full name, and whether it is taken by ref to write.</summary>
    private sealed record ComponentParameter(string Type, bool Writes);

    /// <summary>Represents a single stage-annotated method within a behavior struct.</summary>
    private sealed record StageMethod
    {
        public Stage Stage { get; init; }
        public bool IsStatic { get; init; }

        /// <summary>A <c>readonly</c> method, which C# keeps from writing the behavior's fields, so running it marks nothing changed.</summary>
        public bool IsReadOnly { get; init; }

        public string MethodContainer { get; init; } = string.Empty;
        public string MethodName { get; init; } = string.Empty;

        public Filters Filters { get; init; } =
            new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        /// <summary>The entity's components the method takes after its context, in order.</summary>
        public IReadOnlyList<ComponentParameter> Components { get; init; } = Array.Empty<ComponentParameter>();

        public (string Name, MemberKind Kind)? RunIf { get; init; }
        public (int Key, int Modifier, bool DefaultEnabled)? ToggleKey { get; init; }

        /// <summary>For an OnEnter or OnExit method, the enum value as source, and for an OnTransition one the two.</summary>
        public string? Transition { get; init; }

        /// <summary>The enum value an [InState] names, as source.</summary>
        public string? InState { get; init; }
    }

    /// <summary>Aggregated model for a single [Behavior]-annotated struct and its stage methods.</summary>
    private sealed record BehaviorModel
    {
        public string Namespace { get; init; } = "Engine";
        public string Name { get; init; } = string.Empty;
        public string SafeName { get; init; } = string.Empty;
        public string BehaviorFqn { get; init; } = string.Empty;
        public List<StageMethod> StageMethods { get; init; } = new();
    }
}
