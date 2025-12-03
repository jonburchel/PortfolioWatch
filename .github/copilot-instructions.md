# PortfolioWatch Technical Implementation Guide

This document provides a technical overview of the PortfolioWatch project to assist with future development and maintenance.

## Project Overview

PortfolioWatch is a Windows Presentation Foundation (WPF) desktop application built with .NET 9.0. It is designed to track stock portfolios and market indices, providing real-time updates and visualizations.

## Architecture

The project follows the **Model-View-ViewModel (MVVM)** architectural pattern, utilizing the `CommunityToolkit.Mvvm` library for efficient state management and command handling.

### Key Components

1.  **Models (`PortfolioWatch/Models/`)**
    *   `Stock.cs`: Represents a stock or index entity. Contains properties for symbol, price, change, shares owned, etc.
    *   `AppSettings.cs`: Defines the application's configuration and user preferences.

2.  **ViewModels (`PortfolioWatch/ViewModels/`)**
    *   `MainViewModel.cs`: The primary ViewModel for the application. It orchestrates data fetching, handles user interactions, and exposes observable collections for the UI.

3.  **Views (`PortfolioWatch/Views/`)**
    *   `MainWindow.xaml`: The main application window.
    *   `FloatingWindow.xaml`: A compact, always-on-top window for quick monitoring.
    *   `InputWindow.xaml` & `ConfirmationWindow.xaml`: Dialogs for user input and confirmation.

4.  **Services (`PortfolioWatch/Services/`)**
    *   `StockService.cs`: Handles fetching stock data from external APIs (e.g., Yahoo Finance).
    *   `SettingsService.cs`: Manages loading and saving application settings.

5.  **Converters (`PortfolioWatch/Converters/`)**
    *   `PieSliceConverter.cs`: Generates a dynamic pie slice geometry to visualize a stock's weight in the portfolio.
    *   `SparklineConverter.cs`: Converts historical price data into a sparkline geometry.
    *   `ValueConverters.cs` & `VisibilityConverters.cs`: Various helpers for formatting and UI logic.

6.  **Themes (`PortfolioWatch/Themes/`)**
    *   Supports `DarkTheme.xaml` and `LightTheme.xaml` for user interface customization.

## Technical Details

*   **Framework:** .NET 9.0 (Windows)
*   **UI Framework:** WPF
*   **Dependencies:**
    *   `CommunityToolkit.Mvvm`: MVVM implementation.
    *   `Emoji.Wpf`: Emoji support in UI.
    *   `Hardcodet.NotifyIcon.Wpf`: System tray icon support.
*   **Build Configuration:**
    *   Configured for single-file, self-contained publication (`win-x64`).

## Key Features Implementation

*   **Portfolio Visualization:** The `PieSliceConverter` calculates a `StreamGeometry` based on the stock's percentage of the total portfolio value. This is bound to a path in the `MainWindow`'s stock list.
*   **Real-time Updates:** The `MainViewModel` uses a timer to periodically refresh stock data via the `StockService`.
*   **Data Persistence:** Settings and portfolio data are persisted to a local JSON file managed by the `SettingsService`.

## Development Guidelines

*   **MVVM:** Always implement business logic in ViewModels and keep code-behind files minimal.
*   **Binding:** Use data binding for all UI updates. Avoid direct UI manipulation.
*   **Async/Await:** Use asynchronous programming for I/O bound operations (e.g., network requests) to keep the UI responsive.
*   **Theming:** Ensure all new UI elements use dynamic resources for colors to support theme switching.
