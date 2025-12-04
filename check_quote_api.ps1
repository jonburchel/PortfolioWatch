$symbols = "MSFT,AAPL"
$url = "https://query1.finance.yahoo.com/v7/finance/quote?symbols=$symbols"
$response = Invoke-RestMethod -Uri $url -Method Get
$response.quoteResponse.result | ConvertTo-Json -Depth 5
