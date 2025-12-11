# Azure Web App Setup Instructions

This guide will help you set up the `pw-service` web API on Azure using the Free Tier.

## Prerequisites
- An Azure Account (Create one for free at [azure.com](https://azure.microsoft.com/free/))
- Azure CLI installed (or use the Azure Portal)

## Step 1: Create the Web App (Free Tier)

1.  **Log in to Azure Portal** (https://portal.azure.com).
2.  Search for **"App Services"** and click **Create** -> **Web App**.
3.  **Basics Tab**:
    -   **Subscription**: Select your subscription.
    -   **Resource Group**: Create a new one (e.g., `PortfolioWatch-RG`).
    -   **Name**: Enter a unique name (e.g., `pw-service-yourname`). This will be part of your URL (`https://pw-service-yourname.azurewebsites.net`).
    -   **Publish**: Select **Code**.
    -   **Runtime stack**: Select **.NET 8 (LTS)** (or .NET 9 if available and you are using it, but .NET 8 is standard).
    -   **Operating System**: **Windows** (Free tier is often easier to find on Windows plans, but Linux is fine too if F1 is available).
    -   **Region**: Select a region near you (e.g., `East US`).
    -   **Pricing Plan**:
        -   Click **Change size** (or "Explore pricing plans").
        -   Select **Free F1** (Shared infrastructure). This is completely free.
4.  Click **Review + create**, then **Create**.

### Troubleshooting: "Operation cannot be completed without additional quota"
If you see this error, it means the region you selected (e.g., East US) is out of capacity for Free Tier instances or your subscription has a restriction.
**Solution:**
1.  Go back to the **Basics** tab.
2.  Change the **Region** to something else (e.g., **Central US**, **West US 2**, **North Europe**, or **East US 2**).
3.  You may need to create a new Resource Group in that new region as well.
4.  Try creating the app again.

## Step 2: Configure Environment Variables

1.  Once the resource is created, go to the **App Service** page.
2.  In the left menu, under **Settings**, click **Environment variables**.
3.  Click **Add** (or "New application setting").
4.  **Name**: `GEMINI_API_KEY`
5.  **Value**: `<your_key>`.
6.  Click **Apply**, then **Apply** again (or **Confirm**) to save the changes. The app will restart.

## Step 3: Deploy the Code

You can deploy using Visual Studio, VS Code, or the Azure CLI. Here is how to do it via CLI from your project folder.

1.  Open a terminal in the `pw-service` folder.
2.  Login to Azure:
    ```bash
    az login
    ```
3.  Deploy the app:
    ```bash
    dotnet publish -c Release -o ./publish
    cd publish
    Compress-Archive -Path * -DestinationPath ../publish.zip
    cd ..
    az webapp deployment source config-zip --resource-group PortfolioWatch-rg --name portfolio-watch --src publish.zip
    ```
    *(Replace `PortfolioWatch-RG` and `pw-service-yourname` with the values you used in Step 1).*

    **Alternative: VS Code**
    1.  Install the **Azure App Service** extension.
    2.  Right-click the `pw-service` folder in VS Code.
    3.  Select **Deploy to Web App...**.
    4.  Select your subscription and the Web App you created.
    5.  Click **Deploy**.

## Step 4: Update the Client

1.  Once deployed, copy your Web App URL (e.g., `https://pw-service-yourname.azurewebsites.net/`).
2.  Open `PortfolioWatch/Services/GeminiService.cs` in your local project.
3.  Update the `ServiceUrl` constant:
    ```csharp
    private const string ServiceUrl = "https://pw-service-yourname.azurewebsites.net/analyze";
    ```
4.  Rebuild and run your PortfolioWatch client.
