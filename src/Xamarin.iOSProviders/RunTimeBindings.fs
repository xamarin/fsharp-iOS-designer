namespace Xamarin.UIProviders.DesignTime
open System
open System.IO
open System.Reflection

module RunTime =
    
    type BindingType =
        IOS | TVOS | MACOS
        
    type RunTimeBinding = {Type:BindingType; BaseFolder:string; Assembly:Assembly}
        
    let (++) a b = Path.Combine(a,b)
    //TODO figure out how to find this properly, must be a env var or something.
    let frameworkFolder    = "/Library/Frameworks"
    let ios_tvosBaseFolder = frameworkFolder ++ "Xamarin.iOS.framework/Versions/Current/lib/mono"
    let macBaseFolder      = frameworkFolder ++ "Xamarin.Mac.framework/Versions/Current/lib/mono"
    let tvosDll = "Xamarin.TVOS.dll"
    let iosDll = "Xamarin.iOS.dll"
    let macDll = "Xamarin.Mac.dll"
        
    let getRuntimeType (runtimename:string) =
        match Path.GetFileName(runtimename) with
        | "Xamarin.UIProvider.TVOSRuntime.dll" -> TVOS
        | "Xamarin.UIProvider.iOSRuntime.dll"  -> IOS
        | "Xamarin.UIProvider.OSXRuntime.dll"  -> MACOS
        | _ -> failwithf "Uknown runtime %s" runtimename
        
    let getFolderAndAssembly (rtb:BindingType) =
        let folder, assemblyName =
            match rtb with
            | TVOS  -> ios_tvosBaseFolder ++ "Xamarin.TVOS", tvosDll
            | IOS   -> ios_tvosBaseFolder ++ "Xamarin.iOS" , iosDll
            | MACOS -> macBaseFolder ++ "Xamarin.Mac" , macDll
        folder, Assembly.LoadFrom(folder ++ assemblyName)
        
    let getRuntimeBinding runtimeName =
        let t = getRuntimeType runtimeName
        let f,a = getFolderAndAssembly t
        {Type=t;BaseFolder=f;Assembly=a}