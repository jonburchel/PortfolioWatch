$query = "MSFT"
$url = "https://query1.finance.yahoo.com/v1/finance/search?q=$query&quotesCount=10&newsCount=0"
$response = Invoke-RestMethod -Uri $url -Method Get
$response.quotes | ConvertTo-Json -Depth 5
