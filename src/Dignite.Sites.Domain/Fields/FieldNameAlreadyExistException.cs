using Volo.Abp;

namespace Dignite.Sites.Fields;

/// <summary>
/// A field's name is unique per tenant - it has to be, since it doubles as the key its values are stored
/// under in every content's value bag.
/// </summary>
public class FieldNameAlreadyExistException : BusinessException
{
    public FieldNameAlreadyExistException(string name)
        : base(SitesErrorCodes.FieldNameAlreadyExists)
    {
        WithData("Name", name);
    }
}
