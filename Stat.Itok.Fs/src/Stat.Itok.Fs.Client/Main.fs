module Stat.Itok.Fs.Client.Main

open System.Net.Http
open System.Net.Http.Json
open Microsoft.AspNetCore.Components
open Elmish
open Bolero
open Bolero.Html
open Zanaptak.TypedCssClasses
open Stat.Itok.Shared
open System
open System.Text.Json

let jsonOptions = new JsonSerializerOptions(PropertyNameCaseInsensitive = true)

type Bulma = CssClasses<"https://cdnjs.cloudflare.com/ajax/libs/bulma/0.9.4/css/bulma.min.css", Naming.PascalCase>

/// Routing endpoints definition.
type Page =
    | [<EndPoint "/">] Home

/// The Elmish application's model.

type PageLang =
    |CN
    |EN

type Model =
    {
        lang: PageLang
        page: Page
        error: string option
        jobConfig: JobConfigLite
        isBtnGetNewVerifyUrlLoading:bool
        isBtnAuthAccountLoading:bool
        isBtnSubmitLoading:bool
        isAuthSuccess:bool
        isRankedChecked:bool
        isTurfWarChecked:bool
    }

let initModel =
    {
        lang = EN
        page = Home
        error = None
        jobConfig = JobConfigLite()
        isBtnGetNewVerifyUrlLoading = false
        isBtnAuthAccountLoading = false
        isAuthSuccess = false
        isRankedChecked = true
        isTurfWarChecked = true
        isBtnSubmitLoading = false
    }

initModel.jobConfig.ForcedUserLang <- "zh-CN"

/// The Elmish application's update messages.
type Message =
    | SetPage of Page
    | SetRedirectionInput of string
    | SetForcedUserLang of string
    | GetNewVerifyUrl
    | GotNewVerifyUrl of ApiResp<NinTokenCopyInfo>
    | TryLoginAccountInfo
    | RawLoginAccountInfoResp of HttpResponseMessage
    | ParsedLoginAccountInfo of Result<NinAuthContext, string>
    | TrufWarChecked of bool
    | RankedChecked of bool
    | Error of exn
    | ClearError

let update (http: HttpClient) message model =
    match message with
    | SetPage page ->
        { model with page = page }, Cmd.none
    |TrufWarChecked isChecked ->
        {model with isTurfWarChecked = isChecked},Cmd.none
    |RankedChecked isChecked ->
        {model with isRankedChecked = isChecked},Cmd.none
    | SetRedirectionInput input ->
        model.jobConfig.NinAuthContext.TokenCopyInfo.RedirectUrl <- input
        model , Cmd.none
    | SetForcedUserLang selected ->
        model.jobConfig.ForcedUserLang <- selected
        model, Cmd.none
    | GetNewVerifyUrl -> 
        let getNewVerifyUrl() = http.GetFromJsonAsync<ApiResp<NinTokenCopyInfo>>("/api/nin/verify_url")
        let cmd = Cmd.OfTask.either getNewVerifyUrl () GotNewVerifyUrl Error
        {model with isBtnGetNewVerifyUrlLoading = true},cmd
    | GotNewVerifyUrl verifyUrlResp-> 
        let model = {model with isBtnGetNewVerifyUrlLoading = false}
        match verifyUrlResp.Result with
        |true -> 
            model.jobConfig.NinAuthContext.TokenCopyInfo <- verifyUrlResp.Data
            model, Cmd.none
        |_ -> { model with error = Some verifyUrlResp.Msg }, Cmd.none
    | TryLoginAccountInfo ->
        let getRawResp() = http.PostAsJsonAsync<NinTokenCopyInfo>("/api/nin/auth_account", model.jobConfig.NinAuthContext.TokenCopyInfo)
        let cmd = Cmd.OfTask.either getRawResp () RawLoginAccountInfoResp Error
        {model with isBtnAuthAccountLoading = true}, cmd
    |RawLoginAccountInfoResp rawResp ->
        let parseAsNinAuthContext() = 
            task{
                if rawResp.IsSuccessStatusCode then 
                    let! strResp = rawResp.Content.ReadAsStringAsync()
                    let parsed = JsonSerializer.Deserialize<ApiResp<NinAuthContext>>(strResp, jsonOptions);
                    match parsed.Result with
                    |true -> return Ok parsed.Data
                    |_-> return Result.Error $"Error response is: %s{parsed.Msg}"
                else return Result.Error $"Error when do request: &s{rawResp.StatusCode}"
            }
        let cmd = Cmd.OfTask.either parseAsNinAuthContext () ParsedLoginAccountInfo Error
        {model with isBtnAuthAccountLoading = false},cmd
    |ParsedLoginAccountInfo res ->
        match res with
        |Ok ninAuthCtx ->
            model.jobConfig.NinAuthContext<- ninAuthCtx
            {model with isAuthSuccess = true}, Cmd.none
        |Result.Error err -> { model with error = Some err }, Cmd.none
    | Error exn ->
        { model with error = Some exn.Message; isBtnGetNewVerifyUrlLoading = false; isBtnAuthAccountLoading = false }, Cmd.none
    | ClearError ->
        { model with error = None }, Cmd.none

/// Connects the routing system to the Elmish application.
let router = Router.infer SetPage (fun model -> model.page)

type Main = Template<"wwwroot/main.html">

let textItem (str:string) =
    p{
        attr.``class`` Bulma.IsBlock
        str
    }
let getNewVerifyUrl_EN(model:Model) dispatch =
    div{
        attr.``class`` Bulma.LevelLeft
        p{
            attr.``class`` ([
                Bulma.Button;
                Bulma.IsLink;
                Bulma.LevelItem
                if model.isBtnGetNewVerifyUrlLoading then Bulma.IsLoading  else null
            ]|>String.concat " ")
            on.click (fun _ -> dispatch GetNewVerifyUrl);
            "Get Auth URL"
        }
        a{
            attr.``class``([
                Bulma.LevelItem
                if String.IsNullOrEmpty(model.jobConfig.NinAuthContext.TokenCopyInfo.CopyRedirectionUrl) then Bulma.IsHidden else null
            ]|> String.concat " ")
            attr.target "_blank"
            attr.href model.jobConfig.NinAuthContext.TokenCopyInfo.CopyRedirectionUrl
            "To Copy Redirection" 
        }
    }

let doAuthAccount_EN model dispatch =
    div{
        attr.``class`` ([ Bulma.Field]|> String.concat "")
        label{
            attr.``class`` ([Bulma.Field; Bulma.Label]|> String.concat "")
            "Redirection Link"
            }
        input{
            bind.input.string model.jobConfig.NinAuthContext.TokenCopyInfo.RedirectUrl (fun n -> dispatch (SetRedirectionInput n))
            attr.``class`` Bulma.Input
            attr.``type`` "text"
            attr.placeholder "Paste your Redirection Link"
            }
        Html.br
        Html.br
        button{
            attr.``class`` ([
                Bulma.Button;
                Bulma.IsLink;
                if model.isBtnAuthAccountLoading then Bulma.IsLoading  else null
            ]|>String.concat " ")
            attr.disabled (if (String.IsNullOrEmpty(model.jobConfig.NinAuthContext.TokenCopyInfo.RedirectUrl)) then "disabled" else null)
            on.click (fun _ -> dispatch TryLoginAccountInfo)
            "Login Account"
        }
        Html.br
        Html.br

        if String.IsNullOrEmpty(model.jobConfig.NinAuthContext.UserInfo.Id)
        then empty()
        else 
            table{
                attr.``class`` ([
                    Bulma.Table;
                    Bulma.IsNarrow;
                    Bulma.IsBordered;
                    if model.isBtnAuthAccountLoading then Bulma.IsLoading  else null
                ]|>String.concat " ")
                tr{
                    td{"Id"}
                    td{$"%s{model.jobConfig.NinAuthContext.UserInfo.Id}"}
                }
                tr{
                    td{"Nickname"}
                    td{$"%s{model.jobConfig.NinAuthContext.UserInfo.Nickname}"}
                }
                tr{
                    td{"Country/Area"}
                    td{$"%s{model.jobConfig.NinAuthContext.UserInfo.Country}"}
                }
            }
    }

let CustomLang_EN model dispatch=
    div{
        attr.``class`` ([Bulma.Field; Bulma.Select]|> String.concat " ")
        select{
            bind.change.string model.jobConfig.ForcedUserLang (fun n -> dispatch (SetForcedUserLang n))
            option{
                attr.value  "zh-CN"
                "zh-CN"
            }
            option{
                attr.value  "en-US"
                "en-US"
            }
            option{
                attr.value  "zh-TW"
                "zh-TW"
            }
        }
        
    }

let SelectBattleModel_EN model dispatch =
    div{
        label{
            attr.``class`` ([Bulma.Checkbox]|> String.concat " ")
            input{
                attr.``type`` Bulma.Checkbox
                bind.``checked`` model.isRankedChecked (fun n -> dispatch (RankedChecked n))
            }
            "Bankara(Ranked)"
            
        }
        span{"   "}
        label{
            attr.``class`` ([Bulma.Checkbox]|> String.concat " ")
            input{
                attr.``type`` Bulma.Checkbox
                bind.``checked`` model.isTurfWarChecked (fun n -> dispatch (TrufWarChecked n))
            }
            "Regular(Turf War, contains SplatFest)"
        }
    }

let submitForm_En model dispatch =
    button{
        attr.``class`` ([
            Bulma.Button;
            Bulma.IsSuccess;
            if model.isBtnSubmitLoading then Bulma.IsLoading  else null
        ]|>String.concat " ")
        attr.disabled (if (((not model.isTurfWarChecked) && (not model.isRankedChecked)) ||not model.isAuthSuccess) then "disabled" else null)
        on.click (fun _ -> dispatch TryLoginAccountInfo)
        "Submit"
    }

let mainForm_EN (model:Model) dispatch =
    div{
        attr.``class`` ([
            Bulma.Field
        ]|> String.concat " ")
        p{
           attr.``class`` ([Bulma.Block] |> String.concat " ")
           textItem("1. Login to Nintendo Account in this browser")
           p{
               attr.``class`` Bulma.IsBlock
               "2. Click "
               span{
                    attr.``class`` Bulma.HasBackgroundPrimary
                    "Get Auth URL"
               }
               " , wait "
               span{
                    attr.``class`` Bulma.HasBackgroundPrimary
                    "To Copy Redirection"
               }
               " show, then click"
           }
           p{
               attr.``class`` Bulma.IsBlock
               "3. In opened page，right click on the red "
               span{
                    attr.``class`` Bulma.HasBackgroundDangerLight
                    "Select this one"
               }
               " button. In the menu click "
               span{
                    attr.``class`` Bulma.HasBackgroundWarning
                    "Copy Link"
               }
           }
        }
        getNewVerifyUrl_EN model dispatch
        hr
        p{
            attr.``class`` ([Bulma.Block] |> String.concat " ")
            p{
                attr.``class`` Bulma.IsBlock
                "4. Paste to "
                span{
                    attr.``class`` Bulma.HasBackgroundPrimary
                    "Redirection Link,"
                }
                " Then Click "
                span{
                    attr.``class`` Bulma.HasBackgroundPrimary
                    "Login Account"
                }
                " Button "
            }
        }
        doAuthAccount_EN model dispatch
        hr
        p{
            attr.``class`` ([Bulma.Block] |> String.concat " ")
            p{
                attr.``class`` Bulma.IsBlock
                "5. Choose the language for in-game medals, nameplate etc."
            }
        }
        CustomLang_EN model dispatch
        hr
        p{
            attr.``class`` ([Bulma.Block] |> String.concat " ")
            p{
                attr.``class`` Bulma.IsBlock
                "6. Select the battle mode you want to monitor"
            }
        }
        SelectBattleModel_EN model dispatch
        hr
        submitForm_En model dispatch

    }

let homeBlock_EN model dispatch =
    Main.Home()
        .HomeBody(mainForm_EN model dispatch)
        .Elt()

let notFoundBlock model dispatch =
    p{
        "NotFound"
    }
let menuItem (model: Model) (page: Page) (text: string) =
    Main.MenuItem()
        .Active(if model.page = page then "is-active" else null)
        .Url(router.Link page)
        .Text(text)
        .Elt()

let aboutPage model dispatch =
    Main.About().Elt()

let view model dispatch =
    Main()
        .Menu(concat {
            menuItem model Home "Home"
        })
        .Body(
            cond model.page <| function
            | Home -> 
                match model.lang with 
                |EN -> homeBlock_EN model dispatch
                |CN -> homeBlock_EN model dispatch
        )
        .Error(
            cond model.error <| function
            | None -> empty()
            | Some err ->
                Main.ErrorNotification()
                    .Text(err)
                    .Hide(fun _ -> dispatch ClearError)
                    .Elt()
        )
        .Elt()

type StatItokApp() =
    inherit ProgramComponent<Model, Message>()

    [<Inject>]
    member val HttpClient = Unchecked.defaultof<HttpClient> with get, set

    override this.Program =
        let update = update this.HttpClient
        Program.mkProgram (fun _ -> initModel, Cmd.ofMsg ClearError) update view
        |> Program.withRouter router
