using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace PortfolioWatch.Views
{
    public partial class CusipImportWindow : Window
    {
        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(CusipImportWindow), new PropertyMetadata(string.Empty));

        public string FundName => NameBox.Text;
        public double Quantity => double.TryParse(QuantityBox.Text, out double val) ? val : 0;
        public decimal TotalValue => decimal.TryParse(ValueBox.Text.Replace("$", "").Replace(",", ""), out decimal val) ? val : 0;

        public CusipImportWindow(string cusip)
        {
            InitializeComponent();
            Message = $"The ID '{cusip}' appears to be a private fund CUSIP. To track this, we need a few details.";
            NameBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("Please enter a fund name.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Quantity <= 0)
            {
                MessageBox.Show("Please enter a valid quantity.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (TotalValue <= 0)
            {
                MessageBox.Show("Please enter a valid total value.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
