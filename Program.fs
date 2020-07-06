open Argu
open System.IO
open System

type Project = FSharp.Data.XmlProvider<"sample.vcxproj.filters">

type Arguments =
    | [<Mandatory; AltCommandLineAttribute("-d")>] Add of paths: string list
    | [<Mandatory; Unique>] To of project: string
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Add _ -> "Add a directory."
            | To _ -> "VC++ Project file (project.vcxproj.filters)"

let parser = ArgumentParser.Create<Arguments>()

[<CustomEquality; NoComparison>]
type Directives =
    | IncludeItem of Project.ClInclude
    | SourceItem of Project.ClCompile
    | FilterItem of Project.Filter
    | IncludeFile of string
    | SourceFile of string
    | Filter of string
    member this.Path =
        match this with
        | IncludeItem i -> i.Include
        | SourceItem i -> i.Include
        | FilterItem i -> i.Include
        | IncludeFile f | SourceFile f | Filter f -> f
        
    override this.Equals(b) =
        match b with
        | :? Directives as b -> b.Path = this.Path
        | _ -> false
    override this.GetHashCode() = this.Path.GetHashCode()

[<EntryPoint>]
let main argv =
    try        
        let args = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
        let dirs = args.GetResult Add
        let file = args.GetResult To
        let parent path = Path.GetDirectoryName (path: string)
        let parents path = Seq.unfold (function "" -> None | d -> Some (d, parent d)) path
        let cwdpath path = Path.GetFullPath(path, Environment.CurrentDirectory)        

        let root =
            file |> cwdpath |> parent

        printfn "Path relative to %s" root
        
        let isFilter = (Path.GetExtension file) = ".filters"        

        let sourceExtensions = [| ".c"; ".cpp"; ".cc" |]
        let headerExtensions = [| ".h"; ".hpp"; ".tpp" |]

        let allFiles =
            seq {
                for dir in dirs do
                    let dir = cwdpath dir
                    for file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories) do
                        yield Path.GetRelativePath(root, file)
            }
            |> Seq.cache
        
        let project = Project.Load (Path.Combine(root, file))
        let groups = project.ItemGroups
        let save (file : string) =    
            // Reparse the XML, otherwise indented formatting doesn't happen
            let doc = Xml.Linq.XDocument.Parse (project.XElement.ToString())
            doc.Save file

        let filesWith map extensions =
            allFiles 
            |> Seq.filter (fun file -> extensions |> Array.contains (Path.GetExtension file))
            |> Seq.map map

        let ofType map typeSel =
            groups
            |> Seq.collect typeSel
            |> Seq.map map

        let existingSources = ofType SourceItem (fun g -> g.ClCompiles)
        let existingIncludes = ofType IncludeItem (fun g -> g.ClIncludes)
        let existingFilters = ofType FilterItem (fun g -> g.Filters)

        let actualIncludes = filesWith IncludeFile headerExtensions
        let actualSources = filesWith SourceFile sourceExtensions 
        let actualFilters = 
            Seq.append actualIncludes actualSources 
            |> Seq.map (fun d -> parent d.Path)
            |> Seq.collect parents
            |> Seq.distinct
            |> Seq.map Filter
        
        let diff a b = 
            let a, b = Seq.cache a, Seq.cache b
            a |> Seq.except b, b |> Seq.except a

        let newIncludes, pruneIncludes = diff actualIncludes existingIncludes
        let newSources, pruneSources  = diff actualSources existingSources 
        let newFilters, pruneFilters  = diff actualFilters existingFilters

        // prune filters
        Seq.concat [pruneIncludes; pruneSources; pruneFilters]
        |> Seq.iter (fun item ->
            let node = 
                match item with
                    | SourceItem node -> node.XElement
                    | FilterItem node -> node.XElement
                    | IncludeItem node -> node.XElement
                    | other -> failwithf "Invalid %A" other
            node.Remove()
        )

        let fileFilter (file : string) = 
            file, if isFilter then Some (parent file) else None
        
        let mapNew map items = 
            items |> Seq.map map |> Seq.toArray

        let includes = 
            newIncludes 
            |> mapNew (function IncludeFile file -> fileFilter file |> Project.ClInclude | _ -> failwith "Invalid include")             

        let sources =
            newSources
            |> mapNew (function SourceFile file -> fileFilter file |> Project.ClCompile | _ -> failwith "Invalid source") 
        
        let filters =
            if isFilter then newFilters else Seq.empty
            |> mapNew (function Filter path -> Project.Filter(path, Guid.NewGuid()) | _ -> failwith "Invalid filter")

        let files = new Project.ItemGroup(sources, includes, [||])
        let folders = new Project.ItemGroup([||], [||], filters)

        let add (group: Project.ItemGroup) =
            if not group.XElement.IsEmpty then
                project.XElement.Add group.XElement
        
        // add new items        
        add files
        add folders
        
        // replace original file
        save file

        0 // exit code 0
    with e -> 
        printfn "%s" e.Message
        -1 // return fail code    
