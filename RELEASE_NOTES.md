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
* Lastest fable-elmish, includes memory leak fix

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
