namespace Xamarin.iOSProviders

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Xml.Linq
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open MonoTouch.Design 

[<TypeProvider>] 
type iOSDesignerProvider(config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    //TODO figure out how to find this properly, must be a env var or something.
    let assemblybase = "/Developer/MonoTouch/usr/lib/mono/Xamarin.iOS/"
    do this.RegisterProbingFolder assemblybase    

    let ns = "Xamarin.iOSProviders"
    let asm = Assembly.GetExecutingAssembly()
    let rootType = ProvidedTypeDefinition(asm, ns, "UIProvider", None, HideObjectMethods = true, IsErased = false)
    let watchedFiles = ResizeArray()
    let buildTypes typeName (parameterValues: obj []) =

        let allStoryBoards = Directory.EnumerateFiles ( config.ResolutionFolder, "*.storyboard", SearchOption.AllDirectories) 

        let register = parameterValues.[0] :?> bool
        let isAbstract = parameterValues.[1] :?> bool
        let addUnitCtor = parameterValues.[2] :?> bool

        let a = [""]
        a |> Seq.iter (printfn "%s")


        let scenes = 
            seq { for storyboard in allStoryBoards do

                    let stream, watcherDisposer = File.openWithWatcher storyboard this.Invalidate
                    watchedFiles.Add watcherDisposer
                    let xdoc = XDocument.Load(stream)
                    stream.Close ()
                    let scenes = 
                        match Parser.Instance.Parse(xdoc.Root, DeviceFamily.Undefined) with
                        | :? Storyboard as sb -> sb.Scenes
                        | :? Xib as _xib -> failwith "Xib files are currently not supported"
                        | _ -> failwith "Could not parse file, no supported files were found"
                    yield! scenes }

        let groupedViewControllers = 
            query {for scene in scenes do
                       where (not (String.IsNullOrWhiteSpace scene.ViewController.CustomClass))
                       groupValBy scene.ViewController scene.ViewController.CustomClass}


        //generate storyboard container
        let container = ProvidedTypeDefinition(asm, ns, typeName, Some(typeof<obj>), IsErased=false)

        let generatedTypes =
            [ for sc in groupedViewControllers do
                  let vcs = sc.AsEnumerable()
//                  if not (String.IsNullOrWhiteSpace sc.ViewController.CustomClass) then
                  yield TypeBuilder.buildController vcs isAbstract addUnitCtor register config ]
        
        //Add the types to the container
        container.AddMembers generatedTypes

//        for pt in generatedTypes do
//            container.AddMember pt
//            if vc.IsInitialViewController then
//                container.AddMember (TypeBuilder.buildInitialController pt designerFile)

        //pump types into the correct assembly
        let assembly = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
        assembly.AddTypes [container]
        container

    do  rootType.DefineStaticParameters([ProvidedStaticParameter ("AddRegisteration",      typeof<bool>, false)
                                         ProvidedStaticParameter ("AbstractController",    typeof<bool>, true)
                                         ProvidedStaticParameter ("AddDefaultConstructor", typeof<bool>, false)], buildTypes)
        
        this.AddNamespace(ns, [rootType])
        this.Disposing.Add (fun _ -> for disposer in watchedFiles do disposer.Dispose ())

[<assembly:TypeProviderAssembly()>] 
do()