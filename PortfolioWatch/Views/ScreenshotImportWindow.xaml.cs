using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PortfolioWatch.Models;
using PortfolioWatch.Services;

namespace PortfolioWatch.Views
{
    public partial class ScreenshotImportWindow : Window
    {
        public List<BitmapSource> CapturedImages { get; private set; } = new List<BitmapSource>();
        public List<ParsedHolding> ParsedHoldings { get; private set; } = new List<ParsedHolding>();
        private readonly GeminiService _geminiService = new GeminiService();

        public ScreenshotImportWindow()
        {
            InitializeComponent();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                PasteImage();
            }
        }

        private void PasteImage()
        {
            if (Clipboard.ContainsImage())
            {
                var image = Clipboard.GetImage();
                if (image != null)
                {
                    AddImage(image);
                }
            }
        }

        private void AddImage(BitmapSource image)
        {
            CapturedImages.Add(image);

            var imgControl = new Image
            {
                Source = image,
                Width = 180,
                Height = 180,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(5)
            };

            var border = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)FindResource("ControlBorderBrush"),
                Background = (Brush)FindResource("ControlBackgroundBrush"),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(5),
                Child = imgControl,
                Width = 200,
                Height = 200
            };

            border.ContextMenu = new ContextMenu();
            var removeItem = new MenuItem { Header = "Remove" };
            removeItem.Click += (s, e) => 
            {
                ImagesPanel.Children.Remove(border);
                CapturedImages.Remove(image);
            };
            border.ContextMenu.Items.Add(removeItem);

            ImagesPanel.Children.Add(border);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ImagesPanel.Children.Clear();
            CapturedImages.Clear();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void Process_Click(object sender, RoutedEventArgs e)
        {
            if (CapturedImages.Count == 0)
            {
                MessageBox.Show("Please paste at least one screenshot.", "No Images", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            StatusText.Text = "Preparing images...";

            try
            {
                StatusText.Text = "The model is analyzing your portfolio...";

                string prompt = "Please analyze these screenshots of a portfolio. Extract the holdings into a markdown table with columns: Account name (blank if not found anywhere on the page, but if there is an account name above a group of securities, all of them should be listed with that account name even if not explicitly stated beside each security), Company name (use symbol if unknown), Symbol, Quantity, and Total Value. For cash positions, use 'SPAXX' as the symbol and 'Cash' as the company name, and set the share count equal to the dollar value. IMPORTANT: If any of the extracted Account Names appears as a holding in another account (e.g. a 'BrokerageLink' account appearing as a line item), EXCLUDE that holding from the table. Output ONLY the markdown table. If you cannot extract the data at all, output 'ERROR: <reason>'. Do not include any conversational text.";

                string text = await _geminiService.AnalyzeScreenshotAsync(CapturedImages, prompt);

                if (string.IsNullOrEmpty(text))
                {
                    throw new Exception("API returned empty text content.");
                }

                StatusText.Text = "Parsing results...";
                ParsedHoldings = ParseMarkdownTable(text);

                if (ParsedHoldings.Count == 0)
                {
                    string debugMsg = text.Length > 500 ? text.Substring(0, 500) + "..." : text;
                    throw new Exception($"AI did not return any valid holdings. Raw response start: {debugMsg}");
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Error processing screenshots: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<ParsedHolding> ParseMarkdownTable(string markdown)
        {
            var list = new List<ParsedHolding>();
            
            // Look for table rows
            // | Account Name | Company Name | Symbol | Quantity | Total Value |
            
            var lines = markdown.Split(new[] { "\\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            bool headerFound = false;
            
            foreach (var line in lines)
            {
                if (!line.Contains("|")) continue;
                
                // Check if header separator
                if (line.Contains("---"))
                {
                    headerFound = true;
                    continue;
                }
                
                if (!headerFound) continue; // Skip header row
                
                var parts = line.Split('|').Select(p => p.Trim()).ToList();

                // Remove empty leading/trailing parts caused by the pipes at start/end of line
                if (line.TrimStart().StartsWith("|") && parts.Count > 0) parts.RemoveAt(0);
                if (line.TrimEnd().EndsWith("|") && parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                
                // We expect 5 columns now: Account name, Company name, Symbol, Quantity, Total Value
                if (parts.Count >= 5)
                {
                    try
                    {
                        string account = parts[0];
                        string name = parts[1];
                        string symbol = parts[2];
                        string qtyStr = parts[3];
                        string valStr = parts[4];
                        
                        // Clean up
                        qtyStr = Regex.Replace(qtyStr, @"[^\d.]", "");
                        valStr = Regex.Replace(valStr, @"[^\d.]", "");
                        
                        if (double.TryParse(qtyStr, out double qty))
                        {
                            double.TryParse(valStr, out double val);

                            list.Add(new ParsedHolding
                            {
                                AccountName = account,
                                Name = name,
                                Symbol = symbol,
                                Quantity = qty,
                                Value = val,
                                RawText = line
                            });
                        }
                    }
                    catch { }
                }
            }
            
            return list;
        }
    }
}
