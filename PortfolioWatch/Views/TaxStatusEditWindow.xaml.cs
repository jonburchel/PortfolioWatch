using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using PortfolioWatch.Models;

namespace PortfolioWatch.Views
{
    public partial class TaxStatusEditWindow : Window
    {
        public TaxStatusEditViewModel ViewModel { get; }

        public TaxStatusEditWindow(ObservableCollection<TaxAllocation> currentAllocations)
        {
            InitializeComponent();
            ViewModel = new TaxStatusEditViewModel(currentAllocations);
            DataContext = ViewModel;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
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
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }

    public partial class TaxStatusEditViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<TaxAllocationViewModel> _allocations;

        public double TotalPercentage => Allocations.Sum(a => a.Percentage);

        public bool IsTotalValid => Math.Abs(TotalPercentage - 100) < 0.01;

        public TaxStatusEditViewModel(ObservableCollection<TaxAllocation> currentAllocations)
        {
            Allocations = new ObservableCollection<TaxAllocationViewModel>();

            // Ensure all types are present
            foreach (TaxStatusType type in Enum.GetValues(typeof(TaxStatusType)))
            {
                var existing = currentAllocations.FirstOrDefault(a => a.Type == type);
                var vm = new TaxAllocationViewModel(type, existing?.Percentage ?? 0);
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(TaxAllocationViewModel.Percentage))
                    {
                        OnPropertyChanged(nameof(TotalPercentage));
                        OnPropertyChanged(nameof(IsTotalValid));
                    }
                };
                Allocations.Add(vm);
            }
        }

        public System.Collections.Generic.List<TaxAllocation> GetAllocations()
        {
            return Allocations.Where(a => a.Percentage > 0)
                              .Select(a => new TaxAllocation { Type = a.Type, Percentage = a.Percentage })
                              .ToList();
        }
    }

    public partial class TaxAllocationViewModel : ObservableObject
    {
        public TaxStatusType Type { get; }
        public string Name { get; }
        public System.Windows.Media.Brush Brush { get; }

        [ObservableProperty]
        private double _percentage;

        public TaxAllocationViewModel(TaxStatusType type, double percentage)
        {
            Type = type;
            Percentage = percentage;
            
            var temp = new TaxAllocation { Type = type };
            Name = temp.Name;
            Brush = temp.Brush;
        }
    }
}
