using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PortfolioWatch.Models
{
    public class NewsItem : ObservableObject
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;

        private ImageSource? _imageSource;
        [JsonIgnore]
        public ImageSource? ImageSource
        {
            get => _imageSource;
            set => SetProperty(ref _imageSource, value);
        }

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
                    RefreshDirectionalConfidence();
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

        private decimal _marketCap;
        public decimal MarketCap
        {
            get => _marketCap;
            set
            {
                if (SetProperty(ref _marketCap, value))
                {
                    OnPropertyChanged(nameof(InsiderSignalStrength));
                    OnPropertyChanged(nameof(InsiderSignalStrengthDisplay));
                }
            }
        }

        public decimal MarketValue => (decimal)Shares * Price;

        public decimal DayChangeValue => (decimal)Shares * Change;

        // --- Intraday Specific Properties (Always 1D) ---

        private decimal _intradayChange;
        public decimal IntradayChange
        {
            get => _intradayChange;
            set
            {
                if (SetProperty(ref _intradayChange, value))
                {
                    OnPropertyChanged(nameof(IntradayChangeValue));
                    OnPropertyChanged(nameof(IntradayIsUp));
                }
            }
        }

        private double _intradayChangePercent;
        public double IntradayChangePercent
        {
            get => _intradayChangePercent;
            set => SetProperty(ref _intradayChangePercent, value);
        }

        public bool IntradayIsUp => IntradayChange >= 0;

        private List<double> _intradayHistory = new List<double>();
        public List<double> IntradayHistory
        {
            get => _intradayHistory;
            set => SetProperty(ref _intradayHistory, value);
        }

        private List<DateTime> _intradayTimestamps = new List<DateTime>();
        public List<DateTime> IntradayTimestamps
        {
            get => _intradayTimestamps;
            set => SetProperty(ref _intradayTimestamps, value);
        }

        public decimal IntradayChangeValue => (decimal)Shares * IntradayChange;

        // ------------------------------------------------

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

        private double _earningsSurprisePercent;
        public double EarningsSurprisePercent
        {
            get => _earningsSurprisePercent;
            set
            {
                if (SetProperty(ref _earningsSurprisePercent, value))
                {
                    OnPropertyChanged(nameof(EarningsSignalStrength));
                    OnPropertyChanged(nameof(EarningsSignalStrengthDisplay));
                }
            }
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

        public double EarningsSignalStrength
        {
            get
            {
                if (EarningsStatus == "Upcoming") return 3.0; // Neutral/Unknown magnitude
                
                double absSurprise = Math.Abs(EarningsSurprisePercent);
                
                // Continuous scale from 2% (Score 1.0) to 20% (Score 5.0)
                if (absSurprise < 0.02) return 1.0;
                if (absSurprise >= 0.20) return 5.0;

                // Linear interpolation: 1 + (ratio of range) * 4
                return 1.0 + ((absSurprise - 0.02) / (0.20 - 0.02)) * 4.0;
            }
        }

        public string EarningsSignalStrengthDisplay => (EarningsSignalStrength * 2).ToString("0.0");

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

        public double OptionsSignalStrength => Math.Min(Math.Abs(DirectionalConfidence) * 12.5, 5.0); // Scale 0-0.4 to 0-5, clamped

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
            if (Price == 0 || TotalVolume == 0 || MaxPainPrice == 0) return 0;

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

        private int DaysToExpiration => OptionsImpactDate.HasValue ? (int)Math.Ceiling((OptionsImpactDate.Value.Date - DateTime.Now.Date).TotalDays) : 999;
        private bool IsExpirationImminent => DaysToExpiration <= 3;
        private double DistToMaxPainPct => (Price > 0 && MaxPainPrice > 0) ? (double)(Math.Abs(Price - MaxPainPrice) / Price) : 1.0;
        private bool ShouldDampenSignal => IsExpirationImminent && DistToMaxPainPct < 0.015;

        public string SignalStrengthDisplay
        {
            get
            {
                double val = Math.Abs(DirectionalConfidence) * 25;
                if (ShouldDampenSignal)
                {
                    val *= 0.3;
                }
                return Math.Min(10.0, val).ToString("0.0");
            }
        }
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

                // --- PHASE 2: TEXT GENERATION ---
                if (isMagnetBullish == isFlowBullish)
                {
                    explanation = $"Both Max Pain (${MaxPainPrice:0.00}) and Options Flow are {magnetStr}. Market structure and trader sentiment are aligned.";

                    if (IsExpirationImminent)
                    {
                        // Warn that this is a "Target" or "Pin", not an infinite trend
                        implication = $"EXPIRATION WARNING: With {DaysToExpiration} days left, this structure is acting as a MAGNET. Expect price to be pinned to ${MaxPainPrice:0.00} to kill premium. Upside is likely capped.";
                    }
                    else
                    {
                        implication = $"Expect this trend to continue through the {dateStr} expiration.";
                    }
                }
                else
                {
                    if (isOverallBullish == isFlowBullish)
                    {
                        explanation = $"{flowStr} Options Flow is overpowering the {magnetStr} pull of Max Pain (${MaxPainPrice:0.00}). Traders are betting against the house.";
                        
                        if (isFlowBullish)
                        {
                             // If bullish flow near expiration, warn about the "Ceiling" of Max Pain
                             implication = IsExpirationImminent
                                ? $"VOLATILITY TRAP: Aggressive buying is fighting the Max Pain gravity (${MaxPainPrice:0.00}). The price is likely to get stuck near this level and struggle to break higher unless volume explodes."
                                : $"Max Pain is currently acting as a drag on the price. Once this 'magnet' expires on {dateStr}, the stock may surge upward as that artificial pressure is released.";
                        }
                        else
                        {
                             implication = IsExpirationImminent
                                ? $"Price is heavy, but Max Pain (${MaxPainPrice:0.00}) may act as a floor/support for the next {DaysToExpiration} days."
                                : $"Max Pain is currently propping up the price. Once this support expires on {dateStr}, the stock may drop further as that artificial support is removed.";
                        }
                    }
                    else
                    {
                        explanation = $"The {magnetStr} pull of Max Pain (${MaxPainPrice:0.00}) is overpowering {flowStr} Options Flow. Market Maker incentives are weighing heavier than current volume.";
                        
                        // If Magnet Wins near expiration, it means the Pin is successful.
                        implication = IsExpirationImminent
                            ? $"GRAVITY WINS: Dealers are successfully pinning the price to ${MaxPainPrice:0.00}. Volatility will be crushed into Friday."
                            : $"The price is being pulled toward ${MaxPainPrice:0.00}. This magnetic pressure will persist until the {dateStr} expiration.";
                    }
                }

                // --- PHASE 3: SIGNAL STRENGTH ADJUSTMENT ---
                if (ShouldDampenSignal)
                {
                    explanation += " [Signal Dampened: Price is already pinned at target.]";
                }

                // NEW: Powder Keg Logic
                double rawStrength = Math.Abs(DirectionalConfidence) * 25;
                if (rawStrength > 9.0 && IsExpirationImminent)
                {
                     implication = "CRITICAL MASS: Options structure is overloaded (Score > 9). This is not a stable trend. Expect a violent move toward Max Pain or a massive breakout (Gamma Squeeze). This is a binary gamble, not a trade.";
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

        public bool HasInsiderFlag => InsiderSignalStrength > 0;

        public string InsiderEmoji => HasInsiderFlag ? "💼" : string.Empty;

        public string InsiderSentiment => NetInsiderTransactionValue > 0 ? "Accumulating" : "Distributing";

        public double InsiderSignalStrength
        {
            get
            {
                decimal absValue = Math.Abs(NetInsiderTransactionValue);
                double absValDouble = (double)absValue;

                if (NetInsiderTransactionValue > 0) // Buy
                {
                    // "Genius" Logic for Buys:
                    // Buys are high conviction signals. We use a Logarithmic Scale based on Absolute Dollar Value.
                    // A $10M buy is a massive signal regardless of company size.
                    // Range: $50k (Score 1.0) to $10M (Score 5.0)
                    
                    const double minVal = 50_000;
                    const double maxVal = 10_000_000;

                    if (absValDouble < minVal) return 0; // Ignore small buys
                    if (absValDouble >= maxVal) return 5.0;

                    // Logarithmic Interpolation
                    double logVal = Math.Log10(absValDouble);
                    double logMin = Math.Log10(minVal);
                    double logMax = Math.Log10(maxVal);

                    return 1.0 + ((logVal - logMin) / (logMax - logMin)) * 4.0;
                }
                else // Sell
                {
                    // "Genius" Logic for Sells:
                    // Sells are often noise (tax, diversification). We need an Adaptive Scale based on Market Cap.
                    // We define a "Max Threshold" (10/10 score) that scales with company size but has a ceiling.
                    
                    // Default to $100B (Large Cap) if Market Cap is missing to be conservative
                    decimal capDecimal = MarketCap > 0 ? MarketCap : 100_000_000_000m; 
                    
                    // Target Max: 0.05% of Market Cap
                    decimal maxThresholdDec = capDecimal * 0.0005m; 
                    
                    // Clamp the Max Threshold:
                    // Floor: $10M (For Small Caps, selling $10M is huge)
                    // Ceiling: $500M (For Mega Caps, selling $500M is huge, even if < 0.05%)
                    if (maxThresholdDec < 10_000_000m) maxThresholdDec = 10_000_000m;
                    if (maxThresholdDec > 500_000_000m) maxThresholdDec = 500_000_000m;

                    double maxVal = (double)maxThresholdDec;
                    double minVal = maxVal / 20.0; // Min threshold is 5% of the Max threshold

                    if (absValDouble < minVal) return 0; // Ignore noise
                    if (absValDouble >= maxVal) return 5.0;

                    // Logarithmic Interpolation for Sells
                    double logVal = Math.Log10(absValDouble);
                    double logMin = Math.Log10(minVal);
                    double logMax = Math.Log10(maxVal);

                    return 1.0 + ((logVal - logMin) / (logMax - logMin)) * 4.0;
                }
            }
        }

        public string InsiderSignalStrengthDisplay => (InsiderSignalStrength * 2).ToString("0.0");

        public string InsiderSignalColor => NetInsiderTransactionValue > 0 ? "#2ecc71" : "#e74c3c";

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

        public bool HasRVolFlag => RVolSignalStrength > 0;

        public string RVolEmoji => HasRVolFlag ? "🏦" : string.Empty;

        public string RVOLDisplay => $"{RVOL:0.0}x";

        public double RVolSignalStrength
        {
            get
            {
                // Continuous scale from 1.5x (Score 1.0) to 5.0x (Score 5.0)
                // 10x was too high; 5x is already an extreme outlier.
                if (RVOL < 1.5) return 0; // Ignore noise
                if (RVOL >= 5.0) return 5.0;

                return 1.0 + ((RVOL - 1.5) / (5.0 - 1.5)) * 4.0;
            }
        }

        public string RVolSignalStrengthDisplay => (RVolSignalStrength * 2).ToString("0.0");

        // RVOL validates the move. If price is up, it's a positive signal. If price is down, it's a negative signal.
        public string RVolSignalColor => Change >= 0 ? "#2ecc71" : "#e74c3c";

        public Stock Clone()
        {
            return new Stock
            {
                Symbol = this.Symbol,
                Name = this.Name,
                Price = this.Price,
                Change = this.Change,
                ChangePercent = this.ChangePercent,
                DayProgress = this.DayProgress,
                History = new List<double>(this.History),
                Timestamps = new List<DateTime>(this.Timestamps),
                Shares = this.Shares,
                MarketCap = this.MarketCap,
                PortfolioPercentage = this.PortfolioPercentage,
                EarningsDate = this.EarningsDate,
                EarningsStatus = this.EarningsStatus,
                EarningsMessage = this.EarningsMessage,
                EarningsSurprisePercent = this.EarningsSurprisePercent,
                NewsItems = new List<NewsItem>(this.NewsItems),
                OptionsImpactDate = this.OptionsImpactDate,
                UnusualOptionsVolume = this.UnusualOptionsVolume,
                GammaExposureCritical = this.GammaExposureCritical,
                MaxPainPrice = this.MaxPainPrice,
                CallVolume = this.CallVolume,
                PutVolume = this.PutVolume,
                TotalVolume = this.TotalVolume,
                CallIV = this.CallIV,
                PutIV = this.PutIV,
                OpenInterest = this.OpenInterest,
                AverageVolume = this.AverageVolume,
                NetInsiderTransactionValue = this.NetInsiderTransactionValue,
                InsiderTransactions = new List<InsiderTransaction>(this.InsiderTransactions),
                CurrentVolume = this.CurrentVolume,
                AverageVolumeByTimeOfDay = this.AverageVolumeByTimeOfDay
            };
        }
    }
}
