using System.Windows;

namespace PortfolioWatch.Views
{
    public enum UpdatePromptResult
    {
        None,
        Update,
        Snooze,
        Disable
    }

    public partial class UpdatePromptWindow : Window
    {
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(UpdatePromptWindow), new PropertyMetadata(string.Empty));

        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public static readonly DependencyProperty IsInfoModeProperty =
            DependencyProperty.Register("IsInfoMode", typeof(bool), typeof(UpdatePromptWindow), new PropertyMetadata(false));

        public bool IsInfoMode
        {
            get { return (bool)GetValue(IsInfoModeProperty); }
            set { SetValue(IsInfoModeProperty, value); }
        }

        public UpdatePromptResult Result { get; private set; } = UpdatePromptResult.None;

        public UpdatePromptWindow()
        {
            InitializeComponent();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdatePromptResult.Update;
            DialogResult = true;
            Close();
        }

        private void SnoozeButton_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdatePromptResult.Snooze;
            DialogResult = false;
            Close();
        }

        private void DisableButton_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdatePromptResult.Disable;
            DialogResult = false;
            Close();
        }
    }
}
