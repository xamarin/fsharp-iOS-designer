namespace Xamarin.UIProviders.DesignTime

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Xml.Linq
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open ProvidedTypes

[<TypeProvider>] 
type iOSDesignerProvider(config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    let runtimeBinding = RunTime.getRuntimeBinding config.RuntimeAssembly
    do this.RegisterProbingFolder runtimeBinding.BaseFolder    

    let ns = "Xamarin"
    let asm = Assembly.GetExecutingAssembly()
    let rootType = ProvidedTypeDefinition(asm, ns, "UIProvider", None, HideObjectMethods=true, IsErased=false)
    let watchedFiles = ResizeArray()
    let buildTypes typeName (parameterValues: obj []) =

        let allStoryBoards = Directory.EnumerateFiles(config.ResolutionFolder, "*.storyboard", SearchOption.AllDirectories) 
        let register = parameterValues.[0] :?> bool
        let isAbstract = parameterValues.[1] :?> bool
        let addUnitCtor = parameterValues.[2] :?> bool

        let scenes = 
            seq { for storyboardFile in allStoryBoards do

                    let xdoc, watcherDisposer =
                        use sss = new StreamReader(storyboardFile, true)
                        let xdoc = XDocument.Load(sss)
                        xdoc, File.watch storyboardFile this.Invalidate
                    
                    watchedFiles.Add watcherDisposer

                    let scenes = 
                        match runtimeBinding.Type with 
                        | RunTime.MACOS -> Mac.scenesFromXDoc xdoc
                        | RunTime.IOS 
                        | RunTime.TVOS -> IOS.scenesFromXDoc xdoc
                    yield! scenes }

        let groupedViewControllers =
            query {for scene in scenes do
                       groupValBy scene.ViewController scene.ViewController.CustomClass }

        let groupedViews =
            let rec proc (v:View) =
                [ yield v
                  yield! v.SubViews |> Seq.collect proc ]

            [ for scene in scenes do
                  match scene.ViewController.View with
                  | Some view -> yield! proc view
                  | None -> () ]
            |> List.groupBy (fun v -> v.CustomClass )

        //generate storyboard container
        let container = ProvidedTypeDefinition(asm, ns, typeName, Some(typeof<obj>), IsErased=false)

        let generatedTypes =
            [ for sc in groupedViewControllers do
                  let viewControllers = sc.AsEnumerable()
                  let settings = {IsAbstract = isAbstract
                                  AddUnitCtor = addUnitCtor
                                  Register = register
                                  BindingType = runtimeBinding
                                  GenerationType = Generated.ViewControllers viewControllers }
                  yield TypeBuilder.buildController settings config

              for views in groupedViews do
                  let _customClass, views = views
                  let settings = {IsAbstract = false
                                  AddUnitCtor = false
                                  Register = true
                                  BindingType = runtimeBinding
                                  GenerationType = Generated.Views views }
                  yield TypeBuilder.buildController settings config ]
        
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