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

    let ns = "Xamarin.TypeProviders"
    let asm = Assembly.GetExecutingAssembly()
    let rootType = ProvidedTypeDefinition(asm, ns, "iOS", None, HideObjectMethods = true, IsErased = false)
    let watchedFiles = ResizeArray()
    let assembly = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
    let buildTypes typeName (parameterValues: obj []) =

        let allStoryBoards = Directory.EnumerateFiles ( config.ResolutionFolder, "*.storyboard", SearchOption.AllDirectories) 

        let register = parameterValues.[0] :?> bool
        let isAbstract = parameterValues.[1] :?> bool
        let addUnitCtor = parameterValues.[2] :?> bool
        let customClass = unbox parameterValues.[3] // :?> string

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
                       where (not (String.IsNullOrWhiteSpace scene.ViewController.CustomClass) && scene.ViewController.CustomClass=customClass)
                       groupValBy scene.ViewController scene.ViewController.CustomClass}

        let generatedTypes =
            [ for sc in groupedViewControllers do
                  let vcs = sc.AsEnumerable()
                  yield TypeBuilder.buildController vcs isAbstract addUnitCtor register config typeName ] |> List.head


        rootType.AddMember generatedTypes
        generatedTypes

    do  
        assembly.AddTypes [rootType]
        this.AddNamespace(ns, [rootType])
        rootType.DefineStaticParameters([ProvidedStaticParameter.Create ("AddRegisteration", false)
                                         ProvidedStaticParameter.Create ("AbstractController", true)
                                         ProvidedStaticParameter.Create ("AddDefaultConstructor", false)
                                         ProvidedStaticParameter.Create<string>("CustomClass")], buildTypes)
        
        this.Disposing.Add (fun _ -> for disposer in watchedFiles do disposer.Dispose ())

[<assembly:TypeProviderAssembly()>] 
do()