using System.Windows;

namespace PortfolioWatch.Views
{
    public partial class ConfirmationWindow : Window
    {
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(ConfirmationWindow), new PropertyMetadata(string.Empty));

        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public bool ResetSettings => ResetSettingsCheckBox.IsChecked == true;

        public ConfirmationWindow(string title, string message, bool showResetOption = false, bool isAlert = false)
        {
            InitializeComponent();
            Title = title;
            Message = message;
            if (showResetOption)
            {
                ResetSettingsCheckBox.Visibility = Visibility.Visible;
            }

            if (isAlert)
            {
                NoButton.Visibility = Visibility.Collapsed;
                YesButton.Content = "OK";
                YesButton.Background = (System.Windows.Media.Brush)Application.Current.Resources["ControlBackgroundBrush"];
                YesButton.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["PrimaryForegroundBrush"];
            }
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
