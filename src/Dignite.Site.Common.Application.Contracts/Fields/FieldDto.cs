using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Fields;

/// <summary>
/// A field library definition, read-shaped. <c>Configuration</c> is a plain, JSON-friendly dictionary
/// rather than the kernel's <c>FieldConfigurationDictionary</c> - the contract must not leak that type.
/// </summary>
public class FieldDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? Description { get; set; }

    public string FieldTypeName { get; set; } = default!;

    public IDictionary<string, object?> Configuration { get; set; } = new Dictionary<string, object?>();

    public string? GroupName { get; set; }
}
