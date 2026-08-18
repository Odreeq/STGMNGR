namespace StageManager.Services;

internal enum PreviewPointerAction
{
    None,
    Activate,
    Close,
    TogglePin
}

internal static class WindowPreviewGesture
{
    internal static PreviewPointerAction Resolve(
        bool isLeftButton,
        bool controlPressed,
        bool altPressed)
    {
        if (!isLeftButton)
        {
            return PreviewPointerAction.None;
        }

        if (controlPressed)
        {
            return PreviewPointerAction.Close;
        }

        return altPressed
            ? PreviewPointerAction.TogglePin
            : PreviewPointerAction.Activate;
    }

    internal static bool Execute(
        PreviewPointerAction action,
        Action activate,
        Action close,
        Action togglePin)
    {
        switch (action)
        {
            case PreviewPointerAction.Activate:
                activate();
                return false;
            case PreviewPointerAction.Close:
                close();
                return true;
            case PreviewPointerAction.TogglePin:
                togglePin();
                return true;
            default:
                return false;
        }
    }
}
