using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioWatch.Models;

namespace PortfolioWatch.ViewModels
{
    public partial class PortfolioTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private Guid _id;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private ObservableCollection<Stock> _stocks;

        [ObservableProperty]
        private ObservableCollection<TaxAllocation> _taxAllocations;

        [ObservableProperty]
        private bool _isAddButton;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private bool _isEditingTaxStatus;

        [ObservableProperty]
        private bool _isIncludedInTotal;

        [ObservableProperty]
        private double _portfolioPercentage;

        [ObservableProperty]
        private decimal _totalValue;

        // Used to restore state when tab becomes inactive
        public bool PreviousIsIncludedInTotal { get; private set; }

        public void SaveIncludedState()
        {
            PreviousIsIncludedInTotal = IsIncludedInTotal;
        }

        public void RestoreIncludedState()
        {
            IsIncludedInTotal = PreviousIsIncludedInTotal;
        }

        public PortfolioTabViewModel(PortfolioTab model)
        {
            Id = model.Id;
            Name = model.Name;
            IsIncludedInTotal = model.IsIncludedInTotal;

            if (model.TaxAllocations != null && model.TaxAllocations.Any())
            {
                TaxAllocations = new ObservableCollection<TaxAllocation>(model.TaxAllocations);
            }
            else
            {
                TaxAllocations = new ObservableCollection<TaxAllocation>
                {
                    new TaxAllocation { Type = TaxStatusType.Unspecified, Percentage = 100 },
                    new TaxAllocation { Type = TaxStatusType.NonTaxableRoth, Percentage = 0 },
                    new TaxAllocation { Type = TaxStatusType.TaxablePreTaxIRA, Percentage = 0 },
                    new TaxAllocation { Type = TaxStatusType.TaxableCapitalGains, Percentage = 0 }
                };
            }

            if (model.Stocks != null)
            {
                // Filter out null stocks to prevent crashes
                var validStocks = new System.Collections.Generic.List<Stock>();
                foreach (var s in model.Stocks)
                {
                    if (s != null) validStocks.Add(s);
                }
                Stocks = new ObservableCollection<Stock>(validStocks);
            }
            else
            {
                Stocks = new ObservableCollection<Stock>();
            }
        }

        public PortfolioTabViewModel(bool isAddButton)
        {
            IsAddButton = isAddButton;
            Name = "+";
            Stocks = new ObservableCollection<Stock>();
            TaxAllocations = new ObservableCollection<TaxAllocation>
            {
                new TaxAllocation { Type = TaxStatusType.Unspecified, Percentage = 100 }
            };
            Id = Guid.NewGuid();
            IsIncludedInTotal = false; // Add button is never included
        }

        public PortfolioTab ToModel()
        {
            return new PortfolioTab
            {
                Id = Id,
                Name = Name,
                IsIncludedInTotal = IsIncludedInTotal,
                Stocks = new System.Collections.Generic.List<Stock>(Stocks),
                TaxAllocations = new System.Collections.Generic.List<TaxAllocation>(TaxAllocations)
            };
        }

        [RelayCommand]
        private void EditTaxStatus()
        {
            // This will be handled by the view/mainwindow to open the dialog
            // We can use a messenger or event, but for simplicity in this project structure,
            // we might just expose the command and bind it in the view.
            // Actually, since we need to open a window, it's better to do it from the View layer or via a service.
            // But for now, let's just have the command here and we'll bind to it in the MainWindow.
            // Wait, the MainWindow is the one that will open the dialog.
            // Let's define an event here that the MainWindow can subscribe to, or just handle it in the View directly.
            // Given the existing structure, handling it in the View (MainWindow.xaml.cs) via an event handler on the button might be easiest,
            // but binding to a command in the ViewModel is cleaner MVVM.
            // Let's use a simple event for now.
            RequestEditTaxStatus?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? RequestEditTaxStatus;
    }
}
