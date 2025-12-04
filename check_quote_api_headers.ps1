$symbols = "MSFT,AAPL"
$url = "https://query1.finance.yahoo.com/v7/finance/quote?symbols=$symbols"
$headers = @{
    "User-Agent" = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
}
$response = Invoke-RestMethod -Uri $url -Method Get -Headers $headers
$response.quoteResponse.result | ConvertTo-Json -Depth 5
