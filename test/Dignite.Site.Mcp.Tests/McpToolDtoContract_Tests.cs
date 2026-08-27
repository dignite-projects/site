using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Dignite.Site.Mcp;

/// <summary>
/// Pins the property-level shape of every Create/Update/List DTO an MCP tool constructs against the tool
/// method's own parameters, using <see cref="McpToolContracts"/> as the record of what is supposed to be
/// there. The drift this guards against is exactly what an MCP tool is supposed to be (总体设计 §6.2.2):
/// another caller of the AppService, not another implementation - and that includes not quietly falling
/// behind it as the AppService's DTOs evolve.
/// <para>
/// Motivated by a real bug: <c>update_page</c> (and <c>create_page</c>) silently never had a
/// <c>template</c>/<c>contentTemplate</c> parameter at all, even though <c>UpdatePageDto</c>/
/// <c>CreatePageDto</c> had carried both fields from the start - nothing failed, nothing warned, the
/// fields were just permanently unreachable from MCP. This test would have failed the moment those fields
/// were added to <see cref="McpToolContracts"/> without a matching parameter, which is the point: a
/// contract that must be updated by hand, on purpose, every time a covered DTO's shape changes.
/// </para>
/// <para>
/// This checks structure, not wiring: it confirms a same-named (or explicitly declared) parameter exists
/// with a compatible type, not that the tool method actually assigns it to the right property. That is
/// enough to have caught the bug above on its own, but a tool that has the right parameter and wires it to
/// the wrong place - or ignores it and keeps echoing the current value - would still pass here. Catching
/// that class needs a round-trip integration test per parameter (set it to a new value, assert it actually
/// changed) - <c>test/Dignite.Site.EntityFrameworkCore.Tests/Mcp/*Tools_Tests.cs</c> already does exactly
/// that for the other fields on these same tools (e.g. <c>PageTools_Tests.Should_Reparent_A_Page_By_Name</c>),
/// and now for Template/ContentTemplate too. The two layers are complementary, not redundant: this one is
/// free to add (declare the property, done) and catches a missing parameter immediately; that one is
/// precise about wiring but has to be written by hand per field, so it lags until someone gets to it.
/// </para>
/// </summary>
public class McpToolDtoContract_Tests
{
    public static IEnumerable<object[]> ContractLabels =>
        McpToolContracts.All.Select(contract => new object[] { contract.ToString() });

    [Theory]
    [MemberData(nameof(ContractLabels))]
    public void Every_Dto_Property_Is_Accounted_For(string label)
    {
        var contract = McpToolContracts.All.Single(c => c.ToString() == label);

        var method = contract.ToolsType.GetMethod(contract.MethodName);
        method.ShouldNotBeNull($"{contract} - method not found; the contract is stale, update or remove it.");

        var parameters = method!.GetParameters().ToDictionary(p => p.Name!, p => p);

        var dtoProperties = contract.DtoType.GetProperties()
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name);

        var declared = contract.Properties.ToDictionary(p => p.Property);

        // The API grew a field this contract has never seen - the exact shape of the bug this file exists
        // to catch. Failing here forces a human to categorize it instead of letting it stay unreachable.
        var undeclared = dtoProperties.Keys.Except(declared.Keys).ToList();
        undeclared.ShouldBeEmpty(
            $"{contract}: {contract.DtoType.Name} has propert{(undeclared.Count == 1 ? "y" : "ies")} " +
            $"[{string.Join(", ", undeclared)}] that {nameof(McpToolContracts)} does not know about - the " +
            "API grew a field this contract was never updated for. Either wire it into the tool method and " +
            "declare it Direct/Translated, or declare it Omitted with a reason.");

        // The reverse: a property this contract still describes was renamed or removed on the API side.
        var stale = declared.Keys.Except(dtoProperties.Keys).ToList();
        stale.ShouldBeEmpty(
            $"{contract}: {nameof(McpToolContracts)} declares propert{(stale.Count == 1 ? "y" : "ies")} " +
            $"[{string.Join(", ", stale)}] that no longer exist{(stale.Count == 1 ? "s" : "")} on " +
            $"{contract.DtoType.Name} - it was renamed or removed on the API side. Update the contract to match.");

        foreach (var entry in contract.Properties)
        {
            var property = dtoProperties[entry.Property];
            var actualType = Unwrap(property.PropertyType);
            var expectedType = Unwrap(entry.ExpectedType);

            actualType.ShouldBe(expectedType,
                $"{contract}: {contract.DtoType.Name}.{entry.Property} is now {property.PropertyType}, but " +
                $"{nameof(McpToolContracts)} still expects {entry.ExpectedType} - the API changed this " +
                "property's type. Update the tool parameter (if Direct) and this contract to match.");

            if (entry.Mapping == McpPropertyMapping.Omitted)
            {
                entry.Reason.ShouldNotBeNullOrWhiteSpace(
                    $"{contract}: {contract.DtoType.Name}.{entry.Property} is Omitted with no reason.");
                continue;
            }

            parameters.ShouldContainKey(entry.Parameter!,
                $"{contract}: no parameter named '{entry.Parameter}' for {contract.DtoType.Name}." +
                $"{entry.Property} - it was renamed or removed on the MCP side.");

            if (entry.Mapping == McpPropertyMapping.Direct)
            {
                var parameterType = Unwrap(parameters[entry.Parameter!].ParameterType);
                actualType.IsAssignableFrom(parameterType).ShouldBeTrue(
                    $"{contract}: parameter '{entry.Parameter}' is {parameters[entry.Parameter!].ParameterType}, " +
                    $"which is no longer assignable to {contract.DtoType.Name}.{entry.Property}'s " +
                    $"{property.PropertyType}.");
            }
        }
    }

    private static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;
}
