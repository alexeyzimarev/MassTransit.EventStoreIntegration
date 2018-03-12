#r @"packages/FAKE/tools/FakeLib.dll"
open System.IO
open Fake
open Fake.AssemblyInfoFile
open Fake.Git.Information
open Fake.SemVerHelper
open System

let buildArtifactPath = FullName "./build_artifacts"
let packagesPath = FullName "./packages"

let assemblyVersion = "1.0.1.0"
let baseVersion = "1.0.1"

let envVersion = (environVarOrDefault "APPVEYOR_BUILD_VERSION" (baseVersion + ".0"))
let buildVersion = (envVersion.Substring(0, envVersion.LastIndexOf('.')))

let semVersion : SemVerInfo = (parse buildVersion)

let Version = semVersion.ToString()

let branch = (fun _ ->
  (environVarOrDefault "APPVEYOR_REPO_BRANCH" (getBranchName "."))
)

let FileVersion = (environVarOrDefault "APPVEYOR_BUILD_VERSION" (Version + "." + "0"))

let informationalVersion = (fun _ ->
  let branchName = (branch ".")
  let label = if branchName="master" then "" else " (" + branchName + "/" + (getCurrentSHA1 ".").[0..7] + ")"
  (FileVersion + label)
)

let nugetVersion = (fun _ ->
  let branchName = (branch ".")
  let label = if branchName="master" then "" else "-" + (if branchName="mt3" then "beta" else branchName)
  let version = if branchName="master" then Version else FileVersion
  (version + label)
)

let InfoVersion = informationalVersion()
let NuGetVersion = nugetVersion()

let versionArgs = [ @"/p:Version=""" + NuGetVersion + @""""; @"/p:PackageVersion=""" + NuGetVersion + @""""; @"/p:AssemblyVersion=""" + FileVersion + @""""; @"/p:FileVersion=""" + FileVersion + @""""; @"/p:InformationalVersion=""" + InfoVersion + @"""" ]

printfn "Using version: %s" Version

Target "Clean" (fun _ ->
  ensureDirectory buildArtifactPath

  CleanDir buildArtifactPath
)

Target "RestorePackages" (fun _ -> 
  DotNetCli.Restore (fun p -> { p with Project = "./src/"
                                       AdditionalArgs = versionArgs })
)

Target "Build" (fun _ ->
  DotNetCli.Build (fun p-> { p with Project = @".\src\MassTransit.EventStoreIntegration.sln"
                                    Configuration= "Release"
                                    AdditionalArgs = versionArgs })
)

Target "Package" (fun _ ->
  DotNetCli.Pack (fun p-> { p with 
                                Project = @".\src\MassTransit.EventStoreIntegration.sln"
                                Configuration= "Release"
                                OutputPath= buildArtifactPath
                                AdditionalArgs = versionArgs })
)

Target "Default" (fun _ ->
  trace "Starting..."
)

"Clean"
  ==> "RestorePackages"
  ==> "Build"
  ==> "Package"
  ==> "Default"

RunTargetOrDefault "Default"