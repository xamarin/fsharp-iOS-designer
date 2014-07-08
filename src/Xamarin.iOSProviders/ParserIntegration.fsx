#load "ProvidedTypes.fs"
#load "TypeSystem.fs"
#I "bin/Debug"
#r "monotouch.dll"
#r "System.xml"
#r "System.Xml.Linq"
#r "FSharp.Compatibility.OCaml"
#r "MonoTouch.Design"

open System
open System.IO
open System.Reflection
open System.Linq
open System.Xml.Linq
open System.Collections.Generic
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations   
open MonoTouch.Design   
open MonoTouch.Foundation
open MonoTouch.UIKit
open ProviderImplementation.ProvidedTypes

let storyboardName = "/Users/dave/code/xamarin/fsharp-iOS-designer/src/ReferenceApps/CS_ViewController/garbage.storyboard"
let stream = File.OpenRead (storyboardName)
let xdoc = XDocument.Load(stream)
let parsed = Parser.Instance.Parse(xdoc.Root)

let storyboard = 
    match parsed with
    | :? Storyboard as sb ->
        sb
    //| :? Xib as xib ->
        //TODO
    | _ -> failwith "broken"

let sc = storyboard.Scenes.[0]
let vc = sc.ViewController
let outlet = vc.Outlets.[0]

let iuthing = storyboard.FindById(outlet.Destination) :?> ProxiedUiKitObject
iuthing.DisplayClassName
outlet

