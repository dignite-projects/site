namespace Dignite.Site.Admin.Files;

/// <summary>Where an upload link lives, in one place for the service that issues it and the controller that serves it.</summary>
public static class FileUploadTicketPaths
{
    /// <summary>The route of the redeeming endpoint, below <c>api/site-admin/files</c>.</summary>
    public const string RouteTemplate = "upload/{ticket}";

    /// <summary>The path of the link for <paramref name="protectedTicket"/>, relative to the host: no leading slash.</summary>
    public static string Build(string protectedTicket)
    {
        return "api/site-admin/files/upload/" + protectedTicket;
    }
}
