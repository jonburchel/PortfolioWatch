using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PortfolioWatch.Models
{
    public class Stock : ObservableObject
    {
        private string _symbol = string.Empty;
        public string Symbol
        {
            get => _symbol;
            set => SetProperty(ref _symbol, value);
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set
            {
                if (SetProperty(ref _price, value))
                {
                    OnPropertyChanged(nameof(IsUp));
                    OnPropertyChanged(nameof(MarketValue));
                    OnPropertyChanged(nameof(DayChangeValue));
                }
            }
        }

        private decimal _change;
        public decimal Change
        {
            get => _change;
            set
            {
                if (SetProperty(ref _change, value))
                {
                    OnPropertyChanged(nameof(IsUp));
                    OnPropertyChanged(nameof(DayChangeValue));
                }
            }
        }

        private double _changePercent;
        public double ChangePercent
        {
            get => _changePercent;
            set => SetProperty(ref _changePercent, value);
        }

        private double _dayProgress = 1.0;
        public double DayProgress
        {
            get => _dayProgress;
            set => SetProperty(ref _dayProgress, value);
        }

        public bool IsUp => Change >= 0;

        public double PreviousClose => (double)(Price - Change);

        private List<double> _history = new List<double>();
        public List<double> History
        {
            get => _history;
            set => SetProperty(ref _history, value);
        }

        private List<DateTime> _timestamps = new List<DateTime>();
        public List<DateTime> Timestamps
        {
            get => _timestamps;
            set => SetProperty(ref _timestamps, value);
        }

        private double _shares;
        public double Shares
        {
            get => _shares;
            set
            {
                if (SetProperty(ref _shares, value))
                {
                    OnPropertyChanged(nameof(MarketValue));
                    OnPropertyChanged(nameof(DayChangeValue));
                }
            }
        }

        public decimal MarketValue => (decimal)Shares * Price;

        public decimal DayChangeValue => (decimal)Shares * Change;

        private double _portfolioPercentage;
        public double PortfolioPercentage
        {
            get => _portfolioPercentage;
            set => SetProperty(ref _portfolioPercentage, value);
        }

        // Earnings Properties
        private DateTime? _earningsDate;
        public DateTime? EarningsDate
        {
            get => _earningsDate;
            set => SetProperty(ref _earningsDate, value);
        }

        private string _earningsStatus = "None"; // None, Upcoming, Beat, Miss
        public string EarningsStatus
        {
            get => _earningsStatus;
            set
            {
                if (SetProperty(ref _earningsStatus, value))
                {
                    OnPropertyChanged(nameof(HasEarningsFlag));
                    OnPropertyChanged(nameof(EarningsFlagColor));
                }
            }
        }

        private string _earningsMessage = string.Empty;
        public string EarningsMessage
        {
            get => _earningsMessage;
            set => SetProperty(ref _earningsMessage, value);
        }

        public bool HasEarningsFlag => EarningsStatus != "None";

        public string EarningsFlagColor => EarningsStatus switch
        {
            "Upcoming" => "#3498db", // Blue
            "Beat" => "#2ecc71",     // Green
            "Miss" => "#e74c3c",     // Red
            _ => "Transparent"
        };
    }
}
