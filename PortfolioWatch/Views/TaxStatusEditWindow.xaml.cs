using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioWatch.Models;

namespace PortfolioWatch.Views
{
    public partial class TaxStatusEditWindow : Window
    {
        public TaxStatusEditViewModel ViewModel { get; }
        public bool IsSaved { get; private set; }
        public bool IsShowingDialog { get; set; }

        public TaxStatusEditWindow(List<TaxCategory> globalCategories, ObservableCollection<TaxAllocation> currentAllocations)
        {
            InitializeComponent();
            ViewModel = new TaxStatusEditViewModel(globalCategories, currentAllocations, this);
            DataContext = ViewModel;
            
            this.KeyDown += Window_KeyDown;
            this.Deactivated += Window_Deactivated;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                // Only close if not editing a text box
                if (!(Keyboard.FocusedElement is System.Windows.Controls.TextBox))
                {
                    IsSaved = true;
                    Close();
                }
            }
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            if (IsShowingDialog) return;

            // Implicit cancel on lost focus
            try
            {
                Close(); 
            }
            catch
            {
                // Ignore
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            IsSaved = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // Allow digits and one decimal point
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }

    public partial class TaxAllocationViewModel : ObservableObject
    {
        private readonly TaxStatusEditViewModel _parent;
        private readonly TaxCategory _category;
        
        public Guid CategoryId => _category.Id;

        public string Name
        {
            get => _category.Name;
            set
            {
                if (_category.Name != value)
                {
                    _category.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ColorHex
        {
            get => _category.ColorHex;
            set
            {
                if (_category.ColorHex != value)
                {
                    _category.ColorHex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Brush));
                }
            }
        }

        [ObservableProperty]
        private bool _isEditingName;

        private double _percentage;
        public double Percentage
        {
            get => _percentage;
            set
            {
                if (Math.Abs(_percentage - value) > 0.001)
                {
                    _parent.RequestUpdateAllocation(this, value);
                }
            }
        }

        // Method for parent to update backing field without triggering loop
        public void SetPercentageSilent(double value)
        {
            if (Math.Abs(_percentage - value) > 0.001)
            {
                SetProperty(ref _percentage, value, nameof(Percentage));
            }
        }

        public Brush Brush 
        {
            get
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorHex));
                }
                catch
                {
                    return Brushes.Gray;
                }
            }
        }

        public TaxAllocationViewModel(TaxCategory category, double percentage, TaxStatusEditViewModel parent)
        {
            _parent = parent;
            _category = category;
            _percentage = percentage;
        }

        public TaxAllocation ToModel()
        {
            return new TaxAllocation
            {
                Id = Guid.NewGuid(), // New allocation ID
                CategoryId = _category.Id,
                Name = _category.Name,
                ColorHex = _category.ColorHex,
                Percentage = Percentage,
                Type = _category.Type
            };
        }

        public TaxCategory GetCategory() => _category;

        [RelayCommand]
        private void StartEditing()
        {
            IsEditingName = true;
        }

        [RelayCommand]
        private void StopEditing()
        {
            IsEditingName = false;
        }
        
        [RelayCommand]
        private void Remove()
        {
            _parent.RemoveAllocation(this);
        }
    }

    public partial class TaxStatusEditViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<TaxAllocationViewModel> _allocations = new();

        private readonly List<TaxCategory> _globalCategories;
        private readonly TaxStatusEditWindow _window;

        public double UnspecifiedPercentage => Math.Max(0, 100 - Allocations.Sum(a => a.Percentage));

        public TaxStatusEditViewModel(List<TaxCategory> globalCategories, ObservableCollection<TaxAllocation> currentAllocations, TaxStatusEditWindow window)
        {
            _globalCategories = globalCategories;
            _window = window;

            // Populate from global categories
            foreach (var category in globalCategories)
            {
                // Find existing percentage
                // Match by CategoryId first, then Name/Type for legacy
                var existing = currentAllocations.FirstOrDefault(a => a.CategoryId == category.Id);
                if (existing == null)
                {
                    existing = currentAllocations.FirstOrDefault(a => a.Name == category.Name && a.Type == category.Type);
                }

                double percentage = existing?.Percentage ?? 0;
                Allocations.Add(new TaxAllocationViewModel(category, percentage, this));
            }
            
            // Ensure we don't exceed 100 initially
            ValidateTotal();
        }

        private void ValidateTotal()
        {
            double total = Allocations.Sum(a => a.Percentage);
            if (total > 100)
            {
                double factor = 100.0 / total;
                foreach (var alloc in Allocations)
                {
                    alloc.SetPercentageSilent(alloc.Percentage * factor);
                }
            }
            OnPropertyChanged(nameof(UnspecifiedPercentage));
        }

        public void RequestUpdateAllocation(TaxAllocationViewModel target, double newValue)
        {
            // Clamp new value
            newValue = Math.Max(0, Math.Min(100, newValue));
            
            double oldValue = target.Percentage;
            double delta = newValue - oldValue;
            
            if (Math.Abs(delta) < 0.001) return;

            double currentUnspecified = UnspecifiedPercentage;

            if (delta < 0)
            {
                // Decreasing: Easy, just decrease. Unspecified grows.
                target.SetPercentageSilent(newValue);
            }
            else
            {
                // Increasing
                if (delta <= currentUnspecified + 0.001)
                {
                    // Enough unspecified space
                    target.SetPercentageSilent(newValue);
                }
                else
                {
                    // Not enough space, need to take from others
                    double neededFromOthers = delta - currentUnspecified;
                    
                    var others = Allocations.Where(a => a != target && a.Percentage > 0).ToList();
                    double othersSum = others.Sum(a => a.Percentage);

                    if (othersSum > 0.001)
                    {
                        // Reduce others proportionally
                        foreach (var other in others)
                        {
                            double reduction = neededFromOthers * (other.Percentage / othersSum);
                            double newOtherValue = Math.Max(0, other.Percentage - reduction);
                            other.SetPercentageSilent(newOtherValue);
                        }
                        // Set target
                        target.SetPercentageSilent(newValue);
                    }
                    else
                    {
                        // Cannot increase further
                        target.SetPercentageSilent(oldValue + currentUnspecified);
                    }
                }
            }

            OnPropertyChanged(nameof(UnspecifiedPercentage));
        }

        [RelayCommand]
        private void AddCategory()
        {
            // Generate a random color
            var random = new Random();
            var color = Color.FromRgb((byte)random.Next(50, 200), (byte)random.Next(50, 200), (byte)random.Next(50, 200));
            string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

            var newCategory = new TaxCategory
            {
                Name = "New Category",
                ColorHex = hex,
                Type = TaxStatusType.Custom
            };

            // Add to global list
            _globalCategories.Add(newCategory);

            var vm = new TaxAllocationViewModel(newCategory, 0, this);
            vm.IsEditingName = true; // Start editing immediately
            Allocations.Add(vm);
            OnPropertyChanged(nameof(UnspecifiedPercentage));
        }

        public void RemoveAllocation(TaxAllocationViewModel item)
        {
            if (Allocations.Contains(item))
            {
                // Confirmation
                var confirm = new ConfirmationWindow("Delete Category", 
                    $"Are you sure you want to delete '{item.Name}'? This will remove it from ALL tabs.", 
                    isAlert: false);
                
                confirm.Owner = _window;
                confirm.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                _window.IsShowingDialog = true;
                var result = confirm.ShowDialog();
                _window.IsShowingDialog = false;

                if (result == true)
                {
                    Allocations.Remove(item);
                    _globalCategories.Remove(item.GetCategory());
                    OnPropertyChanged(nameof(UnspecifiedPercentage));
                }
            }
        }

        public List<TaxAllocation> GetAllocations()
        {
            var list = new List<TaxAllocation>();
            
            // Add Unspecified if needed
            if (UnspecifiedPercentage > 0.001) 
            {
                list.Add(new TaxAllocation 
                { 
                    Type = TaxStatusType.Unspecified, 
                    Percentage = UnspecifiedPercentage,
                    Name = "Unspecified",
                    ColorHex = "#808080"
                });
            }
            
            foreach (var vm in Allocations)
            {
                // Return ALL allocations, even 0%, to ensure they are not deleted from other tabs during sync
                list.Add(vm.ToModel());
            }

            return list;
        }
    }
}
