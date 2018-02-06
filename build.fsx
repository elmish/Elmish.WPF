// include Fake lib
#r "./packages/build/FAKE/tools/FakeLib.dll"
open Fake
open System
open Fake.ReleaseNotesHelper

let buildDir = "./build_output/"

let projects = !!"src/**/*.fsproj"

let dotnetcliVersion = DotNetCli.GetDotNetSDKVersionFromGlobalJson()
let mutable dotnetExePath = "dotnet"

let runDotnet workingDir =
    DotNetCli.RunCommand (fun p -> { p with ToolPath = dotnetExePath
                                            WorkingDir = workingDir } )

Target "InstallDotNetCore" (fun _ ->
   dotnetExePath <- DotNetCli.InstallDotNetSDK dotnetcliVersion
)

Target "Clean" (fun _ ->
    projects
    |> Seq.iter (IO.Path.GetDirectoryName >> sprintf "%s/obj" >> CleanDir)
    CleanDir "build_output"
)

Target "Install" (fun _ ->
    projects
    |> Seq.iter (fun p -> let d = IO.Path.GetDirectoryName p in runDotnet d "restore")
)

Target "Build" (fun _ ->
    !! "src/*.sln"
    |> MSBuildRelease buildDir "Build"
    |> Log "AppBuild-Output: "
)

let release = LoadReleaseNotes "RELEASE_NOTES.md"

Target "Meta" (fun _ ->
    [ "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
      "<PropertyGroup>"
      "<PackageProjectUrl>https://github.com/prolucid/Elmish.WPF</PackageProjectUrl>"
      "<PackageLicenseUrl>https://raw.githubusercontent.com/prolucid/Elmish.WPF/master/LICENSE.md</PackageLicenseUrl>"
      "<PackageIconUrl>https://raw.githubusercontent.com/prolucid/Elmish.WPF/master/docs/files/img/logo.png</PackageIconUrl>"
      "<RepositoryUrl>https://github.com/prolucid/Elmish.WPF.git</RepositoryUrl>"
      "<PackageTags>elmish;fsharp</PackageTags>"
      sprintf "<PackageReleaseNotes>%s</PackageReleaseNotes>" (List.head release.Notes)
      "<Authors>Justin Sacks</Authors>"
      "<Description>F# bindings for using elmish in WPF</Description>"
      sprintf "<Version>%s</Version>" (string release.SemVer)
      "</PropertyGroup>"
      "</Project>"]
    |> WriteToFile false "Directory.Build.props"
)

// Build a NuGet package
Target "NuGet" (fun _ ->
    Paket.Pack(fun p -> 
        { p with
            OutputPath = buildDir
            TemplateFile = "paket.template"
            Version = release.NugetVersion
            ReleaseNotes = toLines release.Notes})
)

Target "PublishNuget" (fun _ ->
    Paket.Push(fun p -> 
        { p with
            WorkingDir = buildDir })
)

Target "All" DoNothing

"Clean"
  ==> "Meta"
  ==> "Install"
  ==> "Build"
  ==> "All"

"All" 
  ==> "NuGet"
  ==> "PublishNuget"


// start build
RunTargetOrDefault "All"