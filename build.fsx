#r "./.fake/FakeLib.dll"
#r "System.Xml.dll"
#r "System.Xml.Linq.dll"
#load "./shared/BuildTools.fsx"

open Fake
open System.IO
open System.Xml
open System.Xml.Linq

Target "Clean" (fun _ -> !! "**/bin/" ++ "**/obj/" |> DeleteDirs)

Target "Purge" (fun _ -> DeleteDir "packages")

Target "Build" (fun _ -> 
    Proj.listProj () |> Seq.iter Proj.updateVersion
    Proj.build "src/"
)

Target "Restore" (fun _ -> Proj.restore "src/")

Target "Pack" (fun _ -> Proj.releaseNupkg ())

"Clean" ==> "Purge"

RunTargetOrDefault "Build"