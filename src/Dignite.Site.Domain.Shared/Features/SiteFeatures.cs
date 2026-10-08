namespace Dignite.Site.Features;

public static class SiteFeatures
{
    public const string GroupName = "Site";

    /// <summary>
    /// Whether a tenant may use Site's <b>management surface</b> - the Admin application services, and with
    /// them the MCP tools, which are thin callers of those services. A host such as Dignite.Cloud turns this
    /// off for tenants whose edition does not include Site.
    /// <para>
    /// <b>On by default</b> (see <c>SiteFeatureDefinitionProvider</c>): a host that does not run the Feature
    /// Management module has nowhere to switch it on, and would otherwise lose the whole admin surface. A
    /// host that wants Site to be opt-in overrides the default to <c>false</c> in its own
    /// <c>FeatureDefinitionProvider</c> and grants it per edition or tenant.
    /// </para>
    /// <para>
    /// Deliberately <b>not</b> enforced on the Public application services, and it must stay that way: those
    /// answer anonymous requests, and ABP resolves the Edition level of a feature from the signed-in
    /// user's <c>editionid</c> claim, which an anonymous request does not carry. An edition-granted
    /// feature would read as "off" for every visitor and take the published site down. Whether a published
    /// site goes offline when a tenant loses the feature is a separate product decision.
    /// </para>
    /// </summary>
    public const string Enable = GroupName + ".Enable";
}
