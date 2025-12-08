using System;
using System.Collections.Generic;
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
            // Allow digits and one decimal point
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }

    public partial class TaxStatusEditViewModel : ObservableObject
    {
        private double _nonTaxableRothPercentage;
        private double _taxablePreTaxIRAPercentage;
        private double _taxableCapitalGainsPercentage;

        public double UnspecifiedPercentage => Math.Max(0, 100 - (NonTaxableRothPercentage + TaxablePreTaxIRAPercentage + TaxableCapitalGainsPercentage));

        public double NonTaxableRothPercentage
        {
            get => _nonTaxableRothPercentage;
            set => UpdateAllocation(TaxStatusType.NonTaxableRoth, value);
        }

        public double TaxablePreTaxIRAPercentage
        {
            get => _taxablePreTaxIRAPercentage;
            set => UpdateAllocation(TaxStatusType.TaxablePreTaxIRA, value);
        }

        public double TaxableCapitalGainsPercentage
        {
            get => _taxableCapitalGainsPercentage;
            set => UpdateAllocation(TaxStatusType.TaxableCapitalGains, value);
        }

        public TaxStatusEditViewModel(ObservableCollection<TaxAllocation> currentAllocations)
        {
            // Initialize from existing allocations
            var roth = currentAllocations.FirstOrDefault(a => a.Type == TaxStatusType.NonTaxableRoth);
            var ira = currentAllocations.FirstOrDefault(a => a.Type == TaxStatusType.TaxablePreTaxIRA);
            var gains = currentAllocations.FirstOrDefault(a => a.Type == TaxStatusType.TaxableCapitalGains);

            _nonTaxableRothPercentage = roth?.Percentage ?? 0;
            _taxablePreTaxIRAPercentage = ira?.Percentage ?? 0;
            _taxableCapitalGainsPercentage = gains?.Percentage ?? 0;
            
            // Ensure we don't exceed 100 initially (sanity check)
            double total = _nonTaxableRothPercentage + _taxablePreTaxIRAPercentage + _taxableCapitalGainsPercentage;
            if (total > 100)
            {
                double factor = 100.0 / total;
                _nonTaxableRothPercentage *= factor;
                _taxablePreTaxIRAPercentage *= factor;
                _taxableCapitalGainsPercentage *= factor;
            }
        }

        private void UpdateAllocation(TaxStatusType type, double newValue)
        {
            // Clamp new value between 0 and 100
            newValue = Math.Max(0, Math.Min(100, newValue));

            double oldValue = type switch
            {
                TaxStatusType.NonTaxableRoth => _nonTaxableRothPercentage,
                TaxStatusType.TaxablePreTaxIRA => _taxablePreTaxIRAPercentage,
                TaxStatusType.TaxableCapitalGains => _taxableCapitalGainsPercentage,
                _ => 0
            };

            if (Math.Abs(newValue - oldValue) < 0.001) return;

            double delta = newValue - oldValue;
            double currentUnspecified = UnspecifiedPercentage;

            if (delta < 0)
            {
                // Sliding left (or typing lower value): simply reduce the value, Unspecified increases automatically
                SetBackingField(type, newValue);
            }
            else
            {
                // Sliding right (or typing higher value)
                if (delta <= currentUnspecified + 0.001) // Tolerance for float math
                {
                    // Enough unspecified space
                    SetBackingField(type, newValue);
                }
                else
                {
                    // Not enough unspecified space, need to take from others
                    double neededFromOthers = delta - currentUnspecified;
                    
                    // Identify others
                    var others = new List<(TaxStatusType Type, double Value)>();
                    if (type != TaxStatusType.NonTaxableRoth) others.Add((TaxStatusType.NonTaxableRoth, _nonTaxableRothPercentage));
                    if (type != TaxStatusType.TaxablePreTaxIRA) others.Add((TaxStatusType.TaxablePreTaxIRA, _taxablePreTaxIRAPercentage));
                    if (type != TaxStatusType.TaxableCapitalGains) others.Add((TaxStatusType.TaxableCapitalGains, _taxableCapitalGainsPercentage));

                    double othersSum = others.Sum(o => o.Value);

                    if (othersSum > 0.001)
                    {
                        // Reduce others proportionally
                        foreach (var other in others)
                        {
                            double reduction = neededFromOthers * (other.Value / othersSum);
                            double newOtherValue = Math.Max(0, other.Value - reduction);
                            SetBackingField(other.Type, newOtherValue);
                        }
                        // Set the target value
                        SetBackingField(type, newValue);
                    }
                    else
                    {
                        // Cannot increase further because others are 0 and unspecified is 0
                        // Cap the increase to available unspecified
                        SetBackingField(type, oldValue + currentUnspecified);
                    }
                }
            }

            OnPropertyChanged(nameof(UnspecifiedPercentage));
            OnPropertyChanged(nameof(NonTaxableRothPercentage));
            OnPropertyChanged(nameof(TaxablePreTaxIRAPercentage));
            OnPropertyChanged(nameof(TaxableCapitalGainsPercentage));
        }

        private void SetBackingField(TaxStatusType type, double value)
        {
            switch (type)
            {
                case TaxStatusType.NonTaxableRoth: _nonTaxableRothPercentage = value; break;
                case TaxStatusType.TaxablePreTaxIRA: _taxablePreTaxIRAPercentage = value; break;
                case TaxStatusType.TaxableCapitalGains: _taxableCapitalGainsPercentage = value; break;
            }
        }

        public List<TaxAllocation> GetAllocations()
        {
            var list = new List<TaxAllocation>();
            
            if (UnspecifiedPercentage > 0.001) 
                list.Add(new TaxAllocation { Type = TaxStatusType.Unspecified, Percentage = UnspecifiedPercentage });
            
            if (NonTaxableRothPercentage > 0.001)
                list.Add(new TaxAllocation { Type = TaxStatusType.NonTaxableRoth, Percentage = NonTaxableRothPercentage });
                
            if (TaxablePreTaxIRAPercentage > 0.001)
                list.Add(new TaxAllocation { Type = TaxStatusType.TaxablePreTaxIRA, Percentage = TaxablePreTaxIRAPercentage });
                
            if (TaxableCapitalGainsPercentage > 0.001)
                list.Add(new TaxAllocation { Type = TaxStatusType.TaxableCapitalGains, Percentage = TaxableCapitalGainsPercentage });

            return list;
        }
    }
}
