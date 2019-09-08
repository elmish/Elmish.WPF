module Elmish.WPF.Logging


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
  | PropertyChangedData of PropertyChangedData
  | ValidationErrorsChangedData of ValidationErrorsChangedData // TODO: Split by adding ValidationErrorsRemovedData
  | TimingData of TimingData
  | CreatingHiddenWindowData of CreatingHiddenWindowData
  | CreatingVisibleWindowData of CreatingVisibleWindowData
  | InitializingBindingsData of InitializingBindingsData
  | ClosingWindowData of ClosingWindowData
  | HindingWindowData of HindingWindowData
  | ShowingHiddenWindow of ShowingHiddenWindow
  | NewSubModelSelectedItemSelectionData of NewSubModelSelectedItemSelectionData
  | TryGetMemberCalledData of TryGetMemberCalledData
  | TrySetMemberCalledData of TrySetMemberCalledData
  | GettingErrorsData of GettingErrorsData


let logTraceWith logger data =
  let log fmt = Printf.kprintf logger fmt
  match data with
  | PropertyChangedData d -> log "[%s] PropertyChanged \"%s\"" d.PropertyNameChain d.PropertyName
  | ValidationErrorsChangedData d -> log "[%s] ErrorsChanged \"%s\"" d.PropertyNameChain d.PropertyName
  | TimingData d -> log "[%s] %s (%ims): %s" d.PropertyNameChain d.BindingDataFunctionName d.ElapsedMilliseconds d.PropertyName
  | CreatingHiddenWindowData d -> log "[%s] Creating hidden window" d.PropertyNameChain
  | CreatingVisibleWindowData d -> log "[%s] Creating and opening window" d.PropertyNameChain
  | InitializingBindingsData d -> log "[%s] Initializing bindings" d.PropertyNameChain
  | ClosingWindowData d -> log "[%s] Closing window" d.PropertyNameChain
  | HindingWindowData d -> log "[%s] Hiding window" d.PropertyNameChain
  | ShowingHiddenWindow d -> log "[%s] Showing existing hidden window" d.PropertyNameChain
  | NewSubModelSelectedItemSelectionData d -> log "[%s] Setting selected VM to %A" d.PropertyNameChain d.NewSelection
  | TryGetMemberCalledData d -> log "[%s] TryGetMember %s" d.PropertyNameChain d.PropertyName
  | TrySetMemberCalledData d -> log "[%s] TrySetMember %s" d.PropertyNameChain d.PropertyName
  | GettingErrorsData d -> log "[%s] GetErrors %s" d.PropertyNameChain (d.PropertyName |> Option.defaultValue "<null>")
  

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
  | WindowToCloseMissingData of WindowToCloseMissingData
  | WindowToHideMissingData of WindowToHideMissingData
  | WindowToShowMissingData of WindowToShowMissingData
  | TryGetMemberMissingBindingData of TryGetMemberMissingBindingData
  | TrySetMemberMissingBindingData of TrySetMemberMissingBindingData
  | TrySetMemberReadOnlyBindingData of TrySetMemberReadOnlyBindingData


let logErrorWith logger data =
  let log fmt = Printf.kprintf logger fmt
  match data with
  | WindowToCloseMissingData d -> log "[%s] Attempted to close window, but did not find window reference" d.PropertyNameChain
  | WindowToHideMissingData d -> log "[%s] Attempted to hide window, but did not find window reference" d.PropertyNameChain
  | WindowToShowMissingData d -> log "[%s] Attempted to show existing hidden window, but did not find window reference" d.PropertyNameChain
  | TryGetMemberMissingBindingData d -> log "[%s] TryGetMember FAILED: Property %s doesn't exist" d.PropertyNameChain d.PropertyName
  | TrySetMemberMissingBindingData d -> log "[%s] TrySetMember FAILED: Property %s doesn't exist" d.PropertyNameChain d.PropertyName
  | TrySetMemberReadOnlyBindingData d -> log "[%s] TrySetMember FAILED: Binding %s is read-only" d.PropertyNameChain d.PropertyName
