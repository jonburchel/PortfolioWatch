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
        *   **Portfolio Decorations**: When in Portfolio Mode, the widget can overlay real-time data on top of the icon:
            *   **Intraday %**: Displays the daily percentage change (Color-coded Green/Red).
            *   **Total Value**: Displays the current total portfolio value.
            *   **Mini Sparkline**: A small intraday chart visualizing performance relative to the previous close.
            *   **Visibility**: These elements are configurable via Settings and appear over a darkened scrim for contrast.
2.  **Main Window (`MainWindow.xaml`)**:
    *   **Role**: The primary interface displaying the stock list and portfolio data.
    *   **Behavior**:
        *   **Auto-Hide**: Automatically hides when the mouse leaves the window (unless pinned).
        *   **Pinning**: When pinned (via clicking the Floating Window), it stays visible and ignores auto-hide logic.
        *   **Positioning**: Automatically positions itself relative to the Floating Window (Top-Right or Bottom-Right logic depending on screen space, though currently hardcoded to offset).
        *   **Animations**: Fade-in animation on load.
        *   **Sizing**: Enforces a minimum size of 550x550 pixels to ensure usability.

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

### 3.5. Notifications & Flags
The application uses a system of emoji-based flags to alert the user to significant events or data points for each stock. Each flag includes a "Signal Strength" rating (0-10) overlaid on the icon to indicate the magnitude or conviction of the signal.

*   **Earnings Flag**:
    *   **Icons**:
        *   **Upcoming (📅)**: Earnings report is scheduled within the next 7 days.
        *   **Beat (🎯)**: Most recent earnings report beat analyst estimates.
        *   **Miss (📉)**: Most recent earnings report missed analyst estimates.
    *   **Signal Strength**:
        *   **Upcoming**: Fixed at **6.0/10** (Neutral/Unknown magnitude).
        *   **Beat/Miss**: Calculated based on the **Earnings Surprise %**.
            *   Scale: 2% surprise = 2.0/10, scaling up to 20% surprise = 10.0/10.
            *   Logic: Larger surprises typically drive larger price moves.

*   **News Flag**:
    *   **Icon**: **Breaking News (📰)**.
    *   **Trigger**: Indicates recent news articles are available for the stock.
    *   **Filtering**: The system automatically filters out low-quality sources and "junk" news to ensure only relevant headlines trigger the flag.

*   **Options Flag**:
    *   **Icons**:
        *   **Bullish (🐂)**: Positive directional confidence.
        *   **Bearish (🐻)**: Negative directional confidence.
    *   **Trigger**: Requires unusual volume/gamma AND an options impact date within 7 days.
    *   **Logic & Signal Interpretation**:
        The signal is derived from a "tug-of-war" between two primary forces:
        1.  **Max Pain (Magnet Pull)**: The price point at which the most options contracts expire worthless. Market Makers are incentivized to pin the stock price near this level, creating a "magnetic" pull.
        2.  **Flow Sentiment**: The ratio of Call vs. Put volume, representing active trader sentiment.
        
        The final signal is a weighted average: `(MagnetPull * 0.4) + (FlowSentiment * 0.6)`.
        
        *   **Aligned Signals**: When both forces agree, the trend is considered strong.
            *   **Expiration Warning**: If expiration is imminent (<= 3 days), this alignment is interpreted as a "Magnet" or "Pin" rather than an infinite trend.
        *   **Conflicting Signals**:
            *   **Flow Overpowers Max Pain**: Traders are betting heavily against the house.
                *   **Volatility Trap**: If expiration is imminent, aggressive flow fighting Max Pain suggests the price will struggle to break out and may get stuck near the current level.
                *   **Standard**: Max Pain acts as a temporary drag/prop. Once expiration passes, the stock may surge/drop as pressure releases.
            *   **Max Pain Overpowers Flow**: Market Maker incentives are too strong for current volume.
                *   **Gravity Wins**: If expiration is imminent, dealers are successfully pinning the price. Volatility will be crushed.
                *   **Standard**: The price is being pulled toward Max Pain until expiration.
    *   **Signal Strength**:
        *   **Metric**: The raw directional confidence score (-0.4 to +0.4 typical range) is scaled to a **0-10 rating**.
        *   **Calculation**: `Signal Strength = Min(|Directional Confidence| * 25, 10.0)`.
        *   **Expiration Dampening**: If expiration is imminent (<= 3 days) AND the price is already within 1.5% of the Max Pain target, the signal strength is **reduced by 70%**. This reflects that the "move" is effectively over (price is pinned), even if the structural forces remain high.
        *   **Powder Keg Override**: If the raw signal strength exceeds **9.0** AND expiration is imminent, the system flags a **"Critical Mass"** event. This overrides the standard implication text to warn of a potential "Gamma Squeeze" or violent move, as the options structure is overloaded.
        *   **Visuals**: Overlaid text color indicates direction (Green = Bullish, Red = Bearish).

*   **Insider Flag**:
    *   **Icon**: **Insider Activity (💼)**.
    *   **Trigger**: Significant net insider buying or selling.
    *   **Signal Strength**:
        *   **Buying (Accumulation)**: High conviction signal.
            *   Scale: Logarithmic scale from $50k (2.0/10) to $10M (10.0/10).
            *   Logic: Real money buying is always a strong signal.
        *   **Selling (Distribution)**: Adaptive signal based on Market Cap.
            *   Scale: Logarithmic scale relative to company size.
            *   Thresholds: A $10M sell is a 10/10 signal for a small cap, but might be a 2/10 for a mega-cap.
            *   Logic: Executives sell for many reasons (taxes, diversification), so the threshold for a "warning" is much higher than for buying.

*   **RVOL Flag**:
    *   **Icon**: **High Volume (🏦)**.
    *   **Trigger**: Relative Volume (RVOL) > 1.5x.
    *   **Signal Strength**:
        *   Scale: Linear scale from 1.5x (2.0/10) to 5.0x (10.0/10).
        *   Logic: Higher relative volume indicates institutional participation and validates the price move.
    *   **Color**: Green if price is up, Red if price is down.

### 3.6. Tabbed Portfolio
*   **Overview**: The application supports multiple portfolios organized via tabs, allowing users to categorize holdings (e.g., "Long Term", "Speculative", "Tech Sector").
*   **Management**:
    *   **Create**: Click the `+` button in the tab header to create a new empty portfolio tab.
    *   **Rename**: Double-click a tab header to enter edit mode. Press Enter or click away to save.
    *   **Delete**: Hovering over a tab reveals an `x` button. Deleting a tab requires confirmation if it contains stocks.
    *   **Reorder**: Tabs can be reordered via drag-and-drop. Visual indicators show the drop target.
    *   **Duplicate**: Right-click a tab to duplicate it (including all stocks and share counts).
*   **Behaviors**:
    *   **Include in Total**: Each tab has a checkbox to "Include in Total". Unchecking this excludes the tab's value from the global "Total Portfolio" aggregation.
    *   **Per-Tab Visualization**: Each tab features its own mini pie chart in the header, visualizing the asset allocation within that specific portfolio.
    *   **Scrolling**: The tab bar supports horizontal scrolling with dynamic arrow buttons that appear when tabs overflow the available width.

### 3.7. Tax Status Allocation
*   **Overview**: Provides a high-level view of the portfolio's tax exposure by categorizing assets into tax buckets (e.g., Taxable, Roth, Traditional IRA).
*   **Configuration**:
    *   **Per-Tab Settings**: Each portfolio tab can be assigned a specific tax allocation profile (e.g., "100% Roth" or "50% Taxable / 50% Traditional").
    *   **Edit Dialog**: Accessed via the context menu on a tab header, allowing users to define custom percentage splits across various tax status types.
*   **Aggregation Logic**:
    *   The application calculates a global tax allocation by weighting each tab's allocation against the total market value of the stocks contained within that tab.
    *   **Example**: If Tab A ($10k) is 100% Roth and Tab B ($10k) is 100% Taxable, the global allocation is 50% Roth / 50% Taxable.
*   **Visualization**:
    *   **Aggregate Chart**: A dedicated pie chart in the main dashboard visualizes the total portfolio's tax breakdown.

### 3.8. Merged Portfolio View
*   **Toggle**: "Merged View" checkbox in the main UI.
*   **Functionality**:
    *   **Aggregation**: Dynamically combines stocks from all tabs that have "Include in Total" checked.
    *   **Grouping**: Stocks with the same symbol across different tabs are grouped into a single entry, with their share counts summed.
    *   **Visuals**:
        *   Hides the tab headers to reinforce the unified nature of the view.
        *   Displays a "Merged View (Read-Only)" banner at the top of the list.
    *   **Read-Only**: Share counts in this view are read-only. To modify holdings, users must switch back to the specific tab where the stock resides.
    *   **Import Behavior**: Importing data while in Merged View automatically disables the view and switches to the imported tab (or the first valid tab) to prevent confusion.

### 3.9. CUSIP Support
*   **Overview**: The application supports tracking private CUSIPs (often used for private equity or specific fund classes) by mapping them to public equivalent funds.
*   **Mechanism**:
    *   **AI Resolution**: When a CUSIP is encountered, the system uses Gemini AI to identify a publicly traded fund that tracks the same underlying assets or index.
    *   **Conversion Ratio**: The system calculates a "Tracking Ratio" to equate the private CUSIP's value to the public fund's share price. This ensures the portfolio value remains accurate even though the tracking symbol differs.
*   **Entry Points**:
    *   **Manual Search**: Entering a valid 9-character CUSIP in the main search bar triggers the resolution process. Users are prompted to confirm the fund name and quantity/value to establish the tracking baseline.
    *   **Screenshot Import**: The screenshot import tool automatically detects CUSIPs in the image text. It attempts to resolve them in bulk during the import process, flagging any that cannot be tracked.

### 3.10. Screenshot Import
*   **Overview**: Allows users to bulk import holdings by pasting screenshots of their brokerage accounts.
*   **Workflow**:
    *   **Input**: Users paste images (Ctrl+V) into the import window.
    *   **Processing**: Images are sent to a secure, stateless Azure service powered by Gemini AI.
    *   **Extraction**: The AI identifies Account Names, Symbols/Fund Names, Share Counts, and Values.
*   **Capabilities**:
    *   **Multi-Account Support**: Automatically detects different accounts in the screenshots and creates separate portfolio tabs for each (e.g., "Fidelity - Individual", "Vanguard - Roth IRA").
    *   **CUSIP Detection**: Identifies private CUSIPs and attempts to resolve them to public equivalents (see Section 3.9).
    *   **Privacy**: The processing is anonymous and stateless; no images or data are logged or stored on the server.
*   **Limitations**:
    *   **Duplicates**: Does not automatically deduplicate holdings if the same position is pasted multiple times.
    *   **Accuracy**: Relies on OCR and AI interpretation; results should be verified by the user.

## 4. Data Persistence & Settings

### 4.1. Settings File
*   **Location**: `%AppData%\PortfolioWatch\settings.json`.
*   **Format**: JSON.
*   **Content**:
    *   Window positions and dimensions.
    *   Portfolios (Name, Stocks, Share Counts, "Include in Total" preference).
    *   User preferences (Theme, Opacity, Sort Order, Range).
    *   Update settings (Last check time, Snooze time).

### 4.2. Import/Export
*   **Export**: Saves the current watchlist and settings to a user-selected JSON file.
    *   **Standard Export**: Exports the portfolio exactly as is.
    *   **Normalized Export**: Allows exporting the portfolio with share counts adjusted to match a target total value (e.g., $1,000,000) while maintaining the current asset allocation percentages. Useful for modeling or sharing strategies without revealing actual wealth.
        *   **Tax Allocations**: Includes tax allocation details in the export. If exported from Merged View, it uses the aggregated tax allocations; otherwise, it uses the active tab's allocations.
    *   **Metadata**: Exports include the Portfolio Name (`WindowTitle`) to ensure the context is preserved upon import.
*   **Import**: Loads watchlist and settings from a JSON file, merging/overwriting current state and restoring the Portfolio Name.

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
