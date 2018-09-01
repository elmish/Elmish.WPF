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