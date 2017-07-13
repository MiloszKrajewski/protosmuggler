#r "./.fake/FakeLib.dll"
#r "System.Xml.dll"
#r "System.Xml.Linq.dll"
#load "./shared/BuildTools.fsx"

open Fake
open System.IO
open System.Xml
open System.Xml.Linq

Target "Clean" (fun _ -> !! "**/bin/" ++ "**/obj/" |> DeleteDirs)

Target "Purge" (fun _ -> ["packages"; ".output"] |> CleanDirs)

Target "Build" (fun _ -> 
    Proj.listProj () |> Seq.iter Proj.updateVersion
    Proj.build "src/"
)

Target "Restore" (fun _ -> Proj.restore "src/")

Target "Nuget:Pack" (fun _ -> 
    Proj.restore "src/"
    Proj.releaseNupkg ()
)

Target "Nuget:Push" (fun _ ->
    let nugetAddress = Proj.secrets |> Config.valueOrDefault "nuget" "address" "https://www.nuget.org"
    let nugetApiKey = Proj.secrets |> Config.valueOrFail "nuget" "apikey"
    Proj.listNupkg () 
    |> Seq.iter (fun (coreName, version) ->
        Shell.run "dotnet" (sprintf "nuget push %s.%s.nupkg -k %s -s %s" coreName version nugetApiKey nugetAddress)
    )
)

"Clean" ==> "Purge"
"Clean" ?=> "Build"
"Purge" ?=> "Build"
"Restore" ==> "Build" ==> "Nuget:Pack"
"Nuget:Pack" ==> "Nuget:Push"

RunTargetOrDefault "Build"