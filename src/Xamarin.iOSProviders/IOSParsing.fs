namespace Xamarin.UIProviders.DesignTime
open System
open System.Xml.Linq
open ExtCore
open ExtCore.Control
open MonoTouch.Design

module IOS =
    let outletMap (vc:ProxiedViewController) (o:Outlet) = 
        maybe {
            let! destination = vc.FindById(o.Destination) |> Option.ofObj
            return {Property=o.Property; ElementName= destination.Element.Name.LocalName }}

    let actionMap (vc:ProxiedViewController) (ac:ActionConnection) =
        maybe {
            let! destination = vc.FindById(ac.Destination) |> Option.ofObj
            return {Selector=ac.Selector;ElementName= destination.Element.Name.LocalName}}

    let createScene (scene : MonoTouch.Design.Scene) =
        let vc = scene.ViewController
        let outlets = vc.Outlets
        
        let newOutlets =
            outlets
            |> Seq.choose (outletMap vc)
            |> Seq.toList

        let actions = maybe {
            let! view = vc.View |> Option.ofObj
            let! subviews = view.Subviews |> Option.ofObj
            return subviews
                   |> Seq.collect (fun sv -> sv.Actions)
                   |> Seq.distinct
                   |> Seq.choose (actionMap vc)
                   |> Seq.toList } |> Option.fill List.empty
        
        let newVc = {ViewController.XmlType = vc.Element.Name.LocalName
                     CustomClass = vc.CustomClass
                     Outlets = newOutlets
                     Actions = actions}

        let view = Unchecked.defaultof<_>

        let scene = {ViewController = newVc
                     View = view }
        scene

    let scenesFromXDoc (xdoc:XDocument) =
        let idProvider = MonoTouch.Design.DefaultIdProvider()
        let context = ModelObjectContext.Create(idProvider, Version(8,3,2), DeviceFamily.Undefined)
        let scenes =
            match Parser.Instance.Parse(xdoc.Root, DeviceFamily.Undefined, context) with
            | :? Storyboard as sb -> sb.Scenes
            | :? Xib as _xib -> failwith "Xib files are currently not supported"
            | _ -> failwith "Could not parse file, no supported files were found"
        scenes
        |> Seq.choose (fun scene ->
                           if String.IsNullOrWhiteSpace scene.CustomClass
                           then None
                           else Some (createScene scene))