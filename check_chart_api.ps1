$symbol = "MSFT"
$url = "https://query1.finance.yahoo.com/v8/finance/chart/$symbol?interval=1d&range=1d"
$response = Invoke-RestMethod -Uri $url -Method Get
$response.chart.result[0].meta | ConvertTo-Json -Depth 5
