#r "paket: groupref Build //"
#load ".fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators


Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    |> Shell.cleanDirs
)

Target.create "Build" (fun _ ->
    // Building XAML projects (such as the sample view projects) doesn't work
    // yet using DotNet.build, so we skip those.
    !! "src/Elmish.WPF/*.*proj"
    |> Seq.iter (DotNet.build id)
)

let release = File.read "RELEASE_NOTES.md" |> ReleaseNotes.parse

Target.create "Pack" (fun _ ->
  Paket.pack(fun p ->
    { p with
        OutputPath = "deploy"
        Symbols = true
        Version = release.NugetVersion
        ReleaseNotes = String.toLines release.Notes})
)

Target.create "All" ignore

"Clean"
  ==> "Build"
  ==> "All"

"All"
  ==> "Pack"

Target.runOrDefault "All"
