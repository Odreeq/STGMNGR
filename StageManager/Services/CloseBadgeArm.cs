namespace StageManager.Services;

internal sealed class CloseBadgeArm
{
    internal static readonly TimeSpan HoverDelay = TimeSpan.FromMilliseconds(250);

    private bool _isHovering;
    private bool _isRevealed;
    private bool _isVisualPresented;
    private bool _pressBeganArmed;

    internal bool IsRevealed => _isRevealed;

    internal bool CanBeginPress => _isHovering && _isRevealed && _isVisualPresented;

    internal void Enter()
    {
        _isHovering = true;
    }

    internal void Reveal()
    {
        if (_isHovering)
        {
            _isRevealed = true;
        }
    }

    internal void ConfirmVisualPresented()
    {
        if (_isHovering && _isRevealed)
        {
            _isVisualPresented = true;
        }
    }

    internal void Leave()
    {
        _isHovering = false;
        _isRevealed = false;
        _isVisualPresented = false;
        _pressBeganArmed = false;
    }

    internal bool BeginPress()
    {
        if (!CanBeginPress)
        {
            return false;
        }

        _pressBeganArmed = true;
        return true;
    }

    internal void CancelPress()
    {
        _pressBeganArmed = false;
    }

    internal bool TryRelease()
    {
        if (!_pressBeganArmed || !CanBeginPress)
        {
            _pressBeganArmed = false;
            return false;
        }

        Leave();
        return true;
    }
}
