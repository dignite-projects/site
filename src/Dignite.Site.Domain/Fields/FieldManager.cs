using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.FlexFields.Site.Seo;
using Dignite.Site.Contents;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Fields;

/// <summary>
/// Creating, renaming and deleting field definitions.
/// <para>
/// Renaming and deleting are the reason this class exists. A field's <c>Name</c> is the key its values
/// are stored under in every content's value bag, so changing it is a data migration across the whole
/// content table, not an edit to one row (总体设计 §2.4). FlexFields supplies the migration itself -
/// <c>IFlexFieldValueMigrator&lt;Content&gt;</c>, one provider-neutral implementation, no code from
/// Site - but the <i>ordering</i> is the caller's to get right, and getting it wrong silently destroys
/// data. This class is where that ordering is written down once so no call site has to remember it.
/// </para>
/// </summary>
public class FieldManager : DomainService
{
    protected IFieldRepository FieldRepository { get; }

    protected IFlexFieldValueMigrator<Content> ValueMigrator { get; }

    protected IFlexFieldIndexManager<Content> IndexManager { get; }

    public FieldManager(
        IFieldRepository fieldRepository,
        IFlexFieldValueMigrator<Content> valueMigrator,
        IFlexFieldIndexManager<Content> indexManager)
    {
        FieldRepository = fieldRepository;
        ValueMigrator = valueMigrator;
        IndexManager = indexManager;
    }

    public virtual async Task<Field> CreateAsync(
        string name,
        string displayName,
        string fieldTypeName,
        string? description = null,
        FieldConfigurationDictionary? configuration = null,
        string? groupName = null,
        CancellationToken cancellationToken = default)
    {
        await CheckNameAsync(name, null, cancellationToken);

        var field = new Field(
            GuidGenerator.Create(),
            name,
            displayName,
            fieldTypeName,
            description,
            configuration,
            groupName,
            CurrentTenant.Id);

        return await FieldRepository.InsertAsync(field, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Updates everything about a field <i>except</i> its name - use <see cref="RenameAsync"/> for that.
    /// <para>
    /// Changing the field type triggers a full index rebuild: the type decides which typed column a
    /// value is indexed into, so leaving the old rows in place would leave them in the wrong slot, where
    /// queries against the new type will never look. Rebuilding is a complete fix here because the
    /// kernel re-derives every row from the raw bag value on each run.
    /// </para>
    /// </summary>
    public virtual async Task<Field> UpdateAsync(
        Field field,
        string displayName,
        string fieldTypeName,
        string? description = null,
        FieldConfigurationDictionary? configuration = null,
        string? groupName = null,
        CancellationToken cancellationToken = default)
    {
        var fieldTypeChanged = !string.Equals(field.FieldTypeName, fieldTypeName, StringComparison.Ordinal);

        field.SetDisplayName(displayName);
        field.SetFieldTypeName(fieldTypeName);
        field.SetDescription(description);
        field.SetConfiguration(configuration);
        field.SetGroupName(groupName);

        field = await FieldRepository.UpdateAsync(field, cancellationToken: cancellationToken);

        if (fieldTypeChanged)
        {
            await IndexManager.RebuildAsync(cancellationToken);
        }

        return field;
    }

    /// <summary>
    /// Renames a field definition and moves every content's stored value to the new key.
    /// <para>
    /// The order below is prescribed by <c>IFlexFieldValueMigrator</c> and is not interchangeable:
    /// </para>
    /// <list type="number">
    /// <item>rule out a collision, passing this field's own id so its current name does not count;</item>
    /// <item>change the definition's <c>Name</c>;</item>
    /// <item>rewrite the value bags - once per host type, and <c>Content</c> is Site' only one.</item>
    /// </list>
    /// <para>
    /// The trap is between steps 2 and 3: a content synchronized in that window resolves nothing for the
    /// field and has its index rows deleted. Keeping the whole sequence inside one unit of work is what
    /// closes the window - and note that no reindex is needed afterwards, because index rows key on the
    /// field's id and its value, neither of which a rename touches.
    /// </para>
    /// <para>
    /// Refuses a platform preset field outright - see <see cref="CheckNotPlatformPreset"/>.
    /// </para>
    /// </summary>
    /// <returns>How many contents had a value moved.</returns>
    public virtual async Task<int> RenameAsync(Field field, string newName, CancellationToken cancellationToken = default)
    {
        CheckNotPlatformPreset(field, isDelete: false);

        var oldName = field.Name;

        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            return 0;
        }

        await CheckNameAsync(newName, field.Id, cancellationToken);

        field.SetName(newName);
        await FieldRepository.UpdateAsync(field, cancellationToken: cancellationToken);

        return await ValueMigrator.RenameFieldAsync(oldName, newName, cancellationToken);
    }

    /// <summary>
    /// Deletes a field definition and strips its values out of every content's bag.
    /// <para>
    /// Values first, definition second. Reversed, the values would remain in every bag forever -
    /// unreachable, since nothing names them any more, but still serialized on every read of every
    /// content.
    /// </para>
    /// <para>
    /// This does not unhook the field from the content types that reference it; that stays the caller's
    /// decision, because dropping a field from a type is a change to that type's shape and should be
    /// visible as one.
    /// </para>
    /// <para>
    /// Refuses a platform preset field outright - see <see cref="CheckNotPlatformPreset"/>.
    /// </para>
    /// </summary>
    public virtual async Task DeleteAsync(Field field, CancellationToken cancellationToken = default)
    {
        CheckNotPlatformPreset(field, isDelete: true);

        await ValueMigrator.RemoveFieldAsync(field.Name, cancellationToken);
        await FieldRepository.DeleteAsync(field, cancellationToken: cancellationToken);
    }

    protected virtual async Task CheckNameAsync(string name, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (await FieldRepository.NameExistsAsync(name, excludedId, cancellationToken))
        {
            throw new FieldNameAlreadyExistException(name);
        }
    }

    /// <summary>
    /// A platform preset field - currently just <see cref="SeoFieldNames.FieldName"/> - has nothing else
    /// stable to be recognized by: each tenant's row gets an ordinary, randomly generated <c>Id</c> (a
    /// literal shared <c>Guid</c> cannot exist once per tenant under a single-column primary key on a
    /// shared table), so its <c>Name</c> is the only cross-tenant "well-known" anchor. Deleting or renaming
    /// it would silently disable whatever platform behavior is keyed on that name, so both are refused
    /// here rather than left to be discovered later. Everything else about the field - DisplayName,
    /// FieldTypeName, Configuration, GroupName - stays freely editable through <see cref="UpdateAsync"/>.
    /// </summary>
    protected virtual void CheckNotPlatformPreset(Field field, bool isDelete)
    {
        if (!string.Equals(field.Name, SeoFieldNames.FieldName, StringComparison.Ordinal))
        {
            return;
        }

        if (isDelete)
        {
            throw new FieldIsPlatformPresetCannotBeDeletedException(field.Id, field.Name);
        }

        throw new FieldIsPlatformPresetCannotBeRenamedException(field.Id, field.Name);
    }
}
