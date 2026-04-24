using _3DEngine.Server;
using BlazorBlueprint.Components;
using Editor.Server;
using Editor.Server.Hubs;
using Editor.Shell;

// ── Standalone mode: compile scripts if a shells directory exists ──
var shellRegistry = new ShellRegistry();
ShellCompiler? compiler = null;

// Look for a source/shells/ directory next to the executable
var scriptsDir = Path.Combine(AppContext.BaseDirectory, "source", "shells");
if (!Directory.Exists(scriptsDir))
{
    // Also try relative to project directory (for `dotnet run`)
    scriptsDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "Editor", "Shells");
}

if (Directory.Exists(scriptsDir))
{
    compiler = new ShellCompiler(shellRegistry).WatchDirectory(scriptsDir);
    var compileResult = compiler.Start();
    Console.WriteLine($"[Editor.Server] Script compilation: {compileResult.Message}");
    foreach (var err in compileResult.Errors)
        Console.WriteLine($"[Editor.Server]   ERROR {err.FileName}({err.Line},{err.Column}): {err.Message}");

    compiler.CompilationCompleted += result =>
    {
        Console.WriteLine($"[Editor.Server] Hot-reload: {result.Message}");
        foreach (var err in result.Errors)
            Console.WriteLine($"[Editor.Server]   ERROR {err.FileName}({err.Line},{err.Column}): {err.Message}");
    };
}

// When run standalone, start the server and block until shutdown.
var server = await EditorServerHost.StartAsync(args: args, registry: shellRegistry);
await server.WaitForShutdownAsync();
compiler?.Dispose();

namespace Editor.Server
{
    /// <summary>
    /// Configures and starts the Editor.Server Blazor application.
    /// Can be hosted in-process from the Editor or run standalone.
    /// </summary>
    public static class EditorServerHost
    {
        /// <summary>
        /// Builds and starts the Blazor Server on the given URL without blocking.
        /// Returns the running <see cref="WebApplication"/> so the caller can stop it later.
        /// </summary>
        /// <param name="url">Listen URL for the Blazor Server.</param>
        /// <param name="args">Command-line args forwarded to the web host.</param>
        /// <param name="registry">
        /// Optional externally-owned <see cref="ShellRegistry"/>. When null a new instance is created.
        /// Pass the same instance from the Editor host to share state.
        /// </param>
        /// <returns>The running <see cref="WebApplication"/> that the caller can await or stop.</returns>
        /// <seealso cref="ShellRegistry"/>
        /// <seealso cref="EditorHub"/>
        public static async Task<WebApplication> StartAsync(
            string url = "http://localhost:5000",
            string[]? args = null,
            ShellRegistry? registry = null)
        {
            // ApplicationName must point to this assembly so the static web assets
            // pipeline (wwwroot, _content/, CSS) resolves correctly even when
            // hosted in-process from the Editor executable.
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args ?? [],
                ApplicationName = typeof(EditorServerHost).Assembly.GetName().Name!,
            });
            builder.WebHost.UseUrls(url);

            // Always enable static web assets resolution so CSS/JS from
            // Editor.Server/wwwroot and NuGet packages (_content/) are found,
            // even when the environment isn't Development (e.g., hosted from Editor).
            builder.WebHost.UseStaticWebAssets();

            // ── Editor Shell services ────────────────────────────────────
            var shellRegistry = registry ?? new ShellRegistry();
            builder.Services.AddSingleton(shellRegistry);
            builder.Services.AddSingleton<EditorState>();

            // Add services to the container
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // Add BlazorBlueprint services
            builder.Services.AddBlazorBlueprintComponents();

            // SignalR for engine ↔ shell communication
            builder.Services.AddSignalR();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // Map the editor SignalR hub
            app.MapHub<EditorHub>("/editor-hub");

            await app.StartAsync();
            return app;
        }
    }
}
