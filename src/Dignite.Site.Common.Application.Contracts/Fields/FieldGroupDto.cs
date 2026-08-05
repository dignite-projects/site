using System;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Fields;

/// <summary>
/// An organizational grouping over the field library. <see cref="EntityDto{Guid}"/>, not the full-audited
/// base - it mirrors <c>FieldGroup</c>'s own minimal <c>BasicAggregateRoot</c> shape, which carries no
/// audit trail (总体设计 §2.3).
/// </summary>
public class FieldGroupDto : EntityDto<Guid>
{
    public string Name { get; set; } = default!;

    public int Order { get; set; }
}
