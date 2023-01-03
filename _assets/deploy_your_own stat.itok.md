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
### Function App: background worker

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

