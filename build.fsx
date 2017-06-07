// include Fake lib
#r @"packages\FAKE\tools\FakeLib.dll"
open Fake

let buildDir = "./build_output/"

// Targets
Target "Default" (fun _ ->
    trace "Hello World from FAKE"
)

Target "BuildApp" (fun _ ->
    !! "src/Elmish.WPF.sln"
       |> MSBuildRelease buildDir "Build"
       |> Log "AppBuild-Output: "
)

Target "Clean" (fun _ ->
    CleanDir buildDir
)

// Dependencies
"Clean"
  ==> "BuildApp"
  ==> "Default"

// start build
RunTargetOrDefault "Default"