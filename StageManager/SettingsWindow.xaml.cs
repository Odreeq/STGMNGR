using System.Windows;
using System.Windows.Input;
using StageManager.Models;
using StageManager.Services;

namespace StageManager;

public partial class SettingsWindow : Window
{
    private bool _isReady;

    public SettingsWindow(StageManagerSettings settings)
    {
        InitializeComponent();
        FixedModeRadio.IsChecked = settings.DisplayMode is DisplayMode.Fixed;
        FloatingModeRadio.IsChecked = settings.DisplayMode is DisplayMode.Floating;
        WidthSlider.Value = settings.DisplayWidth;
        UpdateMaximumCardCount(settings.DisplayWidth);
        CardCountSlider.Value = Math.Min(settings.CardCount, CardCountSlider.Maximum);
        PreviewOpacitySlider.Value = settings.PreviewOpacity * 100;
        UpdateLabels();
        _isReady = true;
    }

    public event Action<StageManagerSettings>? SettingsChanged;

    private void ModeRadio_Checked(object sender, RoutedEventArgs e) => PublishChanges();

    private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized)
        {
            return;
        }

        UpdateMaximumCardCount(e.NewValue);
        UpdateLabels();
        PublishChanges();
    }

    private void CardCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized)
        {
            return;
        }

        UpdateLabels();
        PublishChanges();
    }

    private void PreviewOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized)
        {
            return;
        }

        UpdateLabels();
        PublishChanges();
    }

    private void UpdateMaximumCardCount(double width)
    {
        var maximum = LayoutMetrics.CalculateMaximumCardCount(width, SystemParameters.WorkArea.Height);
        CardCountSlider.Maximum = maximum;
        if (CardCountSlider.Value > maximum)
        {
            CardCountSlider.Value = maximum;
        }
    }

    private void UpdateLabels()
    {
        WidthValueText.Text = $"{WidthSlider.Value:0} px";
        var cardCount = (int)Math.Round(CardCountSlider.Value);
        var maximumCardCount = (int)Math.Round(CardCountSlider.Maximum);
        CardCountValueText.Text = cardCount == 1 ? "1 card" : $"{cardCount} cards";
        CardCountHintText.Text = maximumCardCount == 1
            ? "The current layout fits up to 1 card"
            : $"The current layout fits up to {maximumCardCount} cards";
        PreviewOpacityValueText.Text = $"{PreviewOpacitySlider.Value:0}%";
    }

    private void PublishChanges()
    {
        if (!_isReady)
        {
            return;
        }

        SettingsChanged?.Invoke(new StageManagerSettings(
            FixedModeRadio.IsChecked == true ? DisplayMode.Fixed : DisplayMode.Floating,
            WidthSlider.Value,
            (int)Math.Round(CardCountSlider.Value))
        {
            PreviewOpacity = PreviewOpacitySlider.Value / 100
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton is MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
