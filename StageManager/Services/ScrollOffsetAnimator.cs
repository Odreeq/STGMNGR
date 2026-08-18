using System.Windows.Controls;

namespace StageManager.Services;

internal sealed class ScrollOffsetAnimator
{
    private readonly ScrollViewer _scrollViewer;

    public ScrollOffsetAnimator(ScrollViewer scrollViewer)
    {
        _scrollViewer = scrollViewer;
    }

    public void AnimateTo(double target) => _scrollViewer.ScrollToVerticalOffset(target);
}
