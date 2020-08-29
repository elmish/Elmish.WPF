namespace Models

open Elmish.WPF
open Elmish
open System
open System.Windows

type ContactDetail = { Name: string; Content: string; Text: string }
type Internet      = { Name: string; Content: string; Text: string }
type PhoneNumber   = { Name: string; Content: string; Text: string }
type Address       = { Name: string; Content: string; Text: string }

    module FrontOffice =
        type Details =
            | ContactDetail of ContactDetail * Id: Guid
            | Internet      of Internet   * Id: Guid
            | PhoneNumber   of PhoneNumber   * Id: Guid
            | Address       of Address  * Id: Guid

            member this.id =
                   match this with
                   | ContactDetail(_, id)
                   | Internet(_, id)
                   | PhoneNumber(_, id)
                   | Address(_, id) -> id

            member this.name =
                match this with
                | ContactDetail(cd,_) -> cd.Name
                | Internet(i,_)  -> i.Name
                | PhoneNumber(pn,_) -> pn.Name
                | Address(ad,_) -> ad.Name

            member this.content =
                match this with
                | ContactDetail(cd,_) -> cd.Content
                | Internet(i,_)  -> i.Content
                | PhoneNumber(pn,_) -> pn.Content
                | Address(ad,_) -> ad.Content

        let contactDetail  : ContactDetail = { Name="Contact Detail"; Content="Content for Contact Detail"; Text="here is the contact detail text" }
        let internet       : Internet = { Name="Internet";       Content="Content for Internet";       Text="here is the internet text" }
        let phoneNumber    : PhoneNumber =  {Name="Phone Number";   Content="Content for phone number";   Text="here is the phone number text" }
        let address        : Address = { Name="Address";        Content="Content for Address";        Text="here is the Address text" }

        let details   = [ContactDetail (contactDetail,Guid.NewGuid())
                         Internet      (internet,Guid.NewGuid())
                         PhoneNumber   (phoneNumber,Guid.NewGuid())
                         Address       (address,Guid.NewGuid())
                         ]

        // Each instance will hold one of ContactDetails, Internet, Phonenumber, and
        // each has a common property of ID (the Guid). This way, you simplify your model
        type DetailsWithId = DetailsWithId of Details * Guid

        /// This is the main data model for our application
        type Model = {
          ClickCount: int
          Message: string
          Details: Details list
        }

        /// This is used to define the initial state of our application. It can take any arguments, but we'll just use unit. We'll need the Cmd type.
        /// Notice that we return a tuple. The first field of the tuple tells the program the initial state. The second field holds the command to issue.
        /// This is the standard Elmish init() (not special to Elmish.WPF).
        let init() =
           {
              ClickCount = 0
              Message = "Hello Elmish.WPF"
              Details = details
           }

        /// This is a discriminated union of the available messages from the user interface
        type Msg =
          | ButtonClicked
          | Reset

        /// This is the Reducer Elmish.WPF calls to generate a new model based on a message and an old model.
        /// The update function will receive the change required by Msg, and the current state. It will produce a new state and potentially new command(s).
        let update (msg: Msg) (model: Model) =
          match msg with
          | ButtonClicked -> {model with ClickCount = model.ClickCount + 1}
          | Reset -> init()


        /// Elmish.WPF uses this to provide the data context for your view based on a model.
        /// The bindings is the view for Elmish.WPF
        /// Define the “view” function using the Bindings module. This is the central public API of Elmish.WPF. Normally in Elm/Elmish this
        /// function is called view and would take a model and a dispatch function (to dispatch new messages to the update loop) and return
        /// the UI (e.g. a HTML DOM to be rendered), but in Elmish.WPF this function is in general only run once and simply sets up bindings
        /// that XAML-defined views can use. Therefore, it is called bindings instead of view.
        let bindings(): Binding<Model, Msg> list =
            [
              // One-Way Bindings
              "ClickCount" |> Binding.oneWay (fun m -> m.ClickCount)
              "Message" |> Binding.oneWay (fun m -> m.Message)

              //These lines FAIL.  The first statement is needed for the TabControl resources, but the second line is needed to
              // resolve the "Name" binding...How is this fixed???????????????????????
              "Details" |> Binding.oneWay(fun m -> m.Details)
                        |> Binding.subModelSeq((fun m -> m.Details), (fun detail -> detail.id), fun () ->
                            [
                                "Id"   |> Binding.oneWay (fun (_, detail) -> detail.id)
                                "Name" |> Binding.oneWay (fun (_, detail) -> detail.name)
                                "Content" |> Binding.oneWay (fun (_,detail) -> detail.content)
                            ])


              // Commands
              "ClickCommand" |> Binding.cmd ButtonClicked
              "ResetCommand" |> Binding.cmd Reset
            ]

        /// This is the application's entry point. It hands things off to Elmish.WPF
        let entryPoint (mainWindow: Window) =
            Program.mkSimpleWpf init update bindings
            |> Program.runWindowWithConfig
                       { ElmConfig.Default with LogTrace = true; Measure = true; MeasureLimitMs = 1 }
                       mainWindow
