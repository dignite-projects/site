using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// The resource-based requirements the file library authorizes a <c>FileDescriptor</c> or a
/// <c>DirectoryDescriptor</c> against (<see cref="FileDescriptorAuthorizationHandler"/>,
/// <see cref="DirectoryDescriptorAuthorizationHandler"/>).
/// </summary>
public static class FileOperations
{
    public static readonly OperationAuthorizationRequirement Create = new() { Name = nameof(Create) };

    public static readonly OperationAuthorizationRequirement Update = new() { Name = nameof(Update) };

    public static readonly OperationAuthorizationRequirement Delete = new() { Name = nameof(Delete) };

    public static readonly OperationAuthorizationRequirement Get = new() { Name = nameof(Get) };
}
