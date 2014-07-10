namespace Xamarin.iOSProviders

open System
open System.IO
open System.Xml.Linq
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open MonoTouch.Design 

[<TypeProvider>] 
type iOSDesignerProvider(config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    //TODO figure out how to find this properly, must be a env var or something.
    let assemblybase = "/Developer/MonoTouch/usr/lib/mono/2.1/"

    do this.RegisterProbingFolder assemblybase    

    let ns = "Xamarin.iOSProviders"
    let asm = Assembly.GetExecutingAssembly()
    let rootType = ProvidedTypeDefinition(asm, ns, "UIProvider", None, HideObjectMethods = true, IsErased = false)
    let watchedFile = ref  Unchecked.defaultof<_>
    let buildTypes typeName (parameterValues: obj []) =

        let designerFile =
            let filename = parameterValues.[0] :?> string
            if Path.IsPathRooted filename then Uri(filename)
            else Uri (Path.Combine (config.ResolutionFolder, filename))

        let register = parameterValues.[1] :?> bool
        let isAbstract = parameterValues.[2] :?> bool
        let addUnitCtor = parameterValues.[3] :?> bool

        let stream, watcherDisposer = IO.openWithWatcher designerFile this.Invalidate
        watchedFile := watcherDisposer
        let xdoc = XDocument.Load(stream)
        stream.Dispose()

        //generate storyboard container
        let container = ProvidedTypeDefinition(asm, ns, typeName, Some(typeof<obj>), IsErased=false)

        let scenes = 
            match Parser.Instance.Parse(xdoc.Root) with
            | :? Storyboard as sb ->
                sb.Scenes
            //TODO: Support Xibs!
            | :? Xib as xib -> failwith "Xib files are currently not supported"
            | _ -> failwith "Could not parse file, no storyboard or xib types were found"

        let generated = 
            scenes 
            |> Seq.map (fun scene -> scene.ViewController) 
            |> Seq.filter (fun vc -> not (String.IsNullOrWhiteSpace vc.CustomClass) )
            |> Seq.map (fun vc -> vc, TypeBuilder.buildController designerFile vc isAbstract addUnitCtor register config)
        
        //Add the types to the container
        for (vc, pt) in generated do
            container.AddMember pt
            if vc.IsInitialViewController then
                container.AddMember (TypeBuilder.buildInitialController pt designerFile)

        //pump types into the correct assembly
        let assembly = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
        assembly.AddTypes [container]
        container

    do  rootType.DefineStaticParameters([ProvidedStaticParameter ("DesignerFile", typeof<string>)
                                         ProvidedStaticParameter ("IsRegistered", typeof<bool>, false)
                                         ProvidedStaticParameter ("IsAbstract",   typeof<bool>, true)
                                         ProvidedStaticParameter ("AddUnitCtor",  typeof<bool>, false)], buildTypes)
        
        this.AddNamespace(ns, [rootType])
        this.Disposing.Add (fun _ -> if !watchedFile <> null then (!watchedFile).Dispose())

[<assembly:TypeProviderAssembly()>] 
do()