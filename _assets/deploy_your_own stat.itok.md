### Storage account

1. Your storage account should be a full featured storage account(with blob, table, queue)
2. Upload some remote configs(as failback config)
	1. Create a blob container called: **`rconfig`**
	2. Upload the `nin_misc_config.json`(under `/_ninMiscConfig` folder) to **`rconfig`** container

### Azure Cosmos DB account

1. Create Azure Cosmos DB account(free or paid), use **`API for NoSQL`** data model
2. Create database with name: **`store`**
3. Create container in database with name: **`container`**

###  Static Web App: front-end website

``` json
[
  {
    "name": "GlobalConfig__CosmosDbConnStr",
    "value": "Your cosmos db ConnStr"
  },
  {
    "name": "GlobalConfig__CosmosDbPkPrefix",
    "value": "prod"
  },
  {
    "name": "GlobalConfig__StorageAccountConnStr",
    "value": "Your storage account ConnStr"
  },
  {
    "name": "WorkerQueueConnStr",
    "value": "Your storage account ConnStr"
  }
]
```
### Backend Worker 

#### AS Azure Function

>  same as static web app

``` json
[
  {
    "name": "GlobalConfig__CosmosDbConnStr",
    "value": "Your cosmos db ConnStr"
  },
  {
    "name": "GlobalConfig__CosmosDbPkPrefix",
    "value": "prod"
  },
  {
    "name": "GlobalConfig__StorageAccountConnStr",
    "value": "Your storage account ConnStr"
  },
  {
    "name": "WorkerQueueConnStr",
    "value": "Your storage account ConnStr"
  }
]
```
#### AS Docker Container

1. Pull the latest image
`docker pull ghcr.io/itoktsnhc/stat.itok:latest`

2. run with same config above(But as environment variables) in docker 
``` yml
  # docker-compose -f stat.itok.yml -p stat_itok up -d
  version: "3.9"

  services:
    stat_itok:
      container_name: stat_itok
      restart: unless-stopped
      image: "ghcr.io/itoktsnhc/stat.itok"
      pull_policy: always
      environment:
        AzureWebJobs.JobWorker.Disabled: "0"
        AzureWebJobs.JobDispatcher.Disabled: "0"
        AzureWebJobsStorage: "FILLME"
        GlobalConfig__CosmosDbConnStr: "FILLME"
        GlobalConfig__CosmosDbPkPrefix: "prod"
        GlobalConfig__EmailConfig__AdminEmail: "FILLME"
        GlobalConfig__EmailConfig__Password: "FILLME"
        GlobalConfig__EmailConfig__Server: "FILLME"
        GlobalConfig__EmailConfig__Username: "FILLME"
        GlobalConfig__StorageAccountConnStr: "FILLME"
        WorkerQueueConnStr: "FILLME"


```
