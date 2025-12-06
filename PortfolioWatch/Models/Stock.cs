using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PortfolioWatch.Models
{
    public class NewsItem
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        [JsonIgnore]
        public ImageSource? ImageSource { get; set; }
        public string Source { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
    }

    public class InsiderTransaction
    {
        public DateTime Date { get; set; }
        public string Person { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty; // "Buy" or "Sell"
        public decimal Value { get; set; }
        public string ValueDisplay => Value.ToString("C0");
        public string DateDisplay => Date.ToString("MM/dd");
        public string Color => TransactionType.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? "#2ecc71" : "#e74c3c";
    }

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
                    // Recalculate Directional Bias if price changes significantly? 
                    // Requirement says "calculated on data ingest only", so we might not need to trigger it here unless Price setter IS the ingest.
                    // For now, we'll assume ingest sets properties directly or calls a method.
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
                    OnPropertyChanged(nameof(EarningsEmoji));
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

        public string EarningsEmoji => EarningsStatus switch
        {
            "Upcoming" => "📅",
            "Beat" => "🎯",
            "Miss" => "📉",
            _ => string.Empty
        };

        // News Properties
        private List<NewsItem> _newsItems = new List<NewsItem>();
        public List<NewsItem> NewsItems
        {
            get => _newsItems;
            set
            {
                if (SetProperty(ref _newsItems, value))
                {
                    OnPropertyChanged(nameof(HasNewsFlag));
                    OnPropertyChanged(nameof(NewsEmoji));
                }
            }
        }

        public bool HasNewsFlag => NewsItems != null && NewsItems.Any();

        public string NewsEmoji => HasNewsFlag ? "📰" : string.Empty;

        // --- Orange Flag: Options & Market Structure ---

        private DateTime? _optionsImpactDate;
        public DateTime? OptionsImpactDate
        {
            get => _optionsImpactDate;
            set
            {
                if (SetProperty(ref _optionsImpactDate, value))
                {
                    OnPropertyChanged(nameof(HasOptionsFlag));
                    OnPropertyChanged(nameof(OptionsEmoji));
                }
            }
        }

        private bool _unusualOptionsVolume;
        public bool UnusualOptionsVolume
        {
            get => _unusualOptionsVolume;
            set
            {
                if (SetProperty(ref _unusualOptionsVolume, value))
                {
                    OnPropertyChanged(nameof(HasOptionsFlag));
                    OnPropertyChanged(nameof(OptionsEmoji));
                }
            }
        }

        private bool _gammaExposureCritical;
        public bool GammaExposureCritical
        {
            get => _gammaExposureCritical;
            set
            {
                if (SetProperty(ref _gammaExposureCritical, value))
                {
                    OnPropertyChanged(nameof(HasOptionsFlag));
                    OnPropertyChanged(nameof(OptionsEmoji));
                }
            }
        }

        public bool HasOptionsFlag => (UnusualOptionsVolume || GammaExposureCritical) && 
                                      OptionsImpactDate.HasValue && 
                                      (OptionsImpactDate.Value - DateTime.Now.Date).TotalDays <= 7;

        public string OptionsEmoji
        {
            get
            {
                if (!HasOptionsFlag) return string.Empty;
                // Bullish if DirectionalConfidence is positive, Bearish if negative
                return DirectionalConfidence >= 0 ? "🐂" : "🐻";
            }
        }

        private decimal _maxPainPrice;
        public decimal MaxPainPrice
        {
            get => _maxPainPrice;
            set => SetProperty(ref _maxPainPrice, value);
        }

        public string MaxPainColor => MaxPainPrice > Price ? "#2ecc71" : "#e74c3c";

        private long _callVolume;
        public long CallVolume
        {
            get => _callVolume;
            set => SetProperty(ref _callVolume, value);
        }

        private long _putVolume;
        public long PutVolume
        {
            get => _putVolume;
            set => SetProperty(ref _putVolume, value);
        }

        private long _totalVolume;
        public long TotalVolume
        {
            get => _totalVolume;
            set => SetProperty(ref _totalVolume, value);
        }

        private double? _callIV;
        public double? CallIV
        {
            get => _callIV;
            set => SetProperty(ref _callIV, value);
        }

        private double? _putIV;
        public double? PutIV
        {
            get => _putIV;
            set => SetProperty(ref _putIV, value);
        }

        private long _openInterest;
        public long OpenInterest
        {
            get => _openInterest;
            set
            {
                if (SetProperty(ref _openInterest, value))
                    OnPropertyChanged(nameof(GammaRiskDisplay));
            }
        }

        private long _averageVolume;
        public long AverageVolume
        {
            get => _averageVolume;
            set
            {
                if (SetProperty(ref _averageVolume, value))
                    OnPropertyChanged(nameof(GammaRiskDisplay));
            }
        }

        public string GammaRiskDisplay => (AverageVolume > 0 && OpenInterest > 2 * AverageVolume) ? "High" : "Normal";
        public string GammaRiskColor => GammaRiskDisplay == "High" ? "#e74c3c" : "#2ecc71";

        private double? _cachedDirectionalConfidence;
        public double DirectionalConfidence
        {
            get
            {
                if (!_cachedDirectionalConfidence.HasValue)
                {
                    _cachedDirectionalConfidence = CalculateDirectionalConfidence();
                }
                return _cachedDirectionalConfidence.Value;
            }
        }

        public void RefreshDirectionalConfidence()
        {
            _cachedDirectionalConfidence = null;
            OnPropertyChanged(nameof(DirectionalConfidence));
            OnPropertyChanged(nameof(OptionsFlagIconKind));
            OnPropertyChanged(nameof(NetFlowDisplay));
            OnPropertyChanged(nameof(OptionsSummary));
            OnPropertyChanged(nameof(OptionsEmoji));
            OnPropertyChanged(nameof(SignalStrengthDisplay));
            OnPropertyChanged(nameof(SignalStrengthColor));
        }

        private double CalculateDirectionalConfidence()
        {
            if (Price == 0 || TotalVolume == 0) return 0;

            // Step 1: Magnet Pull
            double magnetPull = (double)((MaxPainPrice - Price) / Price);

            // Step 2: Flow Sentiment
            double flowSentiment = (double)(CallVolume - PutVolume) / TotalVolume;

            // Step 3: Skew (Optional - simplified for now as per requirements "If IV data exists")
            // Requirement: DirectionalConfidence = (MagnetPull * 0.4) + (FlowSentiment * 0.6)
            // Skew is mentioned as optional step 3 but not in the final formula provided in the prompt.
            // "Result: DirectionalConfidence = (MagnetPull * 0.4) + (FlowSentiment * 0.6)."
            
            return (magnetPull * 0.4) + (flowSentiment * 0.6);
        }

        // Iconography: Up Arrow (> 0.3), Down Arrow (< -0.3), Circle (Between)
        public string OptionsFlagIconKind
        {
            get
            {
                if (DirectionalConfidence > 0.3) return "Up";
                if (DirectionalConfidence < -0.3) return "Down";
                return "Neutral";
            }
        }

        public string NetFlowDisplay => (CallVolume > PutVolume) ? "Bullish" : "Bearish";
        public string NetFlowColor => (CallVolume > PutVolume) ? "#2ecc71" : "#e74c3c";

        public string SignalStrengthDisplay => (Math.Abs(DirectionalConfidence) * 10).ToString("0.0");
        public string SignalStrengthColor => DirectionalConfidence >= 0 ? "#2ecc71" : "#e74c3c";

        public string OptionsSummary
        {
            get
            {
                if (Price == 0 || TotalVolume == 0) return "Insufficient data.";

                double magnetPull = (double)((MaxPainPrice - Price) / Price);
                double flowSentiment = (double)(CallVolume - PutVolume) / TotalVolume;
                double confidence = (magnetPull * 0.4) + (flowSentiment * 0.6);

                bool isMagnetBullish = magnetPull >= 0;
                bool isFlowBullish = flowSentiment >= 0;
                bool isOverallBullish = confidence >= 0;

                string magnetStr = isMagnetBullish ? "Bullish" : "Bearish";
                string flowStr = isFlowBullish ? "Bullish" : "Bearish";
                string dateStr = OptionsImpactDate.HasValue ? OptionsImpactDate.Value.ToString("M/d") : "upcoming expiration";
                
                string explanation;
                string implication;

                if (isMagnetBullish == isFlowBullish)
                {
                    explanation = $"Both Max Pain (${MaxPainPrice:0.00}) and Options Flow are {magnetStr}. Market structure and trader sentiment are aligned.";
                    implication = $"Expect this trend to continue through the {dateStr} expiration.";
                }
                else
                {
                    if (isOverallBullish == isFlowBullish)
                    {
                        explanation = $"{flowStr} Options Flow is overpowering the {magnetStr} pull of Max Pain (${MaxPainPrice:0.00}). Traders are betting against the house.";
                        
                        if (isFlowBullish)
                        {
                             implication = $"Max Pain is currently acting as a drag on the price. Once this 'magnet' expires on {dateStr}, the stock may surge upward as that artificial pressure is released.";
                        }
                        else
                        {
                             implication = $"Max Pain is currently propping up the price. Once this support expires on {dateStr}, the stock may drop further as that artificial support is removed.";
                        }
                    }
                    else
                    {
                        explanation = $"The {magnetStr} pull of Max Pain (${MaxPainPrice:0.00}) is overpowering {flowStr} Options Flow. Market Maker incentives are weighing heavier than current volume.";
                        implication = $"The price is being pulled toward ${MaxPainPrice:0.00}. This magnetic pressure will persist until the {dateStr} expiration.";
                    }
                }

                return $"Signal Strength: {SignalStrengthDisplay}/10\n\n{explanation}\n\n{implication}";
            }
        }

        // --- Purple Flag: Insider Conviction ---

        private decimal _netInsiderTransactionValue;
        public decimal NetInsiderTransactionValue
        {
            get => _netInsiderTransactionValue;
            set
            {
                if (SetProperty(ref _netInsiderTransactionValue, value))
                {
                    OnPropertyChanged(nameof(HasInsiderFlag));
                    OnPropertyChanged(nameof(InsiderSentiment));
                    OnPropertyChanged(nameof(InsiderEmoji));
                }
            }
        }

        private List<InsiderTransaction> _insiderTransactions = new List<InsiderTransaction>();
        public List<InsiderTransaction> InsiderTransactions
        {
            get => _insiderTransactions;
            set => SetProperty(ref _insiderTransactions, value);
        }

        public bool HasInsiderFlag => NetInsiderTransactionValue > 500000 || NetInsiderTransactionValue < -1000000; // Buy > 500k, Sell > 1M (Sell is negative value usually, but prompt says > 1M sell, implying absolute value or specific logic. Assuming Net Value is signed.)
        // Clarification: "Net Insider Transaction Value ... > $500k (Buy) or > $1M (Sell)"
        // If NetValue is positive (Buy), check > 500k.
        // If NetValue is negative (Sell), check < -1M (magnitude > 1M).
        // Let's assume NetInsiderTransactionValue is signed (+ for buy, - for sell).
        // So: (Net > 500,000) OR (Net < -1,000,000)

        public string InsiderEmoji => HasInsiderFlag ? "💼" : string.Empty;

        public string InsiderSentiment => NetInsiderTransactionValue > 0 ? "Accumulating" : "Distributing";

        // --- Yellow Flag: Relative Volume (RVOL) ---

        private long _currentVolume;
        public long CurrentVolume
        {
            get => _currentVolume;
            set
            {
                if (SetProperty(ref _currentVolume, value))
                {
                    OnPropertyChanged(nameof(RVOL));
                    OnPropertyChanged(nameof(HasRVolFlag));
                    OnPropertyChanged(nameof(RVOLDisplay));
                    OnPropertyChanged(nameof(RVolEmoji));
                }
            }
        }

        private long _averageVolumeByTimeOfDay;
        public long AverageVolumeByTimeOfDay
        {
            get => _averageVolumeByTimeOfDay;
            set
            {
                if (SetProperty(ref _averageVolumeByTimeOfDay, value))
                {
                    OnPropertyChanged(nameof(RVOL));
                    OnPropertyChanged(nameof(HasRVolFlag));
                    OnPropertyChanged(nameof(RVOLDisplay));
                    OnPropertyChanged(nameof(RVolEmoji));
                }
            }
        }

        public double RVOL => _averageVolumeByTimeOfDay > 0 ? (double)_currentVolume / _averageVolumeByTimeOfDay : 0;

        public bool HasRVolFlag => RVOL > 1.5;

        public string RVolEmoji => HasRVolFlag ? "🏦" : string.Empty;

        public string RVOLDisplay => $"{RVOL:0.0}x";
    }
}
