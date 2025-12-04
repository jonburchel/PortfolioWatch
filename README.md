# PortfolioWatch

PortfolioWatch is a modern, lightweight desktop widget for tracking stock prices and portfolio performance in real-time. Built with .NET 9 and WPF, it features a clean, unobtrusive UI that sits on your desktop.

<img src="PortfolioWatch/pyramid.png" width="150" alt="PortfolioWatch Screenshot">

## Features

PortfolioWatch offers a comprehensive set of tools for tracking the market and your personal holdings, including real-time updates, portfolio performance tracking, and extensive customization options.

👉 **[View Full Feature Breakdown](FEATURES.md)**

## Installation

1.  Download the latest release from the [Releases](https://github.com/jonburchel/PortfolioWatch/releases/latest) page.
3.  Run `PortfolioWatch.exe` (or `PortfolioWatch.with.NET.9.0.exe`, if you don't already have the [.NET 9 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) installed).

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

# Alternately, you can run a publish script that publishes a fully self-contained, large .exe, and a small one that is dependent on the .NET Framework 9.0 being locally installed
powershell -ExecutionPolicy Bypass -File publish.ps1 
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
