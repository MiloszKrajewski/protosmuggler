#r "./.fake/FakeLib.dll"
#load "./shared/BuildTools.fsx"

open Fake

Target "Clean" (fun _ -> !! "**/bin/" ++ "**/obj/" |> DeleteDirs)

Target "Purge" (fun _ -> DeleteDir "packages")

Target "Build" (fun _ -> Proj.build "src/")

Target "Restore" (fun _ -> Proj.restore "src/")

Target "Pack" (fun _ -> Proj.releaseNupkg ())

"Clean" ==> "Purge"

RunTargetOrDefault "Build"