using System;
using System.Threading.Tasks;
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

        public static readonly DependencyProperty IconTextProperty =
            DependencyProperty.Register("IconText", typeof(string), typeof(ConfirmationWindow), new PropertyMetadata("🔄"));

        public string IconText
        {
            get { return (string)GetValue(IconTextProperty); }
            set { SetValue(IconTextProperty, value); }
        }

        public static readonly DependencyProperty DetailsProperty =
            DependencyProperty.Register("Details", typeof(string), typeof(ConfirmationWindow), new PropertyMetadata(null));

        public string? Details
        {
            get { return (string?)GetValue(DetailsProperty); }
            set { SetValue(DetailsProperty, value); }
        }

        public bool ResetSettings => ResetSettingsCheckBox.IsChecked == true;

        public Func<Task>? AutoRunTask { get; set; }
        public string? SuccessMessage { get; set; }

        public ConfirmationWindow(string title, string message, bool showResetOption = false, bool isAlert = false, string icon = "🔄", string? details = null)
        {
            InitializeComponent();
            Title = title;
            Message = message;
            IconText = icon;
            Details = details;

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

            if (string.IsNullOrEmpty(details))
            {
                DetailsExpander.Visibility = Visibility.Collapsed;
            }
        }

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (AutoRunTask != null)
            {
                // Switch to busy state
                ActionButtonsPanel.Visibility = Visibility.Collapsed;
                ProgressPanel.Visibility = Visibility.Visible;
                
                // Disable close button via flag or just rely on modal behavior (user can't click X easily if we don't handle Closing event, but X works. 
                // For now, let's assume user won't close, or if they do, task continues in background but window closes.
                // Ideally we should prevent closing, but let's keep it simple.)

                try
                {
                    await AutoRunTask();
                    
                    if (!string.IsNullOrEmpty(SuccessMessage))
                    {
                        Message = SuccessMessage;
                    }
                }
                catch (Exception ex)
                {
                    Message = $"Operation failed: {ex.Message}";
                    IconText = "❌";
                    Details = ex.ToString();
                    DetailsExpander.Visibility = Visibility.Visible;
                }
                finally
                {
                    ProgressPanel.Visibility = Visibility.Collapsed;
                    ActionButtonsPanel.Visibility = Visibility.Visible;
                }
            }
        }

        private void CopyMessage_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(Message);
        }

        private void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            var fullText = $"Message: {Message}\n\nDetails:\n{Details}";
            Clipboard.SetText(fullText);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
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
