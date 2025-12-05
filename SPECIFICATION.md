# PortfolioWatch Detailed Specification

## 1. Overview
PortfolioWatch is a desktop application for tracking stock portfolios and watchlists. It features real-time price updates, portfolio value tracking, a floating window mode, and a customizable UI with theming support.

## 2. Core Data Models

### 2.1 Stock
- **Properties:**
  - `Symbol` (string): Stock ticker symbol.
  - `Name` (string): Company name.
  - `Price` (double): Current stock price.
  - `Change` (double): Daily price change in currency.
  - `ChangePercent` (double): Daily price change in percentage.
  - `PreviousClose` (double): Previous day's closing price.
  - `Shares` (double): Number of shares owned (user input).
  - `History` (List<double>): Historical price data for sparklines.
  - `DayProgress` (double): Progress of current price within day's range (0.0 to 1.0).
  - `HasEarningsFlag` (bool): Indicator for upcoming earnings.
  - `EarningsMessage` (string): Tooltip text for earnings flag.
  - `EarningsFlagColor` (Brush): Color for earnings flag.
  - `MarketValue` (decimal): `Price * Shares`.
  - `DayChangeValue` (decimal): `Change * Shares`.
  - `PortfolioPercentage` (double): Percentage of total portfolio value.
  - `IsUp` (bool): Derived from `Change >= 0`.

### 2.2 AppSettings
- **Properties:**
  - `Stocks` (List<Stock>): List of user's stocks.
  - `SortColumn` (string): Current sort column name.
  - `SortAscending` (bool): Sort direction.
  - `WindowTitle` (string): Custom title for the window.
  - `IsIndexesVisible` (bool): Visibility of market indexes.
  - `IsPortfolioMode` (bool): Toggle for portfolio tracking features.
  - `Theme` (AppTheme): Current theme (System, Light, Dark).
  - `WindowOpacity` (double): Window transparency level.
  - `StartWithWindows` (bool): Startup preference.
  - `IsFirstRun` (bool): Flag for first-time setup.

## 3. User Interface

### 3.1 Main Window
- **Style:** Frameless, transparent background, rounded corners (Radius 12).
- **Behavior:**
  - Draggable via any non-interactive area.
  - Resizable.
  - Auto-hides when not pinned and mouse leaves (unless keyboard focus is within).
  - Animates opacity on load.
  - Topmost window.
  - Not shown in taskbar.

#### 3.1.1 Header
- **Title:** Editable text box.
  - Default: "Watchlist".
  - Updates `WindowTitle` setting on lost focus.
  - Enter key clears focus.
- **Refresh Button:** Manually triggers data update.
- **Menu Button:** Opens context menu (see 3.3).
- **Close Button:** Hides the application (does not exit).

#### 3.1.2 Portfolio Summary (Visible in Portfolio Mode)
- **Total Value:** Sum of all `Stock.MarketValue`.
- **Graph:**
  - Sparkline showing aggregate portfolio history.
  - Dashed baseline for previous close.
  - Color-coded based on `IsPortfolioUp`.
- **Total Change:**
  - Percentage change.
  - Currency change.
  - Color-coded (Green/Red).

#### 3.1.3 Controls Bar
- **Portfolio Toggle:** Switch to enable/disable Portfolio Mode.
- **Indexes Toggle:** Button to show/hide market indexes section.

#### 3.1.4 Indexes List (Collapsible)
- **Items:** Displays major market indexes (e.g., S&P 500, Dow, Nasdaq).
- **Layout:**
  - Symbol (Bold).
  - Name (Small, truncated).
  - Sparkline graph.
  - Price.
  - Change % (Color-coded badge).
- **Interaction:** No delete button.

#### 3.1.5 Column Headers
- **Symbol:** Sort by Symbol.
- **Name:** Sort by Name.
- **Shares:** Label only (Portfolio Mode).
- **Value:** Sort by Market Value (Portfolio Mode).
- **Day $:** Sort by Day Change Value (Portfolio Mode).
- **Day %:** Sort by Change Percent.
- **Sort Indicators:** Arrows indicating sort direction.

#### 3.1.6 Stocks List
- **Items:** User-added stocks.
- **Layout:**
  - **Delete Button:** Visible on hover (Left side).
  - **Symbol/Name:**
    - Symbol (Bold).
    - Earnings Flag (if applicable).
    - Name (Small, truncated).
    - Pie Chart Icon (Portfolio Mode, visualizes `PortfolioPercentage`).
  - **Center:**
    - **Sparkline:** Historical trend (Standard Mode).
    - **Portfolio Data (Portfolio Mode):**
      - **Shares Input:** Editable text box.
        - "+" button overlay when empty/zero.
        - Updates on Enter or Lost Focus.
      - **Market Value:** Calculated value.
      - **Day Change Value:** Calculated change.
  - **Right:**
    - Price.
    - Change % (Color-coded badge).
- **Interactions:**
  - **Click:** Opens Google Search for stock.
  - **Delete:** Removes stock from list.
  - **Scroll:** Mouse wheel scrolling supported.

#### 3.1.7 Footer
- **Status Bar:** Displays messages (e.g., "Last updated...", errors).
- **Add Stock:**
  - **Search Box:** Text input for symbol/name.
  - **Popup:** Displays search results with live prices.
  - **Add Button (+):** Adds selected stock or text input if valid.

### 3.2 Floating Window (Mini Mode)
- **Purpose:** Minimalistic view (implementation details inferred from file existence, likely a smaller, always-on-top summary).
- **Behavior:**
  - Opacity changes on hover.
  - Drag to move.
  - Context menu support.

### 3.3 Context Menu
- **Start with Windows:** Toggle registry key for startup.
- **Theme:** Sub-menu (System, Light, Dark).
- **Opacity:** Slider (20% - 100%) with percentage display.
- **Import Data:** Load stocks from JSON file.
- **Export Data:** Save stocks to JSON file.
- **Reset:** Restore default settings and stocks.
- **About:** Opens GitHub repo.
- **Request Feature:** Opens GitHub issues.
- **Say Thanks:** Donation options (Venmo links).
  - $0 (Email).
  - $1, $3 (Venmo).
  - Custom Amount (Input Dialog).
- **Exit:** Fully shuts down the application.

### 3.4 Dialogs
- **Confirmation Window:** Custom styled "Yes/No" dialog.
- **Input Window:** Custom styled text input dialog with validation.

## 4. Features & Logic

### 4.1 Data Services
- **StockService:**
  - Fetches stock data (Prices, History, Search).
  - Updates prices every 60 seconds.
  - Updates earnings data every 4 hours.
  - **Mock/Real Data:** Implementation likely uses a public API (e.g., Yahoo Finance) or scrapes data.
- **SettingsService:**
  - Persists `AppSettings` to `AppData/PortfolioWatch/settings.json`.
  - Handles Import/Export logic.
  - Manages Windows Registry for startup.

### 4.2 Sorting
- **Modes:**
  - Symbol, Name, Change % (Standard).
  - Market Value, Day Change Value (Portfolio).
- **Logic:**
  - Portfolio sorts separate stocks with shares vs. without shares.
  - Remembers last sort order.

### 4.3 Portfolio Calculations
- **Total Value:** Sum of (Price * Shares).
- **Total Change:** Sum of (Change * Shares).
- **History:** Aggregated historical value of portfolio based on current share counts.
- **Day Progress:** Max day progress of held stocks.

### 4.4 Theming
- **Themes:** Light, Dark, System (follows OS).
- **Resources:** Dynamic resources for Brushes/Colors defined in `Styles.xaml` and theme files.

### 4.5 Search
- **Debounce:** 300ms delay on typing.
- **Results:**
  - Symbol + Name.
  - Live price and change % fetched for top 10 results.
- **Selection:** Adds stock to list, prevents duplicates.

## 5. Technical Details
- **Framework:** WPF (.NET).
- **MVVM:** CommunityToolkit.Mvvm.
- **Storage:** JSON file.
- **External Links:** Opens default browser for links/Venmo.
