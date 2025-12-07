# PortfolioWatch

PortfolioWatch is a modern, lightweight desktop widget for tracking stock prices and portfolio performance in real-time. Built with .NET 9 and WPF, it features a clean, unobtrusive UI that sits on your desktop.

<img src="PortfolioWatch/pyramid.png" width="150" alt="PortfolioWatch Screenshot">

## Features

PortfolioWatch offers a comprehensive set of tools for tracking the market and your personal holdings, including real-time updates, portfolio performance tracking, and extensive customization options.

👉 **[View Full Feature Breakdown](FEATURES.md)**

## Installation

1. The app requires the [.NET 9 Runtime for Windows](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-9.0.308-windows-x64-installer). Install it first if necessary. 
1. Download `PortfolioWatch.exe` from the Assets below. 
1. Run the executable (no installer required).

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
