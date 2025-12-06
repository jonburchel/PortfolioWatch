using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System;
using System.Linq;
using System.Diagnostics;

namespace PortfolioWatch.Probe
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Probe...");
            
            var process = Process.GetProcessesByName("PortfolioWatch").FirstOrDefault();
            if (process == null)
            {
                Console.WriteLine("PortfolioWatch process not found. Please ensure the application is running.");
                return;
            }

            Console.WriteLine($"Attached to process ID: {process.Id}");

            using (var automation = new UIA3Automation())
            {
                var app = Application.Attach(process);
                
                // First, find the FloatingWindow to open the main window
                Console.WriteLine("Looking for FloatingWindow ('PortfolioWatch')...");
                var floatingWindow = app.GetAllTopLevelWindows(automation)
                    .FirstOrDefault(w => w.Title.Equals("PortfolioWatch"));

                if (floatingWindow != null)
                {
                    Console.WriteLine("Found FloatingWindow. Attempting to click to open MainWindow...");
                    // Try to find the border or just click the window center
                    var border = floatingWindow.FindFirstDescendant(cf => cf.ByAutomationId("MainBorder"));
                    if (border != null)
                    {
                        Console.WriteLine("Found MainBorder by AutomationId. Clicking...");
                        border.Click();
                    }
                    else
                    {
                        Console.WriteLine("MainBorder not found by AutomationId. Dumping descendants:");
                        foreach (var descendant in floatingWindow.FindAllDescendants())
                        {
                            Console.WriteLine($" - Type: {descendant.ControlType}, Name: '{descendant.Name}', Id: '{descendant.AutomationId}'");
                        }
                        Console.WriteLine("Clicking window center...");
                        floatingWindow.Click();
                    }
                    
                    // Wait a bit for animation/window creation
                    System.Threading.Thread.Sleep(3000);
                }

                // Find the specific window with retries
                Console.WriteLine("Looking for window with title containing 'Portfolio Watchlist'...");
                FlaUI.Core.AutomationElements.Window window = null;
                
                for (int i = 0; i < 5; i++)
                {
                    window = app.GetAllTopLevelWindows(automation)
                        .FirstOrDefault(w => w.Title.Contains("Portfolio Watchlist"));
                    
                    if (window != null) break;
                    
                    Console.WriteLine($"Attempt {i+1}: Target window not found. Waiting...");
                    System.Threading.Thread.Sleep(1000);
                }

                if (window == null)
                {
                    Console.WriteLine("Target window not found after retries. Available windows:");
                    foreach (var w in app.GetAllTopLevelWindows(automation))
                    {
                        Console.WriteLine($" - Title: '{w.Title}', AutomationId: '{w.AutomationId}'");
                    }
                    return;
                }

                Console.WriteLine($"Successfully found window: '{window.Title}'");

                // Search for the ScrollViewer
                Console.WriteLine("Searching for MainScrollViewer...");
                var scrollViewer = window.FindFirstDescendant(cf => cf.ByAutomationId("MainScrollViewer"));
                
                if (scrollViewer != null)
                {
                    Console.WriteLine($"Found MainScrollViewer. Bounds: {scrollViewer.BoundingRectangle}");
                    
                    // Find scrollbars within the ScrollViewer
                    var scrollBars = scrollViewer.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ScrollBar));
                    Console.WriteLine($"Found {scrollBars.Length} ScrollBars inside MainScrollViewer.");

                    foreach (var sb in scrollBars)
                    {
                        Console.WriteLine("--------------------------------------------------");
                        Console.WriteLine($"ScrollBar Found:");
                        Console.WriteLine($"  AutomationId: {sb.AutomationId}");
                        Console.WriteLine($"  BoundingRectangle: {sb.BoundingRectangle}");
                        Console.WriteLine($"  Width: {sb.BoundingRectangle.Width}");
                        
                        if (sb.BoundingRectangle.Width <= 5 && sb.BoundingRectangle.Width > 0)
                        {
                            Console.WriteLine("  [SUCCESS] ScrollBar width is narrow (<= 5px).");
                        }
                        else
                        {
                            Console.WriteLine("  [WARNING] ScrollBar width is NOT narrow.");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("MainScrollViewer not found by AutomationId.");
                }
            }
        }
    }
}
