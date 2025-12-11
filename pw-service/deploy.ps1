dotnet publish -c Release -o ./publish
cd publish
Compress-Archive -Path * -DestinationPath ../publish.zip -Force
cd ..
az webapp deployment source config-zip --resource-group PortfolioWatch-RG --name portfolio-watch --src publish.zip
Remove-Item publish.zip