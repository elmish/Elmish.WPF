#### 4.0.0-beta-47
* Improved `ViewModelBase` to infer view model property types from the Model getter rather than needing to be explicitly specified.
* Added `'t` type parameter to `Binding<'model, 'msg, 't>` everywhere to support above feature. `Binding<'model, 'msg>` is defined as `Binding<'model, 'msg, obj>` for full backwards compatibility.
* Added `Binding.boxT` and `Binding.unboxT` to support moving back and forth between the two.
* Added `Binding.OneWayT`, `Binding.OneWayToSourceT` and `Binding.CmdT` modules for creating strongly typed primitives for the top feature.
* Added types internally to carry everything through (mostly provably) correctly.
* Added `'viewModel` type parameter to `WpfProgram<'model, 'msg, 'viewModel>` to support using a static view model at the top level. `WpfProgram<'model, 'msg>` is defined as `WpfProgram<'model, 'msg, obj>` for full backwards compatibility. Also made `WpfProgram` core type more generic, replacing the list of bindings with equivalent but more flexible `CreateViewModel` and `UpdateViewModel` functions.
* Added `WpfProgram.mkSimpleT`, `WpfProgram.mkProgramT` and `WpfProgram.mkProgramWithCmdMsgT` for making programs that use static view models as the top-level data context.
* Modified `SubModelStatic` sample project to use new static view model features.

#### 4.0.0-beta-46
* Added `ViewModelBase` which allows view models to be defined as static types with real properties rather than unnamed dynamic types with stringly named properties.
* Added `Binding.SubModelT`, `Binding.SubModelSeqUnkeyedT`, `Binding.SubModelSeqKeyedT` and `Binding.SubModelWinT` modules for creating these static types as sub models.
* Replaced internal usage of refs in dynamic view model with get/set functions to allow for matting of the type.
* Added some internal types to support `ViewModelBase`.
* Improved documentation.
* Added a `SubModelStatic` sample project using above feature.

#### 4.0.0-beta-45
* Improved performance of Lazy effect by reducing calls to later model mappings
* Removed `SourceOrTarget` and `DuplicateIdException` from public API (added in `4.0.0-beta-42`)
* Improved caching effect to not invalidating the cache too early (an issue introduced in version `3.5.3 `via PR 181)
* Renamed `Binding.SetMsgWithModel` to `Binding.setMsgWithModel` (breaking public API added in `4.0.0-beta-42`)
* Fixed bug with `mapMsgWithModel` and `setMsgWithModel` where the original model was sometimes given (an issue introduced in `4.0.0-beta-42`)
* Replaced framework targets `net461` and `net5.0-windows` with `net480` and `net6.0-windows` respectively.
* Updated minimum `FSharp.Core` version to `6.0.5`.
* Updated minimum `Microsoft.Extensions.Logging.Abstractions` version to `6.0.1`.
* Fixed broken log statement called when a `SubModelSelectedItem` binding can't find an item in a `SubModelSeq` binding.

#### 4.0.0-beta-44
* Fixed sticky effect that was broken in `4.0.0-beta-42`

#### 4.0.0-beta-43
* Added `WpfProgram.withElmishErrorHandler`
* Improved debugging experience by overriding `GetDynamicMemberNames`
* Relaxed version constrains on `FSharp.Core` and `Microsoft.Extensions.Logging.Abstractions`

#### 4.0.0-beta-42
* Improved API of `WindowState<_>`
* Dropped support for .NET Core 3.0.  Still have support for .NET Core 3.1.
* Added `setMsg` in the `Binding` module
* Added `setMsgWithModel` in the `Binding` module
* Removed `id` in the `Binding` module (that was added in 4.0.0-beta-3)
* Lazy effect now exposed via the name `addLazy`
* Validation effect now exposed via the name `addValidation`
* Sticky effect now exposed via the name `addSticky`
* Added caching effect via the name `addCaching`
* Now logging when WPF tries to get or set a binding and an exception is thrown
* Added to the `SubModelSeq` method API an overload that only takes bindings
* Added support for a `SubModelSeq` variant that does involve IDs
* `SubModelSeq` variant that involves IDs will now merge elements without considering IDs if duplicate IDs are detected
* Fixed bug (present in 3.5.8) where `ArgumentNullException` is thrown from `INotifyDataErrorInfo.GetErrors` when given `null`
* Now logging and returning `false` to WPF if selection in a `SubModelSelectedItem` binding fails
* Added prebuilt bindings for `Selector.SelectedIndex`
* Added `Binding.TwoWay.id` to API
* Removed trace logging during a successful set in a `SubModelSelectedItem` binding
* Added one-way-to-source binding
* Added function-based API for for one-way bindings
* Added composable monomorphic dispatch wrapping
* Switched the order of inputs in the function given to `Binding.mapMsgWithModel`.  This is breaking for public API introduced in 4.0.0-beta-1.
* Added `alterMsgStream` to API.  This feature is a replacement for what was previously called `wrapDispatch`.

#### 4.0.0-beta-41
* Fixed "backwards typing" (#373) and other bugs (like #371) introduced in 4.0.0-beta-40 with more careful use of the Dispatcher (#374)

#### 4.0.0-beta-40
* Removed recently added trace logging of INotifyDataErrorInfo.HasErrors (#354)
* Fixed typos in documentation and logging (#357)
* Fixed race condition with Dispatcher (#359)
* Removed overload of `ViewModel<_,_>.ToString` because of slow performance (#370)

#### 4.0.0-beta-3
* Added support for composable binding stickiness `sticky`
* Added support for composable binding validation `withValidation`
* Added support for composable binding laziness via `lazy'`
* Improved logging
* Changed CurrentModel and UpdateModel on ViewModel<_,_> from public to internal
* `runWindow` now shows the given window after settings its `DataContext`.  This removes the need to have `Visibility` values default to `Collapsed`.
* Changed minimum Elmish version from 3.0.3 to 3.1.0 (which is currently the latest).  Commands created with `OfAsync` are now executed on a threadpool thread.  For example, it is now easier to show file dialogs without blocking the Elmish dispatch loop.  See [this diff](https://github.com/elmish/Elmish.WPF/commit/d1ec823ccd7f377a860b76bc2358706dc6a70c84).

#### 4.0.0-beta-2
* Added logging when INotifyDataErrorInfo.HasErrors is called
* Fixed bug in INotifyDataErrorInfo.HasErrors where `true` always returned after `true` first correctly returned

#### 4.0.0-beta-1

* **Breaking:** Removed the obsolete binding functions in the `BindingFn` module
* **Breaking:** Removed the obsolete function `Elmish.WPF.Cmd.showWindow`
* **Breaking:** Removed all occurrences of the argument `wrapDispatch` from the methods used to create a binding. There is currently no migration path. Please create an issue if this is a negative impact for you.
* **Breaking:** App initialization is now done using the `WpfProgram` module instead of the `Program` module
* **Breaking:** Removed `ElmConfig`. For controlling logging, see below. For specifying a binding performance log threshold (corresponding to the old `ElmConfig.MeasureLimitMs` field), use `WpfProgram.withBindingPerformanceLogThreshold`
* **Breaking:** The method `Binding.oneWaySeq` is implemented by calling the method `Binding.oneWaySeqLazy` with `equals` = `refEq` and `map` = `id`. This is a breaking change when using a mutable data structure for the sequence. Compensate by directly calling `Binding.oneWaySeqLazy` with `equals` = `fun _ _ = false`.
* **Breaking:** Some calls to `Binding` methods now include an equality constraint. This only is only breaking if the corresponding type included the `NoEquality` attribute.
* Added binding mapping functions
  * Added `mapModel`, `mapMsg`, and `mapMsgWithModel` in both the `Binding` and `Bindings` modules
  * These functions enable common model and message mapping logic to be extracted
  * See the `SubModelSeq` sample for an excellent use of `mapModel` and `mapMsg`
* Improved logging:
  * Now uses `Microsoft.Extensions.Logging` for wide compatibility and easy integration into common log frameworks
  * Use `WpfProgram.WithLogger` to pass an `ILoggerFactory` for your chosen log framework
  * Can control specific log categories
  * See the samples for a demonstration using Serilog

#### 3.5.8
* Removed overload of `ViewModel<_,_>.ToString` because of slow performance (#370)

#### 3.5.7
* Excluded 4.* prereleases from possibilities for version of Elmish dependency
* Added support for multiple validation errors
* Added target `net5.0-windows`
* `ViewModel<'model, 'msg>` now overrides `object.ToString()` and returns a string representation of the current `'model` instance.  This is only intended for debugging.  No guarantees are given about the exact structure of the returned string.
* Fixed incorrect spelling of a word in a log message

#### 3.5.6

* The amount of time used to update `OneWaySeq` and `SubModelSeq` bindings has been significantly decreased.  This includes all cases of a `SubModelSeq` binding and all cases of a `OneWaySeq` binding for which `equals` returns `false`.

#### 3.5.5

* Fix exception when showing sub-windows as part of `init`

#### 3.5.4

* Windows may now be created on any thread

#### 3.5.3

* Fix crash when `init` returns a command opening a `subModelWin`

#### 3.5.2

* Improved error handling when collection bindings contain duplicates

#### 3.5.1

* Improve performance of `Binding.oneWaySeq`, `Binding.oneWaySeqLazy`, and `Binding.subModelSeq`

#### 3.5.0

* Add `netcoreapp3.0` target

#### 3.4.1

* Corrected type parameter of `getId` in `oneWaySeqLazy`

#### 3.4.0

* Make model/dispatch available to `getWindow` in `Binding.subModelWin`

#### 3.3.0

* Added optional dispatch wrapper to two-way bindings and command bindings, which allows dispatches to be throttled/debounced etc.

#### 3.2.1

* Fixed `ElmConfig.MeasureLimitMs` not being used

#### 3.2.0

* Added proper dialog/window support using `Binding.subModelWin`. See [the readme](https://github.com/elmish/Elmish.WPF/tree/feature-windows-binding#can-i-open-new-windowsdialogs) for more and the [NewWindow sample](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples) for an example.
* Deprecated `Cmd.showWindow` (use `Binding.subModelWin` instead)

#### 3.1.0

* Added `Program.withDebugTrace` which is similar to `withConsoleTrace` but writes using `System.Diagnostics.Debug.WriteLine` (e.g. to the VS output window)

#### 3.0.0

* The most massive (and hopefully useful) update yet!
* Breaking: Overload-based syntax for `Binding`. The old `Binding` module is deprecated and renamed to `BindingFn`.  The new `Binding` is a static class with static methods, providing many overloads for flexibility. To migrate, replace all occurrences of `Binding.` with `BindingFn.` and follow the deprecation warnings.
* Breaking: The `Elmish.WPF.Internal` namespace has been removed and everything in it that should actually be internal has been marked `internal`. This includes `ViewModel`.
* Breaking: `Elmish.WPF.Internal.BindingSpec<_,_>` has been moved/renamed to `Elmish.WPF.Binding<_,_>`. It should thus be more pleasant to use in type annotations.
* Breaking: `Elmish.WPF.Utilities.ViewModel.designInstance` has been moved to `Elmish.WPF.ViewModel`. Furthermore, it returns `obj` since `ViewModel` is internal.
* Breaking: Removed `twoWayIfValid`. It hasn’t worked for a while due to core Elmish internals, and was of suspect utility anyway.
* New: Many more helpful `Binding` signatures available due to the new overload-based syntax.
* New: More general `Binding.subModel` and `Binding.subModelSeq` overloads that allow a more idiomatic Elm architecture even with static views. For background information, see [#86](https://github.com/elmish/Elmish.WPF/issues/86) (the issue is otherwise outdated).
* New: Sticky `subModelOpt` bindings that returns the last non-null model when model is `None` (useful when animating out stuff)
* New: `elmEq` and `refEq` as useful equality defaults for lazy bindings. `elmEq` efficiently uses reflection to do a comparison for each member that is referential for reference types except strings, and structural for strings and value types.
* New: `Program.mkSimpleWpf` and `Program.mkProgramWpf` with more WPF-friendly signatures.
* New: `Program.mkProgramWpfWithCmdMsg` for easily following the `CmdMsg` pattern to allow testable commands. See the FAQ in the readme for details.
* New: `Cmd.showWindow` helper to open a new window.
* New: Slow calls can be logged (configurable threshold).
* New: Made available `Program.startElmishLoop` which is a low-level function that starts an Elmish loop given an Elmish `Program` and a WPF `FrameworkElement`. You probably won’t need it.
* Improvement: Logs now indicate the binding path.
* Improvement: Possibly better performance due to internals now using `ValueOption` instead of `Option`.
* Improvement: Finally added (lots of) unit tests, so confidence of correct functionality is higher. (No critical bugs were found when creating the tests.)

#### 2.0.0

* No changes, but updated for Elmish 3.0 so the package can finally move out of beta

#### 2.0.0-beta-11

* Add Binding.subModelSelectedItem

#### 2.0.0-beta-10

* Fix checkboxes erroneously being shown as failing validation (#78) by @BillHally
* The above fix also fixes binding warnings for two-way bindings

#### 2.0.0-beta-9

* Add new bindings `oneWayOpt` and `twoWayOpt` ([#75](https://github.com/elmish/Elmish.WPF/issues/75))
* Update to Elmish 3.0.0-beta-7

#### 2.0.0-beta-8

* Add new binding `subBindingSeq`, see readme for details.

#### 2.0.0-beta-7

* Fix Elmish dependency version in nuget spec

#### 2.0.0-beta-6

* Update to Elmish 3
* Dispatch on UI thread to block instead of getting weird UI behaviour from race conditions when updates take too long

#### 2.0.0-beta-5

* Fix `subModelSeq` items  being unselected during updates

#### 2.0.0-beta-4

* Breaking: Change order of `oneWayLazyWith` arguments to and rename it to `oneWayLazy`, removing the existing `oneWayLazy` function. The rationale is explained in [#60](https://github.com/elmish/Elmish.WPF/issues/60) . To migrate from 2.0.0-beta-3 to 2.0.0-beta-4: Add `(=)` as the `equals` parameter to `oneWayLazy` usages, and rename `oneWayLazyWith` usages to `oneWayLazy`.
* Add `Binding.oneWaySeqLazy`

#### 2.0.0-beta-3

* Add convenience function to create design-time VMs

#### 2.0.0-beta-2

* Improve log messages

#### 2.0.0-beta-1

* Complete rewrite, several breaking changes and new features
* `twoWayValidation` is called `twoWayIfValid` (because that’s what it is, and it clearly separates it from the new `twoWayValidate`)
* `oneWayMap` is called `oneWayLazy` (its implementation has changed, and the use case has expanded, but is similar)
* `cmd` and `cmdIf` have been renamed `paramCmd` and `paramCmdIf`, because the old names have new signatures/use-cases
* `model` has been renamed `subModel` because it’s more clear, and consistent with the new `subModelOpt` and `subModelSeq`
* `Program.runDebugWindow` has been removed in favour of `Program.runWindowWithConfig`
* Bundled Elmish has been removed, and Elmish 2.0 is used as an external dependency
* Any `Application ` instance instantiated before calling `Program.run...` will now be used
* Several new functions in the `Binding` module; dot into it in your IDE or see the repository for samples or source code

#### 1.0.0-beta-7

* Fix for #19, model to view updates for validation bindings

#### 1.0.0-beta-6

* Implemented INotifyDataErrorInfo and corresponding bindings
* Added some documentation for binding assemblers
* Added message box error handler to Program module

#### 1.0.0-beta-5

* Target F# 4.1
* Latest fable-elmish, includes memory leak fix

#### 1.0.0-beta-4

* Added Program.withExceptionHandler

#### 1.0.0-beta-3

* Fixing nuget framework version

#### 1.0.0-beta-2

* Added command parameter
* Renamed Binding.vm to Binding.model
* Reorganized samples and added performance sample (WIP)

#### 1.0.0-beta-1

* Fixing two way binding bugs

#### 0.9.0-beta-9

* Elmish all the WPF!
