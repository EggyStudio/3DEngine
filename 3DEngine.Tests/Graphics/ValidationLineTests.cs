using System.Text.RegularExpressions;
using FluentAssertions;
using Vortice.Vulkan;

namespace Engine.Tests.Graphics;

// The line the engine logs for a message of the validation layer, held against the checks that read
// a run's log for the layer's errors after a game, an example, a storm or a native run, each finding
// a message of error severity as the pinned layer writes it and none of another severity.
public sealed partial class ValidationLineTests
{
    // An error as the layer build/fetch-validation-layer.sh fetches writes it, and a warning as the
    // loader writes one through the layer's messenger.
    private const string PinnedError = "vkCreateBuffer(): pCreateInfo->size is zero.\nThe Vulkan spec states: size must be greater than 0 "
        + "(https://vulkan.lunarg.com/doc/view/1.4.363.0/linux/antora/spec/latest/chapters/resources.html#VUID-VkBufferCreateInfo-size-00912)";
    private const string PinnedWarning = "terminator_CreateInstance: Received return code -9 from call to vkCreateInstance in ICD /usr/lib64/libvulkan_dzn.so. Skipping this driver.";

    // An error whose message begins with the words a check looks for.
    private const string OlderError = "Validation Error: [ VUID-VkBufferCreateInfo-size-00912 ] | MessageID = 0x7c54445e | vkCreateBuffer(): pCreateInfo->size is zero.";

    // The words a grep of a fixed string looks for, as the scripts and the workflows write them.
    [GeneratedRegex(@"grep -c?F '([^']*)'")]
    private static partial Regex Grepped();

    // Each check of a run's log for the layer's errors, where it stands and the words its greps on
    // the line look for, each of which a line it finds holds.
    private static List<(string Where, string[] Words)> Checks()
    {
        var root = Api.CheatsheetTests.RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "build"), "*.sh", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, ".github", "workflows"), "*.yml"));
        var checks = new List<(string Where, string[] Words)>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var words = Grepped().Matches(lines[i]).Select(match => match.Groups[1].Value).ToArray();
                if (words.Any(word => word.Contains("Validation", StringComparison.Ordinal)))
                    checks.Add(($"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{i + 1}", words));
            }
        }
        return checks;
    }

    // A message of the layer as the console and the log file write it.
    private static string Logged(VkDebugUtilsMessageTypeFlagsEXT type, string message, VkDebugUtilsMessageSeverityFlagsEXT severity) => ConsoleLoggerProvider.Line(
        1, severity == VkDebugUtilsMessageSeverityFlagsEXT.Error ? LogLevel.Error : LogLevel.Warning, GraphicsDevice.ValidationCategory,
        GraphicsDevice.ValidationLine(type, message, severity));

    [Fact]
    public void Every_Check_Of_A_Runs_Log_Finds_An_Error_As_The_Pinned_Layer_Writes_It_And_No_Warning()
    {
        var checks = Checks();
        checks.Select(check => check.Where).Should().Contain(where => where.StartsWith(".github/workflows/build.yml:", StringComparison.Ordinal))
            .And.Contain(where => where.StartsWith("build/storm.sh:", StringComparison.Ordinal), "the workflow and the scripts read a run's log for the layer's errors");
        var error = Logged(VkDebugUtilsMessageTypeFlagsEXT.Validation, PinnedError, VkDebugUtilsMessageSeverityFlagsEXT.Error);
        var warning = Logged(VkDebugUtilsMessageTypeFlagsEXT.General, PinnedWarning, VkDebugUtilsMessageSeverityFlagsEXT.Warning);
        foreach (var (where, words) in checks)
        {
            words.Should().OnlyContain(word => error.Contains(word, StringComparison.Ordinal), $"the check at {where} finds the line {error}");
            words.Should().Contain(word => !warning.Contains(word, StringComparison.Ordinal), $"the check at {where} passes over the line {warning}");
        }
    }

    [Fact]
    public void An_Error_Carries_The_Words_A_Check_Looks_For_Once_Whether_The_Layer_Wrote_Them_Or_Not()
    {
        GraphicsDevice.ValidationLine(VkDebugUtilsMessageTypeFlagsEXT.Validation, OlderError, VkDebugUtilsMessageSeverityFlagsEXT.Error)
            .Should().Be($"[Validation] {OlderError}", "a message that begins with the words keeps them as it is");
        GraphicsDevice.ValidationLine(VkDebugUtilsMessageTypeFlagsEXT.Validation, PinnedError, VkDebugUtilsMessageSeverityFlagsEXT.Error)
            .Should().Be($"[Validation] {GraphicsDevice.ErrorWords}: {PinnedError}", "a message that does not gets them before it");
    }

    [Fact]
    public void A_Message_Of_Another_Severity_Keeps_Its_Own_Words_Though_A_Pools_Overrun_Is_Counted_With_The_Errors()
    {
        // A warning of the loader's, a performance warning and a set asked of a pool past its counts,
        // which the layer gives as a warning and the engine counts with the errors, each logged as the
        // layer wrote it, so the checks of a run's log fail on the layer's errors alone.
        const string Overrun = "vkAllocateDescriptorSets(): pAllocateInfo->descriptorPool has 0 descriptorSets remaining.";
        GraphicsDevice.ValidationLine(VkDebugUtilsMessageTypeFlagsEXT.General, PinnedWarning, VkDebugUtilsMessageSeverityFlagsEXT.Warning)
            .Should().Be($"[General] {PinnedWarning}");
        GraphicsDevice.ValidationLine(VkDebugUtilsMessageTypeFlagsEXT.Performance, PinnedWarning, VkDebugUtilsMessageSeverityFlagsEXT.Warning)
            .Should().Be($"[Performance] {PinnedWarning}");
        GraphicsDevice.ValidationLine(VkDebugUtilsMessageTypeFlagsEXT.Validation, Overrun, VkDebugUtilsMessageSeverityFlagsEXT.Warning)
            .Should().Be($"[Validation] {Overrun}");
        GraphicsDevice.CountedAsError(VkDebugUtilsMessageSeverityFlagsEXT.Warning, "VUID-VkDescriptorSetAllocateInfo-apiVersion-07895")
            .Should().BeTrue("a pool's overrun fails a test wherever the layer runs");
        GraphicsDevice.CountedAsError(VkDebugUtilsMessageSeverityFlagsEXT.Warning, "WARNING-Other").Should().BeFalse();
        GraphicsDevice.CountedAsError(VkDebugUtilsMessageSeverityFlagsEXT.Error, null).Should().BeTrue();
    }
}
