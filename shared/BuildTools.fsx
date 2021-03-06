#r "../.fake/FakeLib.dll"
#r "System.Xml.dll"
#r "System.Xml.Linq.dll"

namespace Fake

open Fake
open System
open System.IO
open System.Text

[<AutoOpen>]
module Fx =
    let inline tap f a = f a; a
    let inline forgive f a = try f a with | _ -> ()

module String =
    let quote (arg: string) = if arg.Contains(" ") then sprintf "\"%s\"" arg else arg
    let replace pattern (replacement: string) (input: string) = input.Replace(pattern, replacement)
    let same textA textB = String.Compare(textA, textB, true) = 0

module Option =
    let inline def v o = defaultArg o v
    let inline alt a o = match o with | None -> a | _ -> o
    let inline cast<'a> (o: obj) = match o with | :? 'a as v -> Some v | _ -> None
    let inline ref o = match o with | null -> None | _ -> Some o

module File =
    let update modifier inputFile =
        let outputFile = Path.GetTempFileName()
        inputFile |> CopyFile outputFile
        modifier outputFile
        if CompareFiles true inputFile outputFile |> not then
            tracefn "File %s has been modified. Overwriting." inputFile
            outputFile |> Rename (inputFile |> tap DeleteFile)

    let touch filename = 
        if File.Exists(filename) 
        then FileInfo(filename).LastWriteTimeUtc <- DateTime.UtcNow
        else [] |> WriteFile filename
    
    let exists filename = File.Exists(filename)

module Regex =
    open System.Text.RegularExpressions

    let create ignoreCase pattern =
        let ignoreCase = if ignoreCase then RegexOptions.IgnoreCase else RegexOptions.None
        Regex(pattern, RegexOptions.ExplicitCapture ||| RegexOptions.IgnorePatternWhitespace ||| ignoreCase)
    let replace pattern (replacement: string) input = Regex.Replace(input, pattern, replacement)

    let matches pattern input = Regex.IsMatch(input, pattern)

    let (|Match|_|) (pattern: Regex) text =
        match pattern.Match(text) with | m when m.Success -> Some m | _ -> None

module Xml = 
    open System.Xml
    open System.Xml.Linq

    let load (filename: string) = 
        use reader = new StreamReader(filename)
        XDocument.Load(reader)

    let save (filename: string) (doc: XDocument) = 
        let settings = XmlWriterSettings(Indent = true, IndentChars = "\t")
        use writer = XmlWriter.Create(filename, settings)
        doc.Save(writer)

    let elems (name: string) (doc: XContainer) =
        doc.Elements() |> Seq.filter (fun e -> e.Name.LocalName = name)

    let elem (name: string) (doc: XContainer) = doc |> elems name |> Seq.tryHead

    let private splitPath (path: string) = (path.Split([|'/'|]) |> List.ofArray)

    let updateOrAdd (path: string) (value: string) (doc: XContainer) = 
        let getOrAdd (name: string) (element: XContainer) = 
            match elem name element with 
            | None -> XElement(XName.Get(name)) |> tap element.Add 
            | Some x -> x
        let rec update (path: string list) (element: XContainer) =
            match path with
            | [] -> element.RemoveNodes(); element.Add(XText(value))
            | "" :: tail -> element.Document |> update tail
            | "." :: tail -> element |> update tail
            | ".." :: tail -> element.Parent |> update tail
            | name :: tail -> (getOrAdd name element) |> update tail
        update (splitPath path) doc
    
    let findAll (path: string) (doc: XContainer) =
        let rec find (path: string list) (elements: XContainer seq) =
            match path with
            | [] -> elements |> Seq.choose Option.cast<XElement>
            | "" :: tail -> [doc.Document :> XContainer] |> find tail
            | "." :: tail -> elements |> find tail
            | ".." :: tail -> elements |> Seq.map (fun e -> e.Parent :> XContainer) |> find tail
            | name :: tail -> 
                elements 
                |> Seq.collect (elems name)
                |> Seq.choose Option.cast<XContainer> 
                |> List.ofSeq 
                |> find tail
        find (splitPath path) [doc]

module Config =
    type Item = { Section: string; Key: string; Value: string }
    let private sectionRx = """^\s*\[\s*(?<name>.*?)\s*\]\s*$""" |> Regex.create true
    let private valueRx = """^\s*(?<key>.*?)\s*=\s*(?<value>.*?)\s*$""" |> Regex.create true
    let private emptyRx = """^\s*(;.*)?$""" |> Regex.create true

    let validate (items: Item seq) = items |> Seq.distinctBy (fun i -> i.Section, i.Key) |> List.ofSeq
    let merge configs = configs |> Seq.collect id |> validate

    let load (lines: seq<string>) =
        let rec parse lines section result =
            match lines with
            | [] -> result
            | line :: lines ->
                match line with
                | Regex.Match emptyRx m -> parse lines section result
                | Regex.Match valueRx m ->
                    let item = { Section = section; Key = m.Groups.["key"].Value; Value = m.Groups.["value"].Value }
                    parse lines section (item :: result)
                | Regex.Match sectionRx m ->
                    let section = m.Groups.["name"].Value
                    parse lines section result
                | _ ->
                    printfn "Line '%s' does not match config pattern as has been ignored" line
                    parse lines section result
        parse (lines |> List.ofSeq) "" [] |> List.rev |> validate

    let tryLoadFile fileName = if File.Exists(fileName) then fileName |> ReadFile |> load else []
    let items section (config: Item seq) = config |> Seq.filter (fun i -> i.Section = section)
    let keys section (config: Item seq) = config |> items section |> Seq.map (fun i -> i.Key)
    let value section key (config: Item seq) =
        config |> items section |> Seq.filter (fun i -> i.Key = key) |> Seq.tryHead |> Option.map (fun i -> i.Value)
    let valueOrDefault section key defaultValue config =
        config |> value section key |> Option.def defaultValue
    let valueOrFail section key config =
        match config |> value section key with
        | Some v -> v | None -> failwithf "Value %s:%s could not be found" section key

module Shell =
    open System.Diagnostics

    let runAt directory executable arguments =
        let command = sprintf "%s %s" (String.quote executable) arguments |> tap (tracefn "> %s")
        let comspec = environVarOrFail "COMSPEC"
        let info = ProcessStartInfo(comspec, command |> sprintf "/c \"%s\"", UseShellExecute = false, WorkingDirectory = directory)
        let proc = Process.Start(info)
        proc.WaitForExit()
        match proc.ExitCode with | 0 -> () | c -> failwithf "Execution failed with error code %d" c

    let run executable arguments = runAt "." executable arguments

module Proj =
    let outputFolder = ".output"
    let templateFolder = "./.template.config"
    let releaseNotes = "./CHANGES.md" |> ReleaseNotesHelper.LoadReleaseNotes
    let timestamp = 
        let baseline = DateTime.Parse("2000-01-01T00:00:00") // ~y2k
        DateTime.Now.Subtract(baseline).TotalSeconds |> int |> sprintf "%8x"

    let packageVersion = releaseNotes.NugetVersion |> Regex.replace "-wip$" (timestamp |> sprintf "-wip%s")
    let assemblyVersion = releaseNotes.AssemblyVersion

    let secrets =
        ["."]
        |> Seq.map (sprintf "%s/.secrets.config" >> Config.tryLoadFile)
        |> Config.merge

    let listNupkg () =
        !! (outputFolder @@ "*.nupkg.latest")
        |> Seq.map (fun versionFile ->
            let coreName = versionFile |> Regex.replace "\\.nupkg\\.latest$" ""
            let version = versionFile |> ReadFile |> Seq.head
            (coreName, version)
        )
    
    let updateVersion projectFile = 
        projectFile |> File.update (fun fn ->
            Xml.load fn
            |> tap (Xml.updateOrAdd "/Project/PropertyGroup/Version" packageVersion)
            |> tap (Xml.updateOrAdd "/Project/PropertyGroup/AssemblyVersion" assemblyVersion)
            |> tap (Xml.updateOrAdd "/Project/PropertyGroup/FileVersion" assemblyVersion)
            |> Xml.save fn
        )

    let listProj () =
        let isValidProject projectPath =
            let projectFolder = projectPath |> directory |> filename
            let projectName = projectPath |> fileNameWithoutExt
            String.same projectFolder projectName
        !! "src/*/*.*proj" |> Seq.filter isValidProject

    let isTemplateProj projectFile =
        let templateJson = (projectFile |> directory) @@ ".template.config/template.json"
        File.Exists(templateJson)

    let isTestProj projectFile =
        projectFile |> directory |> (fun n -> ["\\.x?Tests?$"; "\\.Demo$"] |> Seq.exists (fun p -> Regex.matches p n))

    let build project =
        DotNetCli.Build (fun p -> 
            { p with 
                Project = project
                Configuration = "Release"
            })

    let restore project = DotNetCli.Restore (fun p -> { p with Project = project })

    let pack version project =
        DotNetCli.Pack (fun p -> 
            { p with 
                Project = project
                Configuration = "Release"
                OutputPath = outputFolder |> FullName
                // AdditionalArgs = ["--include-symbols"]
            })
        let versionFile = outputFolder @@ (project |> filename) |> sprintf "%s.nupkg.latest"
        [ version ] |> WriteFile versionFile

    let publish targetFolder project =
        DotNetCli.Publish (fun p ->
            { p with
                Project = project
                Configuration = "Release"
                Output = targetFolder |> FullName
            }
        )

    let fixPackReferences folder =
        let fileMissing filename = File.Exists(filename) |> not
        !! (folder @@ "**/paket.references") 
        |> Seq.map directory
        |> Seq.iter (fun projectPath ->
            let projectName = projectPath |> filename
            !! (projectPath @@ (projectName |> sprintf "%s.*proj")) 
            |> Seq.iter (fun projectFile ->
                let referenceFile = projectPath @@ "obj" @@ (filename projectFile) + ".references"
                let nuspecFile = projectPath @@ "obj" @@ projectName + "." + packageVersion + ".nuspec"

                // recreate 'projectName.csproj.refereces' even if it is empty
                if referenceFile |> fileMissing then
                    referenceFile 
                    |> tap (tracefn "Creating: %s") 
                    |> tap (directory >> CreateDir) 
                    |> File.touch

                // delete 'old' .nuspec (they are messing with pack)
                !! (projectPath @@ "obj" @@ "*.nuspec") -- nuspecFile |> DeleteFiles
            )
        )

    let releaseNupkg () = 
        let projects =
            listProj ()
            |> Seq.filter (isTemplateProj >> not)
            |> Seq.filter (isTestProj >> not)
            |> Seq.map directory
            |> Seq.distinct
            |> Seq.toArray

        projects |> Seq.iter (fun project ->
            projects |> Seq.iter fixPackReferences
            pack packageVersion project
        )

