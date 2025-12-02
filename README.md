# PortfolioWatch

PortfolioWatch is a modern, lightweight desktop widget for tracking stock prices and portfolio performance in real-time. Built with .NET 9 and WPF, it features a clean, unobtrusive UI that sits on your desktop.

<img src="PortfolioWatch/pyramid.png" width="150" alt="PortfolioWatch Screenshot">

## Features

*   **Real-time Tracking:** Monitor stock prices and daily changes.
*   **Portfolio Mode:** Track your total portfolio value and performance.
*   **Interactive Charts:** View intraday price movements with sparkline charts.
*   **Search:** Easily find and add stocks by symbol or name.
*   **Customizable:** Toggle index visibility, sort by various metrics, and more.
*   **Always on Top:** Keep an eye on the market while you work (optional).

## Installation

1.  Download the latest release from the [Releases](https://github.com/jonburchel/PortfolioWatch/releases/tag/release) page.
3.  Run `PortfolioWatch.exe` (or `PortfolioWatch_with_.NET_full.exe`, if you don't already have the .NET 9 runtime installed).

## Building from Source

Requirements:
*   [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

```bash
# Clone the repository
git clone https://github.com/jonburchel/portfolio-watch.git

# Navigate to the project directory
cd portfolio-watch

# Build the project
dotnet build -c Release

# Publish (Self-contained single file)
dotnet publish -c Release
```

## Technologies Used

*   **C# / .NET 9**
*   **WPF (Windows Presentation Foundation)**
*   **CommunityToolkit.Mvvm** for MVVM pattern implementation
*   **Hardcodet.NotifyIcon.Wpf** for system tray integration

## Authorship

[Gemini 3 Pro](https://gemini.google.com/) with [Cline](https://cline.bot) in [VS Code](https://code.visualstudio.com/) wrote all of this code based on specifications provided by [Jon Burchel](https://github.com/jonburchel).

## License

This project is licensed under the MIT License.
