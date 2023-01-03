# <img src="https://user-images.githubusercontent.com/11204672/204310549-5c30aec4-924e-4e15-8a04-27ed9d7afe5c.png" width="25"> stat.itok

![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/itoktsnhc/stat.itok/keep_func_alive.yml?branch=main&label=Keep%20Site%20Alive)
![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/itoktsnhc/stat.itok/static_website.yml?branch=release%2Fstatic&label=web)
![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/itoktsnhc/stat.itok/background_worker.yml?branch=release%2Fbackground&label=worker)


A tool/system for connecting your splatoon3 battle history(nsoapp) to [stat.ink](https://stat.ink/)

> **Hosted on Azure now: https://stat.itok.xyz/**
>
> And check the run status on JobTrackerX: http://jobtracker.itok.xyz/ （Empty if no one play the game 🙂）  

*If you want to deploy your own stat.itok, check the [deployment guide](https://github.com/Itoktsnhc/stat.itok/blob/main/_assets/deploy_your_own%20stat.itok.md)*

## Basic Info
Programing languages or frameworks: 
- C# for backend functions and worker in **Stat.Itok.Func**, **Stat.Itok.Func.Worker**
- Svelte + Bulma CSS for frontend in **Stat.Itok.Front**

Azure service used:

- Azure Storage
  - Blob: Cache battle detail responses.
  - Queue: For internal fetch detail job task.
  - ~~Table: Store raw Nintendo account info(Session Token), store battle id map for deduplication~~
- Azure Static Web App
  - Static Web App
  - Backend functions(HTTP Triggers): handle new Nintendo account info(**Job Config**)
- Azure Functions
  - Timer Trigger: Job Dispatcher, regularly check new battles for each **Job Config**, If any new battles, build a **Job Run** which contains **Job Run Tasks** , send the **Job Run Tasks** to Azure Queue Storage for Queue Trigger
  - Queue Trigger: 
    - Job Worker: Handler for normal detail battle fetch job.
    - Job Poison Worker: Handler for bad(poison) detail battle fetch job
- Azure CosmosDB(no SQL api)
  - Free(1000RU + 25GB)data store for JobConfig, JobRun and BattleTaskPayload



## Details
### Concepts:

- `JobConfig`: Just a entity contains auth info for fetch new battle. 
- `JobRun`: A virtual container contains one or many `JobRunTask`.
- `JobRunTask`: A task contains fetch battle detail required information.

### Execution Flow

![Snipaste_2022-12-12_17-56-04](https://user-images.githubusercontent.com/11204672/207016519-3872ef8d-7370-43f6-8f09-8bd68c2d4d9d.png)

## Credits

- [s3s](https://github.com/frozenpandaman/s3s) for the total battle detail parse code

- [stat.ink](https://github.com/fetus-hina/stat.ink) for the great website

- [imink](https://github.com/imink-app) for the f-calc API

  

## Disclaimer

**This is not a Nintendo or stat.ink official tool.** 

This tool will store the Nintendo Account's Session Token related info. And If you care about your account's privacy, you should host your own by forking the code and deploy to Azure or anywhere else you want.

**No commitment to the reliability**
