// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"
open Fake 
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System

// --------------------------------------------------------------------------------------
// START TODO: Provide project-specific details below
// --------------------------------------------------------------------------------------

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package 
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docs/tools/generate.fsx"

// The name of the project 
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "fszmq"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "An Apache-licenced F# binding for the �MQ distributed programming library."

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = """An Apache-licenced F# binding for the �MQ distributed programming library."""

// List of author names (for NuGet package)
let authors = [ "Paulmichael Blasucci" ]
// Tags for your project (for NuGet package)
let tags = "F# fsharp zeromq zmq 0MQ distributed concurrent parallel messaging transport"

// File system information 
// (<solutionFile>.sln and <solutionFile>.Tests.sln are built during the building)
let solutionFile  = "fszmq"
// Pattern specifying assemblies to be tested using NUnit
let testAssemblies = ["tests/*/bin/*/fszmq*tests*.dll"]

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted 
let gitHome = "https://github.com/pblasucci"
// The name of the project on GitHub
let gitName = "fszmq"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps 
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let release = parseReleaseNotes (IO.File.ReadAllLines "RELEASE_NOTES.md")

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
  let fileName = "src/" + project + "/AssemblyInfo.fs"
  CreateFSharpAssemblyInfo fileName
      [ Attribute.Title project
        Attribute.Product project
        Attribute.Description summary
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion ] 
)

// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages

Target "RestorePackages" (fun _ ->
    !! "./**/packages.config"
    |> Seq.iter (RestorePackage (fun p -> { p with ToolPath = "./.nuget/NuGet.exe" }))
)

Target "Clean" (fun _ ->
    CleanDirs ["bin"; "temp"]
)

Target "CleanDocs" (fun _ ->
    CleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

Target "Build" (fun _ ->
    { BaseDirectories = [__SOURCE_DIRECTORY__]
      Includes = [ solutionFile +       ".sln"
                   solutionFile + ".Tests.sln" ]
      Excludes = [] } 
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target "RunTests" (fun _ ->
    let xunitVersion = GetPackageVersion "packages" "xunit.runners"
    let xunitPath = sprintf "packages/xunit.runners.%s/tools/xunit.console.clr4.x86.exe" xunitVersion
    { BaseDirectories = [__SOURCE_DIRECTORY__]
      Includes = testAssemblies
      Excludes = [] } 
    |> xUnit (fun p ->
        { p with
            ToolPath    = xunitPath
            ShadowCopy  = true
            TimeOut     = TimeSpan.FromMinutes 20.
            OutputDir   = @".\bin\"
            HtmlOutput  = true })
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->
    // Format the description to fit on a single line (remove \r\n and double-spaces)
    let description = description.Replace("\r", "")
                                 .Replace("\n", "")
                                 .Replace("  ", " ")
    let nugetPath = ".nuget/nuget.exe"
    NuGet (fun p -> 
        { p with   
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = release.NugetVersion
            ReleaseNotes = String.Join(Environment.NewLine, release.Notes)
            Tags = tags
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] })
        ("nuget/" + project + ".nuspec")
)

// --------------------------------------------------------------------------------------
// Generate the documentation

Target "GenerateDocs" (fun _ ->
    executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"] [] |> ignore
)

// --------------------------------------------------------------------------------------
// Release Scripts

Target "ReleaseDocs" (fun _ ->
    let ghPages      = "gh-pages"
    let ghPagesLocal = "temp/gh-pages"
    Repository.clone "temp" (gitHome + "/" + gitName + ".git") ghPages
    Branches.checkoutBranch ghPagesLocal ghPages
    CopyRecursive "docs/output" ghPagesLocal true |> printfn "%A"
    CommandHelper.runSimpleGitCommand ghPagesLocal "add ." |> printfn "%s"
    let cmd = sprintf """commit -a -m "Update generated documentation for version %s""" release.NugetVersion
    CommandHelper.runSimpleGitCommand ghPagesLocal cmd |> printfn "%s"
    Branches.push ghPagesLocal
)

Target "Release" DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "RestorePackages"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "RunTests"
  ==> "All"

"All" 
  ==> "CleanDocs"
  ==> "GenerateDocs"
  ==> "ReleaseDocs"
  ==> "NuGet"
  ==> "Release"

RunTargetOrDefault "All"
