#addin "nuget:?package=Cake.Git&version=0.16.1"
///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var solutions       = GetFiles("./**/*.sln");
var solutionPaths   = solutions.Select(solution => solution.GetDirectory());
var buildProject    = "Build/Build.proj";
var artifactsPath   = MakeAbsolute(Directory("./Artifacts/" + configuration));
var symbolsPath     = artifactsPath.Combine("symbols");
var testResultsPath = artifactsPath.Combine("TestResults");
var commitSha       = GitLogTip("./").Sha;
var commitTxtPath   = MakeAbsolute(File("./Kudu.Services.Web/commit.txt"));
var buildNumber     = (AppVeyor.IsRunningOnAppVeyor) ? AppVeyor.Environment.Build.Number.ToString() : "";
if (buildNumber.Length > 3)
{
    buildNumber = buildNumber.Substring(buildNumber.Length-3).TrimStart('0');
}

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
    // Executed BEFORE the first task.
    Information("Running tasks {0}...", buildNumber);
});

Teardown(ctx =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("CreateOutputPath")
    .Does(() =>
{
    // Create output directories.
    if (!DirectoryExists(artifactsPath))
    {
        Information("Creating path {0}", artifactsPath);
    }
    if (!DirectoryExists(symbolsPath))
    {
        Information("Creating path {0}", symbolsPath);
    }
    if (!DirectoryExists(testResultsPath))
    {
        Information("Creating path {0}", testResultsPath);
    }
});

Task("Clean")
    .Does(() =>
{
    // Clean solution directories.
    foreach(var path in solutionPaths)
    {
        Information("Cleaning {0}", path);
        CleanDirectories(path + "/**/bin/" + configuration);
        CleanDirectories(path + "/**/obj/" + configuration);
    }
});

Task("Restore")
    .Does(() =>
{
    // Restore all NuGet packages.
    foreach(var solution in solutions)
    {
        Information("Restoring {0}...", solution);
        NuGetRestore(solution);
    }
});

Task("UpdateVersion")
    .Does(() =>
{
    // Update version.
    Information("Updating version {0}", buildProject);
    MSBuild(buildProject, settings =>
        settings.WithTarget("UpdateVersion")
            .UseToolVersion(MSBuildToolVersion.VS2015)
            .WithProperty("TreatWarningsAsErrors","true")
            .WithProperty("build_number",buildNumber)
            .SetConfiguration(configuration));

});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("UpdateVersion")
    .IsDependentOn("CreateOutputPath")
    .Does(() =>
{
    // Build main solution projects.
    Information("Building {0}", buildProject);
    MSBuild(buildProject, settings =>
        settings.WithTarget("Build")
            .UseToolVersion(MSBuildToolVersion.VS2015)
            .WithProperty("TreatWarningsAsErrors","true")
            .WithProperty("ExcludeXmlAssemblyFiles", "false")
            .WithProperty("build_number",buildNumber)
            .SetVerbosity(Verbosity.Minimal)
            .SetNodeReuse(false)
            .SetConfiguration(configuration));
});

Task("BuildSites")
    .IsDependentOn("Build")
    .Does(() =>
{
    // Build sites.
    Information("Storing commit sha {0} to {1}", commitSha, commitTxtPath);
    System.IO.File.WriteAllText(
        "Kudu.Services.Web/commit.txt",
        GitLogTip("./").Sha
        );

    Information("Building sites {0}", buildProject);
    MSBuild(buildProject, settings =>
        settings.WithTarget("BuildSites")
            .UseToolVersion(MSBuildToolVersion.VS2015)
            .WithProperty("TreatWarningsAsErrors","true")
            .WithProperty("ExcludeXmlAssemblyFiles", "false")
            .WithProperty("build_number",buildNumber)
            .SetVerbosity(Verbosity.Minimal)
            .SetNodeReuse(false)
            .SetConfiguration(configuration));
});

Task("BuildZips")
    .IsDependentOn("BuildSites")
    .Does(() =>
{
    // Build zip files.
    Information("Building zips {0}", buildProject);
    MSBuild(buildProject, settings =>
        settings.WithTarget("BuildZips")
            .UseToolVersion(MSBuildToolVersion.VS2015)
            .WithProperty("TreatWarningsAsErrors","true")
            .WithProperty("build_number",buildNumber)
            .SetConfiguration(configuration));
});

Task("CopySymbols")
    .IsDependentOn("BuildSites")
    .Does(() =>
{
    // Copy symbol files.
    Information("Building sites {0}", buildProject);
    MSBuild(buildProject, settings =>
        settings.WithTarget("CopySymbols")
            .UseToolVersion(MSBuildToolVersion.VS2015)
            .WithProperty("TreatWarningsAsErrors","true")
            .WithProperty("build_number",buildNumber)
            .SetConfiguration(configuration));
});

Task("BuildNuget")
    .IsDependentOn("BuildSites")
    .Does(() =>
{
    // Create nuget packages
    Information("Building nuget packages {0}", buildProject);
    MSBuild(buildProject, settings =>
        settings.WithTarget("BuildNuget")
            .UseToolVersion(MSBuildToolVersion.VS2015)
            .WithProperty("TreatWarningsAsErrors","true")
            .WithProperty("build_number",buildNumber)
            .SetConfiguration(configuration));
});

///////////////////////////////////////////////////////////////////////////////
// TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("BuildZips")
    .IsDependentOn("CopySymbols")
    .IsDependentOn("BuildNuget");


Task("AppVeyor")
    .IsDependentOn("BuildZips")
    .IsDependentOn("CopySymbols")
    .IsDependentOn("BuildNuget");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);