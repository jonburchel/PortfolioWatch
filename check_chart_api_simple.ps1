$symbol = "MSFT"
$url = "https://query1.finance.yahoo.com/v8/finance/chart/$symbol?interval=1d&range=1d"
try {
    $response = Invoke-RestMethod -Uri $url -Method Get
    $response.chart.result[0].meta | ConvertTo-Json -Depth 5
} catch {
    Write-Host $_.Exception.Message
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader $_.Exception.Response.GetResponseStream()
        $reader.ReadToEnd()
    }
}
