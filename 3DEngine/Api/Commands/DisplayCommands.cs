using System.Text;

namespace Engine;

/// <summary>The console command that lists the monitors and the modes each can be set to.</summary>
internal static class DisplayCommands
{
    [Command("monitors", "Each monitor, its current mode, and the modes it can be set to in fullscreen")]
    internal static string Monitors()
    {
        var text = new StringBuilder();
        for (int i = 0; i < E3D.GetMonitorCount(); i++)
        {
            text.AppendLine($"{i} {E3D.GetMonitorName(i)}: {E3D.GetMonitorWidth(i)}x{E3D.GetMonitorHeight(i)} at {E3D.GetMonitorRefreshRate(i)} Hz");
            text.AppendLine("  " + string.Join(", ", E3D.GetMonitorModes(i).Select(m => $"{m.Width}x{m.Height}@{m.RefreshRate}")));
        }
        return text.Length == 0 ? "no monitors" : text.ToString().TrimEnd();
    }
}
