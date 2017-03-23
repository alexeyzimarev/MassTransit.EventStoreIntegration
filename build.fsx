// include Fake libs
#I "packages/FAKE/tools/"
#r "packages/FAKE/tools/FakeLib.dll"
#r "packages/FAKE/tools/Fake.Deploy.Lib.dll"

open Fake
open Fake.Paket
open Fake.FileUtils
open Fake.Testing.XUnit2
open Fake.PaketTemplate
open Fake.AssemblyInfoFile
open Fake.ProcessHelper
open Fake.FileHelper
open Fake.Json
open Fake.Testing.NUnit3

let pathInfo = directoryInfo "."
let product = environVarOrDefault "BAMBOO_productName" pathInfo.Name
let company = "Alexey Zimarev"
let copyright = "Copyright © " + System.DateTime.UtcNow.Year.ToString() + " " + company

// Directories
let rootDir = currentDirectory
let buildDir  = currentDirectory + "/bin/"
let testsDir  = currentDirectory + "/tests/"
let nugetDir = currentDirectory + "/nuget"
let testOutputDir = currentDirectory + "/"

let gitversion = "packages/GitVersion.CommandLine/tools/GitVersion.exe"

// Filesets
let appReferences =
    !! "src/**/*.csproj"
      ++ "src/**/*.fsproj"
      -- "src/**/*Tests.csproj"

let testReferences =
    !! "src/**/*Tests.csproj"

type Version = {
    Major: int
    Minor: string
    Patch: string
    PreReleaseTag: string
    PreReleaseTagWithDash: string
    BuildMetaData: string
    BuildMetaDataPadded: string
    FullBuildMetaData: string
    MajorMinorPatch: string
    SemVer: string
    LegacySemVer: string
    LegacySemVerPadded: string
    AssemblySemVer: string
    FullSemVer: string
    InformationalVersion: string
    BranchName: string
    Sha: string
    NuGetVersionV2: string
    NuGetVersion: string
    CommitDate: string
}
let mutable gitVer = Unchecked.defaultof<Version>

// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; testsDir; nugetDir ]
    MSBuildRelease buildDir "Clean" appReferences
        |> Log "Clean-Output: "
)

Target "SetVersion" (fun _ ->

    let result = ExecProcessAndReturnMessages (fun info ->
        info.FileName <- gitversion
        info.WorkingDirectory <- "."
        info.Arguments <- "/output json") (System.TimeSpan.FromMinutes 5.0)

    if result.ExitCode <> 0 then failwithf "'GitVersion.exe' returned with a non-zero exit code"

    let jsonResult = System.String.Concat(result.Messages)

    jsonResult |> deserialize<Version> |> fun ver -> gitVer <- ver

    CreateCSharpAssemblyInfo "./src/SolutionAssemblyInfo.cs"
        [Attribute.Product product
         Attribute.Company company
         Attribute.Copyright copyright
         Attribute.Version gitVer.AssemblySemVer
         Attribute.FileVersion (gitVer.MajorMinorPatch + ".0")
         Attribute.InformationalVersion gitVer.InformationalVersion]
)

Target "BuildApp" (fun _ ->
    MSBuildRelease buildDir "Build" appReferences
        |> Log "BuildApp-Output: "
)

Target "BuildTests" (fun _ ->
    MSBuildRelease testsDir "Build" testReferences
        |> Log "BuildTests-Output: "
)

Target "Pack" (fun _ ->
    Pack (fun p ->
        {p with
            OutputPath = nugetDir
            WorkingDir = rootDir + "/src"
            Version = gitVer.NuGetVersionV2
            MinimumFromLockFile = true
         })
)

Target "Test" (fun _ ->
    !! (testsDir + "/*Tests.dll")
      |> NUnit3 (fun p ->
          {p with
             ToolPath = "packages\\NUnit.ConsoleRunner\\tools\\nunit3-console.exe"
          })
)

// Build order
"Clean"
  ==> "SetVersion"
  ==> "BuildApp"
//  ==> "BuildTests"
//  ==> "Test"
  ==> "Pack"

// start build
RunTargetOrDefault "Pack"
