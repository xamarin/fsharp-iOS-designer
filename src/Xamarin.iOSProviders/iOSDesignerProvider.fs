namespace Xamarin.UIProviders.DesignTime

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Xml.Linq
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open MonoTouch.Design
open ProvidedTypes

[<TypeProvider>] 
type iOSDesignerProvider(config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    //TODO figure out how to find this properly, must be a env var or something.
    let baseFolder ="/Library/Frameworks/Xamarin.iOS.framework/Versions/Current/lib/mono"
    
    let assemblybase, assembly =
        match Path.GetFileName config.RuntimeAssembly with
        | a when a = "Xamarin.UIProvider.TVOSRuntime.dll" ->
            let folder = Path.Combine (baseFolder, "Xamarin.TVOS")
            let assembly = Path.Combine(folder, "Xamarin.TVOS.dll")
            folder, Assembly.LoadFrom assembly
        | a when a = "Xamarin.UIProvider.iOSRuntime.dll" ->
            let folder = Path.Combine (baseFolder, "Xamarin.iOS")
            let assembly = Path.Combine(folder, "Xamarin.iOS.dll")
            folder, Assembly.LoadFrom assembly
        | other -> failwithf  "Uknown runtime provider assembly: %s" other
        
    do this.RegisterProbingFolder assemblybase    

    let ns = "Xamarin"
    let asm = Assembly.GetExecutingAssembly()
    let rootType = ProvidedTypeDefinition(asm, ns, "UIProvider", None, HideObjectMethods=true, IsErased=false)
    let watchedFiles = ResizeArray()
    let buildTypes typeName (parameterValues: obj []) =

        let allStoryBoards = Directory.EnumerateFiles ( config.ResolutionFolder, "*.storyboard", SearchOption.AllDirectories) 
        let register = parameterValues.[0] :?> bool
        let isAbstract = parameterValues.[1] :?> bool
        let addUnitCtor = parameterValues.[2] :?> bool

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
                  yield TypeBuilder.buildController assembly vcs isAbstract addUnitCtor register config ]
        
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

    do  rootType.DefineStaticParameters(
         [mkProvidedStaticParameter "AddRegisteration" false
          mkProvidedStaticParameter "AbstractController" true
          mkProvidedStaticParameter "AddDefaultConstructor" false], buildTypes)
        
        this.AddNamespace(ns, [rootType])
        this.Disposing.Add (fun _ -> for disposer in watchedFiles do disposer.Dispose ())

//[<assembly:TypeProviderAssembly()>] 
//do()