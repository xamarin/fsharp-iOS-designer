#I "/Developer/MonoTouch/usr/lib/mono/2.1"
#r "/Applications/Xamarin Studio.app/Contents/Resources/lib/monodevelop/AddIns/MonoDevelop.IPhone/MonoTouch.Design.dll"
#r "/Applications/Xamarin Studio.app/Contents/Resources/lib/monodevelop/AddIns/MonoDevelop.IPhone/MonoTouch.Design.Client.dll"
#r "packages/ExtCore.0.8.45/lib/net45/ExtCore.dll" 
#r "/Library/Frameworks/Xamarin.iOS.framework/Versions/Current/lib/mono/Xamarin.iOS/Xamarin.iOS.dll"
#r "System.xml.Linq"
#load "ProvidedTypes.fs"
#load "ProvidedTypesHelpers.fs"
#load "IO.fs"
#load "Debug.fs"
#load "Designtime.fs"
#load "iOSDesignerProvider.fs"
open System
open System.IO
open System.Net
open System.Linq
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open Xamarin.iOSProviders

let (/) a b = Path.Combine(a, b)
let outputFolder = __SOURCE_DIRECTORY__ / "bin" / "Debug"
let workingFolder = __SOURCE_DIRECTORY__ / "../../samples/StoryBoards/"
let assemblyName = "iOSDesignerTypeProvider.exe"
let runtimeAssembly = outputFolder / assemblyName
let config = [|box false;box true;box true |]
let generated = Debug.generate workingFolder runtimeAssembly (fun cfg -> new iOSDesignerProvider(cfg)) config
let output = Debug.prettyPrint false false 10 100 generated
printfn "%s" output

//These lines build the assembly on disk
//let assemblyGen = ProvidedAssembly("test.dll")
//assemblyGen.AddTypes([generated])