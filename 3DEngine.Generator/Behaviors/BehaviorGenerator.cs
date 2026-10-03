using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine;

/// <summary>Roslyn incremental generator scanning [Engine.Behavior] structs and emitting stage systems plus a registration function discoverable at runtime.</summary>
/// <remarks>
/// <para>
/// Produces two kinds of source outputs:
/// <list type="number">
///   <item><description>Per-behavior <c>{Name}_Generated.g.cs</c> files containing system functions for each stage method.</description></item>
///   <item><description>A single <c>BehaviorsRegistration.g.cs</c> file marked with <c>[GeneratedBehaviorRegistration]</c>,
///     discoverable by <c>BehaviorsPlugin</c> at runtime via reflection.</description></item>
/// </list>
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class BehaviorGenerator : IIncrementalGenerator
{
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
            var behaviors = pair.Right
                .OfType<INamedTypeSymbol>()
                .Select(BuildModel)
                .ToList();

            foreach (var b in behaviors)
                spc.AddSource($"{b.SafeName}.g.cs", GenBehaviorSystems(b));

            if (behaviors.Count > 0)
                spc.AddSource("BehaviorsRegistration.g.cs", GenRegistration(behaviors));
        });
    }

    // -- Model extraction --

    /// <summary>Builds a behavior model (namespace, name, stage methods, filters) from a type symbol.</summary>
    private static BehaviorModel BuildModel(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? "Engine" : type.ContainingNamespace.ToDisplayString();
        var methods = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Select(m => (Method: m, Stage: GetStage(m)))
            .Where(x => x.Stage is not null)
            .Select(x => new StageMethod
            {
                Stage = x.Stage!.Value,
                IsStatic = x.Method.IsStatic,
                MethodContainer = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                MethodName = x.Method.Name,
                Filters = GetFilters(x.Method),
                RunIf = GetRunIf(x.Method, type),
                ToggleKey = GetToggleKey(x.Method),
            })
            .ToList();

        return new BehaviorModel
        {
            Namespace = ns,
            Name = type.Name,
            SafeName = type.Name + "_Generated",
            BehaviorFqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            StageMethods = methods,
        };
    }

    /// <summary>Maps method attributes to a scheduling stage if present.</summary>
    private static Stage? GetStage(IMethodSymbol m)
    {
        foreach (var a in m.GetAttributes())
        {
            switch (a.AttributeClass?.ToDisplayString())
            {
                case "Engine.OnStartupAttribute": return Stage.Startup;
                case "Engine.OnFirstAttribute": return Stage.First;
                case "Engine.OnPreUpdateAttribute": return Stage.PreUpdate;
                case "Engine.OnUpdateAttribute": return Stage.Update;
                case "Engine.OnPostUpdateAttribute": return Stage.PostUpdate;
                case "Engine.OnRenderAttribute": return Stage.Render;
                case "Engine.OnLastAttribute": return Stage.Last;
                case "Engine.OnCleanupAttribute": return Stage.Cleanup;
            }
        }

        return null;
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

    /// <summary>Extracts the [RunIf] condition member (method/property/field) info from a method's attributes.</summary>
    private static (string Name, MemberKind Kind)? GetRunIf(IMethodSymbol method, INamedTypeSymbol behaviorType)
    {
        string? attrName = null;
        foreach (var a in method.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() == "Engine.RunIfAttribute" &&
                a.ConstructorArguments.Length > 0 &&
                a.ConstructorArguments[0].Value is string name)
            {
                attrName = name;
                break;
            }
        }

        if (attrName is null) return null;

        foreach (var member in behaviorType.GetMembers())
        {
            if (member.Name != attrName) continue;
            if (member is IMethodSymbol) return (attrName, MemberKind.Method);
            if (member is IPropertySymbol) return (attrName, MemberKind.Property);
            if (member is IFieldSymbol) return (attrName, MemberKind.Field);
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

    // -- Source emission --

    /// <summary>Generates per-stage system functions and a static Register helper for one behavior.</summary>
    private static string GenBehaviorSystems(BehaviorModel b)
    {
        var registerCalls = string.Concat(b.StageMethods
            .GroupBy(m => m.Stage)
            .Select(g =>
                $"        app.AddSystem(Engine.Stage.{g.Key}, {BuildDescriptor(b, g.Key, g.First(), g.Any(m => !m.IsStatic))});\n"));

        var stageMethods = string.Concat(b.StageMethods.Select(m => "\n" + GenStageMethod(b, m)));

        return
            $$"""
              // <auto-generated />
              namespace {{b.Namespace}};

              internal static class {{b.SafeName}}
              {
                  public static void Register(Engine.App app)
                  {
                      {{registerCalls}}    
                  }
                  {{stageMethods}}
              }

              """;
    }

    /// <summary>Builds the <c>SystemDescriptor</c> chained-call expression for one stage group.</summary>
    /// <remarks>
    /// Fine-grained resource access: instance behaviors write only to their own component
    /// store type; static-only behaviors declare a read on EcsWorld. This prevents false
    /// write/write conflicts between unrelated behavior types, allowing the parallel
    /// scheduler to batch them together.
    /// </remarks>
    private static string BuildDescriptor(BehaviorModel b, Stage stage, StageMethod first, bool hasInstanceMethod)
    {
        var systemId = $"{b.SafeName}_{stage}";
        var access = hasInstanceMethod
            ? $".Write<{b.BehaviorFqn}>()"
            : ".Read<global::Engine.EcsWorld>()";
        var ctor = $"new global::Engine.SystemDescriptor({systemId}, \"{systemId}\")";

        if (first.ToggleKey is { } tk)
        {
            var def = tk.DefaultEnabled ? "true" : "false";
            return
                $"{ctor}.RunIf(global::Engine.BehaviorConditions.KeyToggle(\"{systemId}\", (global::Engine.Key){tk.Key}, (global::Engine.KeyModifier){tk.Modifier}, {def})){access}";
        }

        if (first.RunIf is { } ri)
        {
            var expr = ri.Kind == MemberKind.Method
                ? $"{b.BehaviorFqn}.{ri.Name}"
                : $"_ => {b.BehaviorFqn}.{ri.Name}";
            return $"{ctor}.RunIf({expr}){access}";
        }

        return $"{ctor}{access}";
    }

    /// <summary>Generates the per-stage system method body (static dispatch or chunked parallel iteration).</summary>
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
        var name = $"{b.SafeName}_{m.Stage}";

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
                      var __physics = world.Resource<Engine.PhysicsWorld>();
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
                                  var ctx = new Engine.BehaviorContext(world, ecs, __cmd, __time, __input, __physics);
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
                          var ctx = new Engine.BehaviorContext(world, ecs, __cmd, __time, __input, __physics);
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
        return lines.Count == 0 ? "" : string.Join("\n", lines) + "\n";
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
        return lines.Count == 0 ? "" : string.Join("\n", lines) + "\n";
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
                      {{calls}}    
                  }
              }

              """;
    }

    // -- Intermediate representation --

    /// <summary>Scheduling stage for generated system registration.</summary>
    private enum Stage
    {
        Startup,
        First,
        PreUpdate,
        Update,
        PostUpdate,
        Render,
        Last,
        Cleanup
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