namespace StageManager.Services;

internal sealed class OverlayBadgePressOwnership
{
    internal bool OwnsButtonUp { get; private set; }

    internal bool CloseEligible { get; private set; }

    internal void BeginBadgePress()
    {
        OwnsButtonUp = true;
        CloseEligible = true;
    }

    internal void CancelCloseEligibility()
    {
        CloseEligible = false;
    }

    internal bool CompleteButtonUp()
    {
        if (!OwnsButtonUp)
        {
            return false;
        }

        OwnsButtonUp = false;
        CloseEligible = false;
        return true;
    }

    internal void Reset()
    {
        OwnsButtonUp = false;
        CloseEligible = false;
    }
}
