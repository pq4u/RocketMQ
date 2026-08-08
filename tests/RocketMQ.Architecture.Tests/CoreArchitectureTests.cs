using NetArchTest.Rules;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using TestResult = NetArchTest.Rules.TestResult;

namespace RocketMQ.Architecture.Tests;

// <summary>
/// Turns the "Core must not depend on a specific adapter" rule.
/// This project intentionally does NOT reference any Adapters.* project —
/// it only reflects over the Core assembly, so it stays valid even after
/// gRPC is replaced by Pipelines and SQLite by the custom WAL manager.
/// </summary>
public class CoreArchitectureTests
{
    private static readonly System.Reflection.Assembly _coreAssembly =
        typeof(InboundMessage).Assembly;

    [Theory]
    [InlineData("Grpc")]
    [InlineData("Google.Protobuf")]
    [InlineData("Microsoft.Data.Sqlite")]
    [InlineData("System.IO.Pipelines")]
    [InlineData("System.Data.Sqlite")]
    public void Core_Should_Not_Depend_On_Any_Adapter_Technology(string forbiddenNamespacePrefix)
    {
        var result = Types.InAssembly(_coreAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespacePrefix)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result, forbiddenNamespacePrefix));
    }

    [Fact]
    public void Abstractions_Should_Be_Interfaces()
    {
        // Guards against someone "temporarily" adding a concrete base class
        // to Core.Abstractions to save time — that's exactly the kind of
        // shortcut that quietly reintroduces coupling.
        var result = Types.InAssembly(_coreAssembly)
            .That()
            .ResideInNamespace("RocketMQ.Core.Abstractions")
            .Should()
            .BeInterfaces()
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result, "Core.Abstractions must only contain interfaces"));
    }

    [Fact]
    public void Domain_Types_Should_Be_Sealed()
    {
        // Not strictly an architecture rule about layering, but cheap to
        // enforce here and catches "someone made InboundMessage extensible
        // instead of adding a new type" early.
        var result = Types.InAssembly(_coreAssembly)
            .That()
            .ResideInNamespace("RocketMQ.Core.Models")
            .And()
            .AreClasses()
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result, "Core.Models classes must be sealed"));
    }

    private static string FormatFailures(TestResult result, string ruleDescription)
    {
        var offenders = result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>();
        return $"Rule violated: {ruleDescription}. Offending types:\n{string.Join("\n", offenders)}";
    }
}
