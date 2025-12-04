$headers = @{
    "User-Agent" = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
    "Accept" = "application/json, text/plain, */*"
    "Accept-Language" = "en-US,en;q=0.9"
}
$symbols = "MSFT,AAPL"
$url = "https://query1.finance.yahoo.com/v6/finance/quote?symbols=$symbols"
try {
    $response = Invoke-RestMethod -Uri $url -Method Get -Headers $headers
    $response.quoteResponse.result | ConvertTo-Json -Depth 5
} catch {
    Write-Host $_.Exception.Message
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader $_.Exception.Response.GetResponseStream()
        $reader.ReadToEnd()
    }
}
