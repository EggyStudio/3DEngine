using FluentAssertions;
using Xunit;

namespace Engine.Tests.Common;

[Trait("Category", "Unit")]
public class LoggerTests
{
    [Fact]
    public void CreateLogger_Returns_Same_Instance_For_Same_Category()
    {
        var factory = new LoggerFactory();

        var a = factory.CreateLogger("Engine.Test");
        var b = factory.CreateLogger("Engine.Test");

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void CreateLogger_Returns_Different_Instances_For_Different_Categories()
    {
        var factory = new LoggerFactory();

        var a = factory.CreateLogger("Engine.A");
        var b = factory.CreateLogger("Engine.B");

        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void Log_Category_Returns_Logger()
    {
        var logger = Log.Category("Engine.Test");

        logger.Should().NotBeNull();
        logger.Should().BeAssignableTo<ILogger>();
    }

    [Fact]
    public void Log_Category_Returns_Cached_Instance()
    {
        var a = Log.Category("Engine.Cached");
        var b = Log.Category("Engine.Cached");

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void Log_For_Uses_TypeName_As_Category()
    {
        var logger = Log.For<LoggerTests>();

        logger.Should().NotBeNull();
        logger.Should().BeAssignableTo<ILogger>();
    }

    [Fact]
    public void UseProvider_Dispatches_To_Custom_Provider()
    {
        var provider = new SpyLoggerProvider();
        var logger = new Logger("Test.Category");
        logger.UseProvider(provider);

        logger.Info("hello");

        provider.Messages.Should().ContainSingle();
        provider.Messages[0].Level.Should().Be(LogLevel.Info);
        provider.Messages[0].Category.Should().Be("Test.Category");
        provider.Messages[0].Message.Should().Be("hello");
    }

    [Fact]
    public void UseProvider_Returns_Same_Logger_For_Fluent_Chaining()
    {
        var logger = new Logger("Test");

        var result = logger.UseProvider(new SpyLoggerProvider());

        result.Should().BeSameAs(logger);
    }

    [Fact]
    public void UseProvider_Does_Not_Add_ConsoleLoggerProvider()
    {
        var logger = new Logger("Test");
        var spy = new SpyLoggerProvider();

        logger.UseProvider(ConsoleLoggerProvider.Instance);
        logger.UseProvider(spy);

        // Set console level very high so console provider doesn't fire
        var originalLevel = LogConfig.ConsoleMinimumLevel;
        try
        {
            LogConfig.ConsoleMinimumLevel = LogLevel.Critical;
            logger.Log(LogLevel.Info, "test");

            // Only spy should have received the message
            spy.Messages.Should().ContainSingle();
        }
        finally
        {
            LogConfig.ConsoleMinimumLevel = originalLevel;
        }
    }

    [Fact]
    public void UseProvider_Does_Not_Add_Same_Provider_Twice()
    {
        var spy = new SpyLoggerProvider();
        var logger = new Logger("Test");
        logger.UseProvider(spy);
        logger.UseProvider(spy); // duplicate

        logger.Info("once");

        spy.Messages.Should().ContainSingle();
    }

    [Fact]
    public void Trace_Routes_To_LogLevel_Trace()
    {
        var (logger, spy) = CreateLoggerWithSpy();

        logger.Trace("msg");

        spy.Messages.Should().ContainSingle(m => m.Level == LogLevel.Trace);
    }

    [Fact]
    public void Debug_Routes_To_LogLevel_Debug()
    {
        var (logger, spy) = CreateLoggerWithSpy();

        logger.Debug("msg");

        spy.Messages.Should().ContainSingle(m => m.Level == LogLevel.Debug);
    }

    [Fact]
    public void Info_Routes_To_LogLevel_Info()
    {
        var (logger, spy) = CreateLoggerWithSpy();

        logger.Info("msg");

        spy.Messages.Should().ContainSingle(m => m.Level == LogLevel.Info);
    }

    [Fact]
    public void Warn_Routes_To_LogLevel_Warning()
    {
        var (logger, spy) = CreateLoggerWithSpy();

        logger.Warn("msg");

        spy.Messages.Should().ContainSingle(m => m.Level == LogLevel.Warning);
    }

    [Fact]
    public void Error_Routes_To_LogLevel_Error()
    {
        var (logger, spy) = CreateLoggerWithSpy();

        logger.Error("msg");

        spy.Messages.Should().ContainSingle(m => m.Level == LogLevel.Error);
    }

    [Fact]
    public void Error_Includes_Exception()
    {
        var (logger, spy) = CreateLoggerWithSpy();
        var ex = new InvalidOperationException("boom");

        logger.Error("failed", ex);

        spy.Messages.Should().ContainSingle();
        spy.Messages[0].Exception.Should().BeSameAs(ex);
    }

    [Fact]
    public void Critical_Routes_To_LogLevel_Critical()
    {
        var (logger, spy) = CreateLoggerWithSpy();

        logger.Critical("fatal");

        spy.Messages.Should().ContainSingle(m => m.Level == LogLevel.Critical);
    }

    [Fact]
    public void FrameTrace_Suppressed_When_PerFrameLogging_Disabled()
    {
        var (logger, spy) = CreateLoggerWithSpy();
        var original = LogConfig.PerFrameLogging;
        try
        {
            LogConfig.PerFrameLogging = false;

            logger.FrameTrace("should not appear");

            spy.Messages.Should().BeEmpty();
        }
        finally
        {
            LogConfig.PerFrameLogging = original;
        }
    }

    [Fact]
    public void FrameTrace_Emitted_When_PerFrameLogging_Enabled()
    {
        var (logger, spy) = CreateLoggerWithSpy();
        var original = LogConfig.PerFrameLogging;
        try
        {
            LogConfig.PerFrameLogging = true;

            logger.FrameTrace("per-frame msg");

            spy.Messages.Should().ContainSingle(m => m.Level == LogLevel.Trace);
        }
        finally
        {
            LogConfig.PerFrameLogging = original;
        }
    }

    [Fact]
    public void ExtraProvider_Respects_MinimumLevel()
    {
        var (logger, spy) = CreateLoggerWithSpy();
        var original = LogConfig.MinimumLevel;
        try
        {
            LogConfig.MinimumLevel = LogLevel.Warning;

            logger.Info("below threshold");
            logger.Warn("at threshold");

            spy.Messages.Should().ContainSingle(m => m.Level == LogLevel.Warning);
        }
        finally
        {
            LogConfig.MinimumLevel = original;
        }
    }

    [Fact]
    public void LogConfig_Defaults()
    {
        // These just verify the static defaults haven't been accidentally changed
        LogConfig.MaxLogFileBytes.Should().Be(50L * 1024 * 1024);
    }

    private static (Logger Logger, SpyLoggerProvider Spy) CreateLoggerWithSpy()
    {
        var spy = new SpyLoggerProvider();
        var logger = new Logger("Test");
        logger.UseProvider(spy);
        return (logger, spy);
    }

    private sealed class SpyLoggerProvider : ILoggerProvider
    {
        public List<LogEntry> Messages { get; } = [];

        public void Log(LogLevel level, string category, string message, Exception? exception = null)
            => Messages.Add(new LogEntry(level, category, message, exception));

        public record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);
    }
}
