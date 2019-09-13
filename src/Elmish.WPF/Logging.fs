module internal Elmish.WPF.Logging


let compositeLogger loggers s =
  loggers |> Seq.iter (fun logger -> logger s)


type PropertyChangedData =
  { PropertyNameChain: string
    PropertyName: string }

type ValidationErrorsChangedData =
  { PropertyNameChain: string
    PropertyName: string }  // TODO: add Errors

type TimingData =
  { PropertyNameChain: string
    PropertyName: string
    BindingDataFunctionName: string
    ElapsedMilliseconds: int64 }

type CreatingHiddenWindowData =
  { PropertyNameChain: string }

type CreatingVisibleWindowData =
  { PropertyNameChain: string }

type InitializingBindingsData =
  { PropertyNameChain: string }

type ClosingWindowData =
  { PropertyNameChain: string }

type HindingWindowData =
  { PropertyNameChain: string }

type ShowingHiddenWindow =
  { PropertyNameChain: string }

type NewSubModelSelectedItemSelectionData =
  { PropertyNameChain: string
    NewSelection: obj voption }

type TryGetMemberCalledData =
  { PropertyNameChain: string
    PropertyName: string }

type TrySetMemberCalledData =
  { PropertyNameChain: string
    PropertyName: string }

type GettingErrorsData =
  { PropertyNameChain: string
    PropertyName: string option }


type TraceLogData =
  | PropertyChanged of PropertyChangedData
  | ValidationErrorsChanged of ValidationErrorsChangedData // TODO: Split by adding ValidationErrorsRemovedData
  | Timing of TimingData
  | CreatingHiddenWindow of CreatingHiddenWindowData
  | CreatingVisibleWindow of CreatingVisibleWindowData
  | InitializingBindings of InitializingBindingsData
  | ClosingWindow of ClosingWindowData
  | HindingWindow of HindingWindowData
  | ShowingHiddenWindow of ShowingHiddenWindow
  | NewSubModelSelectedItemSelection of NewSubModelSelectedItemSelectionData
  | TryGetMemberCalled of TryGetMemberCalledData
  | TrySetMemberCalled of TrySetMemberCalledData
  | GettingErrors of GettingErrorsData


let logTraceWith logger data =
  let log fmt = Printf.kprintf logger fmt
  match data with
  | PropertyChanged d -> log "[%s] PropertyChanged \"%s\"" d.PropertyNameChain d.PropertyName
  | ValidationErrorsChanged d -> log "[%s] ErrorsChanged \"%s\"" d.PropertyNameChain d.PropertyName
  | Timing d -> log "[%s] %s (%ims): %s" d.PropertyNameChain d.BindingDataFunctionName d.ElapsedMilliseconds d.PropertyName
  | CreatingHiddenWindow d -> log "[%s] Creating hidden window" d.PropertyNameChain
  | CreatingVisibleWindow d -> log "[%s] Creating and opening window" d.PropertyNameChain
  | InitializingBindings d -> log "[%s] Initializing bindings" d.PropertyNameChain
  | ClosingWindow d -> log "[%s] Closing window" d.PropertyNameChain
  | HindingWindow d -> log "[%s] Hiding window" d.PropertyNameChain
  | ShowingHiddenWindow d -> log "[%s] Showing existing hidden window" d.PropertyNameChain
  | NewSubModelSelectedItemSelection d -> log "[%s] Setting selected VM to %A" d.PropertyNameChain d.NewSelection
  | TryGetMemberCalled d -> log "[%s] TryGetMember %s" d.PropertyNameChain d.PropertyName
  | TrySetMemberCalled d -> log "[%s] TrySetMember %s" d.PropertyNameChain d.PropertyName
  | GettingErrors d -> log "[%s] GetErrors %s" d.PropertyNameChain (d.PropertyName |> Option.defaultValue "<null>")
  

type WindowToCloseMissingData =
  { PropertyNameChain: string }

type WindowToHideMissingData =
  { PropertyNameChain: string }

type WindowToShowMissingData =
  { PropertyNameChain: string }

type TryGetMemberMissingBindingData =
  { PropertyNameChain: string
    PropertyName: string }

type TrySetMemberMissingBindingData =
  { PropertyNameChain: string
    PropertyName: string }

type TrySetMemberReadOnlyBindingData =
  { PropertyNameChain: string
    PropertyName: string }


type ErrorLogData =
  | WindowToCloseMissing of WindowToCloseMissingData
  | WindowToHideMissing of WindowToHideMissingData
  | WindowToShowMissing of WindowToShowMissingData
  | TryGetMemberMissingBinding of TryGetMemberMissingBindingData
  | TrySetMemberMissingBinding of TrySetMemberMissingBindingData
  | TrySetMemberReadOnlyBinding of TrySetMemberReadOnlyBindingData


let logErrorWith logger data =
  let log fmt = Printf.kprintf logger fmt
  match data with
  | WindowToCloseMissing d -> log "[%s] Attempted to close window, but did not find window reference" d.PropertyNameChain
  | WindowToHideMissing d -> log "[%s] Attempted to hide window, but did not find window reference" d.PropertyNameChain
  | WindowToShowMissing d -> log "[%s] Attempted to show existing hidden window, but did not find window reference" d.PropertyNameChain
  | TryGetMemberMissingBinding d -> log "[%s] TryGetMember FAILED: Property %s doesn't exist" d.PropertyNameChain d.PropertyName
  | TrySetMemberMissingBinding d -> log "[%s] TrySetMember FAILED: Property %s doesn't exist" d.PropertyNameChain d.PropertyName
  | TrySetMemberReadOnlyBinding d -> log "[%s] TrySetMember FAILED: Binding %s is read-only" d.PropertyNameChain d.PropertyName
