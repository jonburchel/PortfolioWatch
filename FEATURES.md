# PortfolioWatch Features & Implementation Details

This document provides an exhaustive breakdown of the features, behaviors, and technical implementation details of the PortfolioWatch application (v1.0).

## 1. Core Architecture

*   **Framework**: WPF (Windows Presentation Foundation) targeting .NET 8.0.
*   **Pattern**: MVVM (Model-View-ViewModel) using `CommunityToolkit.Mvvm`.
*   **Entry Point**: `App.xaml.cs` handles application startup, single-instance enforcement (Mutex), theme application, and window initialization.
*   **Single Instance**: Enforces a single running instance using a named Mutex (`PortfolioWatch_Singleton_Mutex`). If a second instance is started, it brings the existing instance to the foreground and exits.

## 2. User Interface (UI) & UX

### 2.1. Windows & Navigation
The application consists of two synchronized windows:
1.  **Floating Window (`FloatingWindow.xaml`)**:
    *   **Role**: A small, always-on-top widget (pyramid icon) that acts as the anchor.
    *   **Behavior**:
        *   **Draggable**: Can be moved around the screen.
        *   **Sticky Positioning**: Syncs its position with the Main Window.
        *   **Hover**: Hovering over the icon temporarily shows the Main Window (peeking).
        *   **Click**: Toggles the "Pinned" state of the Main Window.
        *   **Context Menu**: Right-click provides access to application commands (Exit).
        *   **Opacity**: Dims when not interacting to reduce distraction.
2.  **Main Window (`MainWindow.xaml`)**:
    *   **Role**: The primary interface displaying the stock list and portfolio data.
    *   **Behavior**:
        *   **Auto-Hide**: Automatically hides when the mouse leaves the window (unless pinned).
        *   **Pinning**: When pinned (via clicking the Floating Window), it stays visible and ignores auto-hide logic.
        *   **Positioning**: Automatically positions itself relative to the Floating Window (Top-Right or Bottom-Right logic depending on screen space, though currently hardcoded to offset).
        *   **Animations**: Fade-in animation on load.

### 2.2. Theming
*   **Themes**: Supports Light, Dark, and System (follows Windows setting) themes.
*   **Implementation**: Uses ResourceDictionaries (`Themes/DarkTheme.xaml`, `Themes/LightTheme.xaml`) swapped dynamically in `App.xaml.cs`.
*   **Resources**: Defines brushes for backgrounds, text, borders, and semantic colors (Positive/Negative/Accent).

### 2.3. Tray Icon
*   **Library**: `Hardcodet.NotifyIcon.Wpf`.
*   **Features**:
    *   Shows application icon in the system tray.
    *   **Left-Click**: Shows/Activates the Main Window.
    *   **Context Menu**: Shared context menu with the Main Window (Settings, Exit, etc.).

## 3. Stock & Portfolio Management

### 3.1. Data Sources
*   **Quotes & Charts**: Scraped from Yahoo Finance (unofficial) via `StockService`.
*   **Search**: Yahoo Finance Autocomplete API.
*   **Earnings**: Scraped from Nasdaq API (unofficial).

### 3.2. Watchlist Features
*   **Add Stock**:
    *   Search bar with debounced autocomplete (300ms delay).
    *   Supports adding by Symbol.
    *   Prevents duplicate entries.
*   **Remove Stock**: Context menu option or delete button (hover behavior) on stock items.
*   **Sorting**:
    *   Sort by Symbol, Name, Change %, Day $ Change, Market Value.
    *   **Portfolio Sort Logic**: When sorting by value/gain, stocks with 0 shares are sorted separately at the bottom (by change %) to keep the portfolio view clean.
*   **Sparklines**:
    *   Visualizes price history for the selected range (1d, 5d, 1mo, etc.).
    *   **Color Coded**: Green if price > previous close, Red otherwise.
    *   **Baseline**: Dashed line indicating the previous close price.
    *   **Tooltip**: Hovering a sparkline shows the specific price, % change, and time for that data point.

### 3.3. Portfolio Mode
*   **Toggle**: "Portfolio Mode" switch in the UI.
*   **Functionality**:
    *   Allows entering "Shares" count for each stock.
    *   Calculates **Market Value** (Price * Shares).
    *   Calculates **Day Gain/Loss** ($ and %).
    *   **Total Portfolio Header**: Displays the aggregated total value and daily change of all holdings.
    *   **Portfolio Sparkline**: Aggregates the historical price action of all holdings into a single master chart.
    *   **Pie Chart**: Visualizes asset allocation (hidden if < 2 stocks with value).

### 3.4. Earnings Data
*   **Indicators**:
    *   **Upcoming**: Shows a calendar icon if earnings are within the next 7 days.
    *   **Beat/Miss**: Shows a green arrow (Beat) or red arrow (Miss) based on the last reported EPS vs Estimate.
*   **Data**: Fetched asynchronously to avoid blocking UI.

## 4. Data Persistence & Settings

### 4.1. Settings File
*   **Location**: `%AppData%\PortfolioWatch\settings.json`.
*   **Format**: JSON.
*   **Content**:
    *   Window positions and dimensions.
    *   Watchlist (Symbols, Shares).
    *   User preferences (Theme, Opacity, Sort Order, Range).
    *   Update settings (Last check time, Snooze time).

### 4.2. Import/Export
*   **Export**: Saves the current watchlist and settings to a user-selected JSON file.
*   **Import**: Loads watchlist and settings from a JSON file, merging/overwriting current state.

### 4.3. Start with Windows
*   **Implementation**: Uses Windows Registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
*   **Toggle**: Checkbox in the Settings menu.

## 5. Auto-Update System

### 5.1. Update Check
*   **Source**: GitHub Releases API (latest release).
*   **Frequency**:
    *   **Startup**: Checks immediately on app launch.
    *   **Daily**: Checks every 24 hours if the app is left running.
    *   **Manual**: "Check for updates" button in the context menu.
*   **Logic**: Compares `Assembly.GetEntryAssembly().GetName().Version` with the version tag from GitHub (stripping 'v' prefix).

### 5.2. Update Application
*   **Verification**:
    *   Downloads `SHA256SUMS` from the release assets.
    *   Calculates SHA256 hash of the downloaded executable.
    *   Aborts if hashes do not match.
*   **Installation**:
    1.  Downloads the new `.exe` to a temporary location.
    2.  Generates a temporary batch script (`update.bat`).
    3.  **Batch Script Logic**:
        *   Waits for the current process to exit (looping check).
        *   Overwrites the old `.exe` with the new one.
        *   Restarts the application.
        *   Deletes itself.
    4.  App launches the batch script and shuts down.

## 6. Technical Components

### 6.1. Services
*   **`StockService`**:
    *   `HttpClient` management (headers, timeouts).
    *   Parsing logic for Yahoo Finance HTML/JSON and Nasdaq JSON.
    *   Parallel data fetching for quotes (using `Task.WhenAll`).
*   **`SettingsService`**:
    *   Serialization/Deserialization of `AppSettings`.
    *   Registry management for startup.
*   **`UpdateService`**:
    *   GitHub API interaction.
    *   File download and hash verification.
    *   Batch script generation.

### 6.2. ViewModels
*   **`MainViewModel`**:
    *   **State**: Manages `ObservableCollection<Stock>`, search results, and UI state (Busy, Portfolio Mode, etc.).
    *   **Timers**:
        *   `_timer` (60s): Refreshes stock prices.
        *   `_earningsTimer` (4h): Refreshes earnings data.
    *   **Commands**: RelayCommands for all UI interactions (Refresh, Add, Remove, Sort, etc.).

### 6.3. Converters
*   **`SparklineConverter`**: Converts a `List<double>` of prices into a `StreamGeometry` for drawing the graph.
*   **`PieSliceConverter`**: Converts a percentage (0-1) into a pie slice `StreamGeometry`.
*   **`ValueConverters`**: Formatting helpers (Zero to Empty String).
*   **`VisibilityConverters`**: Boolean/Double to Visibility logic.

## 7. External Dependencies
*   `CommunityToolkit.Mvvm`: MVVM pattern support (ObservableObject, RelayCommand).
*   `Hardcodet.NotifyIcon.Wpf`: System tray icon support.
*   `Newtonsoft.Json`: JSON parsing (for API responses).
*   `System.Text.Json`: JSON serialization (for settings).
*   `HtmlAgilityPack`: HTML parsing (for Yahoo Finance scraping).
