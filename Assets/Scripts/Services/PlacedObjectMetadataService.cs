using UnityEngine;

public static class PlacedObjectMetadataService
{
    public static bool SetDisplayName(PlacedObject target, string value)
    {
        if (target == null) return false;
        string normalized = value?.Trim() ?? string.Empty;
        if (string.Equals(target.displayName, normalized, System.StringComparison.Ordinal)) return false;

        return Execute(new EditPlacedObjectMetadataCommand(
            target,
            normalized,
            target.description,
            target.hasDescriptionOverride));
    }

    public static bool SetDescription(PlacedObject target, string value)
    {
        if (target == null) return false;
        string normalized = value?.Trim() ?? string.Empty;
        if (target.hasDescriptionOverride && string.Equals(target.description ?? string.Empty, normalized, System.StringComparison.Ordinal))
            return false;

        return Execute(new EditPlacedObjectMetadataCommand(
            target,
            target.displayName,
            normalized,
            hasDescriptionOverride: true));
    }

    static bool Execute(IEditorCommand command)
    {
        var commandService = CommandService.I;
        if (commandService != null && commandService.Stack != null)
            return commandService.Stack.Execute(command);

        Debug.LogWarning("[PlacedObjectMetadataService] CommandService is missing. Applying metadata without undo.");
        return command.Do();
    }
}

sealed class EditPlacedObjectMetadataCommand : IEditorCommand
{
    readonly PlacedObject target;
    readonly string beforeDisplayName;
    readonly string beforeDescription;
    readonly bool beforeHasDescriptionOverride;
    readonly string afterDisplayName;
    readonly string afterDescription;
    readonly bool afterHasDescriptionOverride;

    public string Label => "Edit object details";

    public EditPlacedObjectMetadataCommand(
        PlacedObject target,
        string displayName,
        string description,
        bool hasDescriptionOverride)
    {
        this.target = target;
        beforeDisplayName = target.displayName;
        beforeDescription = target.description;
        beforeHasDescriptionOverride = target.hasDescriptionOverride;
        afterDisplayName = displayName;
        afterDescription = description;
        afterHasDescriptionOverride = hasDescriptionOverride;
    }

    public bool Do() => Apply(afterDisplayName, afterDescription, afterHasDescriptionOverride);
    public bool Undo() => Apply(beforeDisplayName, beforeDescription, beforeHasDescriptionOverride);

    bool Apply(string displayName, string description, bool hasDescriptionOverride)
    {
        if (target == null) return false;
        target.SetDisplayName(displayName);
        target.description = description;
        target.hasDescriptionOverride = hasDescriptionOverride;
        return true;
    }
}
