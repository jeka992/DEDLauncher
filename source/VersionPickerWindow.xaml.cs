using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DedLauncher;

/// <summary>
/// Тёмное окно выбора версии мода/файла. Рекомендуемая (совместимая
/// с профилем) версия отмечена бейджем «ПОДХОДИТ» и выбрана по умолчанию.
/// </summary>
public partial class VersionPickerWindow : Window
{
    public class Choice
    {
        public string Title { get; set; } = "";
        public string Sub { get; set; } = "";
        public bool Recommended { get; set; }
        public object? Tag { get; set; }
    }

    private Choice? _result;

    public VersionPickerWindow()
    {
        InitializeComponent();
    }

    /// <summary>Показывает окно и возвращает выбор (null — отмена).</summary>
    public static Choice? ShowPick(string title, string subtitle,
        IReadOnlyList<Choice> choices, int recommendedIndex)
    {
        var win = new VersionPickerWindow
        {
            Owner = Application.Current.MainWindow
        };
        win.TitleText.Text = title;
        win.SubText.Text = subtitle;
        win.VersionsList.ItemsSource = choices;
        if (choices.Count > 0)
            win.VersionsList.SelectedIndex = recommendedIndex >= 0 ? recommendedIndex : 0;
        win.OkBtn.IsEnabled = choices.Count > 0;
        win.ShowDialog();
        return win._result;
    }

    private Choice? SelectedChoice => VersionsList.SelectedItem as Choice;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _result = null;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _result = null;
        Close();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _result = SelectedChoice;
        if (_result != null) Close();
    }

    private void VersionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedChoice != null)
        {
            _result = SelectedChoice;
            Close();
        }
    }

    private void VersionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OkBtn.IsEnabled = SelectedChoice != null;
    }
}
