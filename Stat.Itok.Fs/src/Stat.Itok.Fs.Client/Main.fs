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

type Bulma = CssClasses<"https://cdnjs.cloudflare.com/ajax/libs/bulma/0.7.4/css/bulma.min.css", Naming.PascalCase>

/// Routing endpoints definition.
type Page =
    | [<EndPoint "/">] Home
    | [<EndPoint "/about">] About

/// The Elmish application's model.

type Model =
    {
        page: Page
        error: string option
        jobConfig: JobConfigLite
        isBtnGetNewVerifyUrlLoading:bool
    }

let initModel =
    {
        page = Home
        error = None
        jobConfig = JobConfigLite()
        isBtnGetNewVerifyUrlLoading = false
    }

/// The Elmish application's update messages.
type Message =
    | SetPage of Page
    | GetNewVerifyUrl
    | GotNewVerifyUrl of ApiResp<NinTokenCopyInfo>
    | Error of exn
    | ClearError

let update (http: HttpClient) message model =
    match message with
    | SetPage page ->
        { model with page = page }, Cmd.none
    | GetNewVerifyUrl -> 
        let getNewVerifyUrl() = http.GetFromJsonAsync<ApiResp<NinTokenCopyInfo>>("/api/nin/verify_url")
        let cmd = Cmd.OfTask.either getNewVerifyUrl ()  GotNewVerifyUrl Error
        {model with isBtnGetNewVerifyUrlLoading = true},cmd
    | GotNewVerifyUrl verifyUrlResp-> 
        let model = {model with isBtnGetNewVerifyUrlLoading = false}
        match verifyUrlResp.Result with
        |true -> 
            model.jobConfig.NinAuthContext.TokenCopyInfo <- verifyUrlResp.Data
            model, Cmd.none
        |_ -> { model with error = Some verifyUrlResp.Msg }, Cmd.none
    | Error exn ->
        { model with error = Some exn.Message; isBtnGetNewVerifyUrlLoading = false }, Cmd.none
    | ClearError ->
        { model with error = None }, Cmd.none

/// Connects the routing system to the Elmish application.
let router = Router.infer SetPage (fun model -> model.page)

type Main = Template<"wwwroot/main.html">

let textItem (str:string)=
    p{
        attr.``class`` Bulma.IsBlock
        str
    }
let authUrlRow(model:Model) dispatch =
    div{
        attr.``class`` Bulma.LevelRight
        p{
            attr.``class`` ([
                Bulma.Button;
                Bulma.IsLink;
                Bulma.LevelItem
                if model.isBtnGetNewVerifyUrlLoading then Bulma.IsLoading  else ""
            ]|>String.concat " ")
            on.click (fun _ -> dispatch GetNewVerifyUrl);
            "Get Auth URL"
        }
        a{
            attr.``class``([
                Bulma.LevelItem
                if String.IsNullOrEmpty(model.jobConfig.NinAuthContext.TokenCopyInfo.CopyRedirectionUrl) then Bulma.IsHidden else ""
            ]|> String.concat " ")
            attr.target "_blank"
            attr.href model.jobConfig.NinAuthContext.TokenCopyInfo.CopyRedirectionUrl
            "To Copy Redirection" 
        }
    }

let mainForm (model:Model) dispatch =
    div{
        attr.``class`` ([
            Bulma.Field
            Bulma.HasAddons
        ]|> String.concat " ")
        authUrlRow model dispatch
    }

let homePage model dispatch =
    Main.Home().HomeBody(
        div{
            ol{
               attr.``class`` ([Bulma.Block] |> String.concat " ")
               textItem(" Login to Nintendo Account in this browser")
               p{
                   attr.``class`` Bulma.IsBlock
                   " Click "
                   span{
                        attr.``class`` Bulma.HasBackgroundWarning
                        "Get Auth URL"
                   }
                   " , wait "
                   span{
                        attr.``class`` Bulma.HasBackgroundWarning
                        "To Copy Redirection"
                   }
                   " show, then click"
               }
               p{
                   attr.``class`` Bulma.IsBlock
                   " In opened page，right click on red "
                   span{
                        attr.``class`` Bulma.HasBackgroundWarning
                        "Select this one"
                   }
                   " button. In the menu click "
                   span{
                        attr.``class`` Bulma.HasBackgroundWarning
                        "Copy Link"
                   }
               }
            }
            Html.hr
            mainForm model dispatch
        })
        .Elt()
let aboutPage model dispatch =
    Main.About().Elt()

let menuItem (model: Model) (page: Page) (text: string) =
    Main.MenuItem()
        .Active(if model.page = page then "is-active" else "")
        .Url(router.Link page)
        .Text(text)
        .Elt()

let view model dispatch =
    Main()
        .Menu(concat {
            menuItem model Home "Home"
            menuItem model About "About"
        })
        .Body(
            cond model.page <| function
            | Home -> homePage model dispatch
            | About -> aboutPage model dispatch
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
