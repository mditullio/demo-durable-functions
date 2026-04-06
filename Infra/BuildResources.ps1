# Variables
$RG = "rg-demodurablefunc-itn-001"
$LOC = "italynorth"
$SA = "stdemodurableitn001"
$FUNC = "func-demodurablefunc-itn-001"
$IP = "194.230.194.46"
$LAW = "law-demodurablefunc-itn-001"
$APPINS = "appi-demodurablefunc-itn-001"

# 1. Create Resource Group
az group create --name $RG --location $LOC

# 2. Create Storage Account (LRS)
az storage account create --name $SA --resource-group $RG --location $LOC --sku Standard_LRS

# 3. Create Function App (Flex Consumption)
az functionapp create `
    --resource-group $RG `
    --name $FUNC `
    --storage-account $SA `
    --flexconsumption-location $LOC `
    --runtime dotnet-isolated `
    --runtime-version 8.0 `
    --functions-version 4

# 4. Configure IP Restriction
az functionapp config access-restriction add `
    --resource-group $RG `
    --name $FUNC `
    --rule-name "AllowOfficeIP" `
    --action Allow `
    --ip-address "$IP/32" `
    --priority 100
	
	
# 5. Create Log Analytics Workspace
az monitor log-analytics workspace create `
    --resource-group $RG `
    --workspace-name $LAW `
    --location $LOC

# 6. Create Application Insights
az monitor app-insights component create `
    --app $APPINS `
    --location $LOC `
    --kind web `
    --resource-group $RG `
    --workspace $LAW

# 7. Get Connection String
$APPINS_CONN_STRING = $(az monitor app-insights component show `
    --app $APPINS `
    --resource-group $RG `
    --query connectionString -o tsv)

# 8. Link to Function App
az functionapp config appsettings set `
    --name $FUNC `
    --resource-group $RG `
    --settings APPLICATIONINSIGHTS_CONNECTION_STRING=$APPINS_CONN_STRING