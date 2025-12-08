using System.Windows;

namespace PortfolioWatch.Views
{
    public enum ImportAction
    {
        Cancel,
        Merge,
        Replace
    }

    public partial class ImportPromptWindow : Window
    {
        public ImportAction Result { get; private set; } = ImportAction.Cancel;

        public ImportPromptWindow()
        {
            InitializeComponent();
        }

        private void Merge_Click(object sender, RoutedEventArgs e)
        {
            Result = ImportAction.Merge;
            DialogResult = true;
            Close();
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            Result = ImportAction.Replace;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = ImportAction.Cancel;
            DialogResult = false;
            Close();
        }
    }
}
