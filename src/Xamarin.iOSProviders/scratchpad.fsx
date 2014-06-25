#r "/Developer/MonoTouch/usr/lib/mono/2.1/monotouch.dll"
open MonoTouch.UIKit
open System
open System.Reflection

module TypeExt =
    type Type with
        member x.GetVirtualMethods() = 
            x.GetMethods (BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly) 
            |> Array.filter (fun m -> m.IsVirtual)
    
let vc = typeof<UIViewController>
let meths = 
    vc.GetMethods (BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly) 
    |> Array.filter (fun m -> m.IsVirtual && (*not*) (m.ReturnType = typeof<Void>) )
meths.Length

open TypeExt
typeof<UIViewController>.GetVirtualMethods() |> Array.map (fun m-> m.Name + " : " + m.ReturnType.FullName )