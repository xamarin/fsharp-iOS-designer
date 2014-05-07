namespace Xamarin.iOSProviders
open System
open System.IO
open System.Net
open ProviderImplementation
open ProviderImplementation.ProvidedTypes

module internal Test =

    type test() =
        member x.Help() =()

    let (++) a b = Path.Combine(a, b)
    let outputFolder = __SOURCE_DIRECTORY__  ++ "bin" ++ "Debug"
    let assemblyName = "iOSDesignerTypeProvider.exe"
    let runtimeAssembly = outputFolder ++ assemblyName

    printfn "%s\n%s" outputFolder runtimeAssembly
    let testXib = "/Users/dave/Projects/IOSTypeProviderTests/cstest/MainStoryboard.storyboard"
    let config = [| box testXib|]

    let generated = Debug.generate outputFolder runtimeAssembly (fun cfg -> new iOSDesignerProvider(cfg) :> TypeProviderForNamespaces) config

    //These lines build the assembly on disk
    //let assemblyGen = ProvidedAssembly("test.dll")
    //assemblyGen.AddTypes([generated])

    let output = Debug.prettyPrint false false 10 100 generated
    printfn "%s" output
    Console.ReadKey() |> ignore    