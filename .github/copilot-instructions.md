# PortfolioWatch Developer Guide & Copilot Instructions

This document serves as the primary source of truth for navigating, understanding, and modifying the PortfolioWatch codebase. It is designed to help AI assistants and developers efficiently locate relevant files and understand the project's workflow.

## 1. Environment & Workflow Preferences

*   **Terminal**: Use **Git Bash** for all terminal commands.
*   **Path Style**: Always use forward slashes `/` for file paths in commands.
*   **Validation Workflow**: After making code changes, **ALWAYS** perform the following steps to validate:
    1.  Run the build/publish script:
        ```bash
        ./publish.ps1
        ```
    2.  Launch the generated binary to verify the fix/feature:
        ```bash
        "F:/scratch_projects/portfolio-watch/Releases/FrameworkDependent/PortfolioWatch.exe"
        ```
    *Note: This ensures that the application builds correctly and runs in a production-like environment immediately after changes.*

## 2. Project Navigation Map

### Core Application Logic
*   **Entry Point**: `PortfolioWatch/App.xaml.cs`
    *   *Responsibility*: Startup logic, Single Instance Mutex, Theme switching, Exception handling.
*   **Main Logic Hub**: `PortfolioWatch/ViewModels/MainViewModel.cs`
    *   *Responsibility*: This is the "brain" of the application. It handles data fetching timers, UI commands (Add/Remove/Sort), and manages the `Stocks` collection. **Start here for most feature requests.**
*   **Data Models**: `PortfolioWatch/Models/`
    *   `Stock.cs`: The core entity. Contains properties for Price, Change, Shares, MarketValue, and Flags (Earnings, News, etc.).
    *   `AppSettings.cs`: Defines the structure of the `settings.json` file.

### User Interface (Views)
*   **Main Dashboard**: `PortfolioWatch/Views/MainWindow.xaml`
    *   *Content*: The main grid, stock list (DataGrid/ListView), portfolio summary headers, and tab control.
*   **Floating Widget**: `PortfolioWatch/Views/FloatingWindow.xaml`
    *   *Content*: The small "Pyramid" icon window that stays on top. Handles the "Peeking" logic.
*   **Dialogs**:
    *   `InputWindow.xaml`: Generic text input (e.g., for renaming tabs).
    *   `ConfirmationWindow.xaml`: Generic Yes/No prompts.
    *   `ImportPromptWindow.xaml`: Specific dialog for handling import conflicts.

### Data & Services
*   **Stock Data Fetching**: `PortfolioWatch/Services/StockService.cs`
    *   *Responsibility*: Scrapes Yahoo Finance and Nasdaq. Handles HTTP requests and HTML parsing.
*   **Persistence**: `PortfolioWatch/Services/SettingsService.cs`
    *   *Responsibility*: Loads/Saves `settings.json` and manages Registry keys for "Start with Windows".
*   **Updates**: `PortfolioWatch/Services/UpdateService.cs`
    *   *Responsibility*: Checks GitHub Releases and handles the self-update process.

### Visual Helpers (Converters)
Located in `PortfolioWatch/Converters/`. Key files:
*   `SparklineConverter.cs`: Draws the mini-charts in the stock list.
*   `PieSliceConverter.cs`: Draws the pie charts for asset allocation.
*   `TaxAllocationsToPieChartConverter.cs`: Aggregates tax status data for the main chart.

## 3. Feature Reference

For a detailed breakdown of specific features and their behaviors, refer to **[FEATURES.md](../FEATURES.md)**. This file contains exhaustive details on:
*   **Stock Flags**: Logic for Earnings, News, Options, Insider, and RVOL flags.
*   **Portfolio Logic**: How Market Value, Day Gain, and Totals are calculated.
*   **Tab System**: Behavior of the multi-portfolio tab interface.
*   **Tax Allocation**: How tax status is tracked and visualized.

## 4. Development Guidelines

*   **MVVM Pattern**: Strictly adhere to MVVM.
    *   **Logic** goes in `ViewModels`.
    *   **UI Structure** goes in `Views` (XAML).
    *   **Code-Behind** (`.xaml.cs`) should be minimal (only for pure UI logic like window dragging or specific event handling that binding can't cover).
*   **Async/Await**: Use `async` for all I/O operations (Network, File System) to keep the UI responsive.
*   **Theming**: Use `DynamicResource` for colors (e.g., `{DynamicResource PrimaryTextBrush}`) to ensure compatibility with Light/Dark themes.

## 5. Key File Locations

| Component | File Path |
| :--- | :--- |
| **Main ViewModel** | `PortfolioWatch/ViewModels/MainViewModel.cs` |
| **Stock Model** | `PortfolioWatch/Models/Stock.cs` |
| **Stock Service** | `PortfolioWatch/Services/StockService.cs` |
| **Main Window XAML** | `PortfolioWatch/Views/MainWindow.xaml` |
| **App Entry** | `PortfolioWatch/App.xaml.cs` |
| **Build Script** | `publish.ps1` |
