﻿#I @"tools/FAKE/tools"
#r "FakeLib.dll"

open System
open System.IO
open System.Text

open Fake
open Fake.DotNetCli
open Fake.DocFxHelper

// Variables
let configuration = "Release"

// Directories
let output = __SOURCE_DIRECTORY__  @@ "build"
let outputTests = output @@ "tests"
let outputBinaries = output @@ "binaries"
let outputNuGet = output @@ "nuget"

let buildNumber = environVarOrDefault "BUILD_NUMBER" "0"
let preReleaseVersionSuffix = "beta" + (if (not (buildNumber = "0")) then (buildNumber) else "")
let versionSuffix = 
    match (getBuildParam "nugetprerelease") with
    | "dev" -> preReleaseVersionSuffix
    | _ -> ""

Target "Clean" (fun _ ->
    CleanDir output
    CleanDir outputTests
    CleanDir outputBinaries
    CleanDir outputNuGet

    CleanDirs !! "./**/bin"
    CleanDirs !! "./**/obj"
)

Target "RestorePackages" (fun _ ->
    DotNetCli.Restore
        (fun p -> 
            { p with
                Project = "./src/Akka.DI.AutoFac.sln"
                NoCache = false })
)

Target "Build" (fun _ ->
        DotNetCli.Build
            (fun p -> 
                { p with
                    Project = "./src/Akka.DI.AutoFac.sln"
                    Configuration = configuration })
)

//--------------------------------------------------------------------------------
// Tests targets 
//--------------------------------------------------------------------------------

Target "RunTests" (fun _ ->
    let projects = !! "./**/*.Tests.csproj"

    let runSingleProject project =
        DotNetCli.RunCommand
            (fun p -> 
                { p with 
                    WorkingDir = (Directory.GetParent project).FullName
                    TimeOut = TimeSpan.FromMinutes 10. })
                (sprintf "xunit -parallel none -teamcity -xml %s_xunit.xml" (outputTests @@ fileNameWithoutExt project)) 

    projects |> Seq.iter (runSingleProject)
)

//--------------------------------------------------------------------------------
// Nuget targets 
//--------------------------------------------------------------------------------

Target "CreateNuget" (fun _ ->
    DotNetCli.Pack
        (fun p -> 
            { p with
                Project = "./**/Akka.Logger.Serilog.csproj"
                Configuration = configuration
                AdditionalArgs = ["--include-symbols"]
                VersionSuffix = versionSuffix
                OutputPath = outputNuGet })
)

//--------------------------------------------------------------------------------
//  Target dependencies
//--------------------------------------------------------------------------------

Target "BuildRelease" DoNothing
Target "All" DoNothing
Target "Nuget" DoNothing

// build dependencies
"Clean" ==> "RestorePackages" ==> "Build" ==> "BuildRelease"

// tests dependencies
"Clean" ==> "RestorePackages" ==> "RunTests"

// nuget dependencies
"Clean" ==> "RestorePackages" ==> "Build" ==> "CreateNuget"

// all
"BuildRelease" ==> "All"

RunTargetOrDefault "All"