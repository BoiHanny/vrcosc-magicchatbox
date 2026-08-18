using System;
using System.IO;
using NLog.Config;
using Xunit;

namespace MagicChatbox.Tests.Services;

/// <summary>
/// The shipped NLog.config, parsed rather than assumed.
/// </summary>
/// <remarks>
/// The file sets throwConfigExceptions, so a single attribute NLog does not recognise is not a
/// degraded log - it is an exception during startup and an app that will not open. NLog 6 dropped
/// concurrentWrites from FileTarget, and nothing in a build or a normal test run reads this file, so
/// the first thing to notice was a user who could not start the app.
/// </remarks>
public class NLogConfigurationTests
{
    [Fact]
    public void The_shipped_configuration_loads()
    {
        string path = ConfigPath();
        Assert.True(File.Exists(path), "NLog.config not found at " + path);

        var exception = Record.Exception(() =>
        {
            var config = new XmlLoggingConfiguration(path);
            Assert.NotEmpty(config.AllTargets);
            Assert.NotEmpty(config.LoggingRules);
        });

        Assert.True(exception == null, "NLog.config does not load, so the app would not start: " + exception);
    }

    [Fact]
    public void Both_named_targets_the_rules_write_to_exist()
    {
        var config = new XmlLoggingConfiguration(ConfigPath());

        foreach (LoggingRule rule in config.LoggingRules)
        {
            foreach (NLog.Targets.Target target in rule.Targets)
                Assert.NotNull(config.FindTargetByName(target.Name));
        }
    }

    private static string ConfigPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "vrcosc-magicchatbox", "NLog.config");
    }
}
