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
/// and <c>[ToggleKey]</c>. <c>[OnEnter]</c> and <c>[OnExit]</c> stand in for a stage and register
/// the method on a state transition, and <c>[InState]</c> adds a run condition. A method the
/// generator cannot call is reported (E3D001 to E3D004) and
/// left out, so the error is on the method rather than in generated code.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class BehaviorGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor BadSignature = new(
        "E3D001",
        "A behavior stage method has the wrong signature",
        "'{0}' must return void and take one BehaviorContext parameter to run as a {1} system",
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
                        if (a.AttributeClass?.ToDisplayString() == "Engine.BehaviorAttribute")
                            return type;
                    return null;
                })
            .Where(s => s is not null)
            .Collect();

        ctx.RegisterSourceOutput(ctx.CompilationProvider.Combine(candidates), (spc, pair) =>
        {
            var behaviors = new List<BehaviorModel>();
            foreach (var type in pair.Right.OfType<INamedTypeSymbol>().Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default))
            {
                var model = BuildModel(type, spc);
                behaviors.Add(model);
                spc.AddSource($"{model.SafeName}.g.cs", GenBehaviorSystems(model));
            }

            if (behaviors.Count > 0)
                spc.AddSource("BehaviorsRegistration.g.cs", GenRegistration(behaviors));
        });
    }

    // -- Model extraction

    /// <summary>Builds a behavior model from a type symbol, reporting and skipping methods that cannot run.</summary>
    private static BehaviorModel BuildModel(INamedTypeSymbol type, SourceProductionContext spc)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? "Engine" : type.ContainingNamespace.ToDisplayString();
        var methods = new List<StageMethod>();

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

            if (!method.ReturnsVoid || method.Parameters.Length != 1 ||
                method.Parameters[0].Type.ToDisplayString() != "Engine.BehaviorContext")
            {
                spc.ReportDiagnostic(Diagnostic.Create(BadSignature, location, method.Name, stages[0]));
                continue;
            }

            string? transition = null;
            if (stages[0] is Stage.OnEnter or Stage.OnExit)
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
            if (HasAttribute(method, "Engine.InStateAttribute"))
            {
                inState = GetStateValue(method, "Engine.InStateAttribute");
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
                MethodContainer = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                MethodName = method.Name,
                Filters = GetFilters(method),
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

    /// <summary>Maps a method's attributes to the stages they name.</summary>
    private static List<Stage> GetStages(IMethodSymbol m)
    {
        var stages = new List<Stage>();
        foreach (var a in m.GetAttributes())
        {
            Stage? stage = a.AttributeClass?.ToDisplayString() switch
            {
                "Engine.OnStartupAttribute" => Stage.Startup,
                "Engine.OnFirstAttribute" => Stage.First,
                "Engine.OnPreUpdateAttribute" => Stage.PreUpdate,
                "Engine.OnFixedUpdateAttribute" => Stage.FixedUpdate,
                "Engine.OnUpdateAttribute" => Stage.Update,
                "Engine.OnPostUpdateAttribute" => Stage.PostUpdate,
                "Engine.OnRenderAttribute" => Stage.Render,
                "Engine.OnLastAttribute" => Stage.Last,
                "Engine.OnCleanupAttribute" => Stage.Cleanup,
                "Engine.OnEnterAttribute" => Stage.OnEnter,
                "Engine.OnExitAttribute" => Stage.OnExit,
                _ => null,
            };
            if (stage is not null) stages.Add(stage.Value);
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
    private static string? GetStateValue(IMethodSymbol m, string attributeName)
    {
        var a = m.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == attributeName);
        if (a is null || a.ConstructorArguments.Length != 1) return null;

        var arg = a.ConstructorArguments[0];
        if (arg.Kind != TypedConstantKind.Enum || arg.Type is not INamedTypeSymbol enumType || arg.Value is null) return null;

        var fqn = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
            if (member.HasConstantValue && Equals(member.ConstantValue, arg.Value))
                return $"{fqn}.{member.Name}";

        return $"(({fqn})({System.Convert.ToString(arg.Value, System.Globalization.CultureInfo.InvariantCulture)}))";
    }

    /// <summary>Extracts With/Without/Changed filters from method attributes.</summary>
    private static Filters GetFilters(IMethodSymbol m)
    {
        var with = new List<string>();
        var without = new List<string>();
        var changed = new List<string>();
        foreach (var a in m.GetAttributes())
        {
            var bucket = a.AttributeClass?.ToDisplayString() switch
            {
                "Engine.WithAttribute" => with,
                "Engine.WithoutAttribute" => without,
                "Engine.ChangedAttribute" => changed,
                _ => null,
            };
            if (bucket is null || a.ConstructorArguments.Length == 0) continue;
            foreach (var v in a.ConstructorArguments[0].Values)
                if (v.Value is ITypeSymbol ts)
                    bucket.Add(ts.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return new Filters(with, without, changed);
    }

    /// <summary>The member name a method's [RunIf] names, or null.</summary>
    private static string? GetRunIfName(IMethodSymbol method)
    {
        foreach (var a in method.GetAttributes())
            if (a.AttributeClass?.ToDisplayString() == "Engine.RunIfAttribute" &&
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
            if (a.AttributeClass?.ToDisplayString() != "Engine.ToggleKeyAttribute") continue;
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

        var hasFilters = m.Filters.With.Count + m.Filters.Without.Count + m.Filters.Changed.Count > 0;
        var hoist = hasFilters ? GenFilterHoist(m.Filters, "        ") : "";
        var parChecks = hasFilters ? GenFilterChecks(m.Filters, "                        ") : "";
        var seqChecks = hasFilters ? GenFilterChecks(m.Filters, "                ") : "";

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
                                      behv.{{m.MethodName}}(ctx);
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
                              behv.{{m.MethodName}}(ctx);
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
        return string.Join("\n", lines);
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
            lines.Add($"{indent}if (!__fChg{i}.ChangedThisFrame(entity, 0)) {skip};");
        return string.Join("\n", lines);
    }

    /// <summary>Emits a registration method marked with [GeneratedBehaviorRegistration] that registers all discovered behaviors.</summary>
    private static string GenRegistration(IEnumerable<BehaviorModel> behaviors)
    {
        var calls = string.Concat(behaviors.Select(b =>
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
    }

    /// <summary>Component filter configuration extracted from [With], [Without], [Changed] attributes.</summary>
    private sealed record Filters(
        IReadOnlyList<string> With,
        IReadOnlyList<string> Without,
        IReadOnlyList<string> Changed);

    /// <summary>Represents a single stage-annotated method within a behavior struct.</summary>
    private sealed record StageMethod
    {
        public Stage Stage { get; init; }
        public bool IsStatic { get; init; }
        public string MethodContainer { get; init; } = string.Empty;
        public string MethodName { get; init; } = string.Empty;

        public Filters Filters { get; init; } =
            new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        public (string Name, MemberKind Kind)? RunIf { get; init; }
        public (int Key, int Modifier, bool DefaultEnabled)? ToggleKey { get; init; }

        /// <summary>For an OnEnter or OnExit method, the enum value as source.</summary>
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
