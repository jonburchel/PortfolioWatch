using System.Windows;
using System.Windows.Input;

namespace PortfolioWatch.Views
{
    public partial class InputWindow : Window
    {
        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(InputWindow), new PropertyMetadata(string.Empty));

        public string InputText
        {
            get { return InputBox.Text; }
            set { InputBox.Text = value; InputBox.SelectAll(); }
        }

        public InputWindow(string message, string title, string defaultText = "")
        {
            InitializeComponent();
            Message = message;
            Title = title;
            InputText = defaultText;
            InputBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
                Close();
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }
    }
}
