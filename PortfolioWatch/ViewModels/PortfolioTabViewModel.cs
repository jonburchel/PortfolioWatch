using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
        private bool _isAddButton;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private bool _isIncludedInTotal;

        [ObservableProperty]
        private double _portfolioPercentage;

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
                Stocks = new System.Collections.Generic.List<Stock>(Stocks)
            };
        }
    }
}
