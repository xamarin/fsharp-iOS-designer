namespace Xamarin.UIProviders.DesignTime
open System
open System.Xml.Linq
open MonoTouch.Design

module IOS =
    let vcOutletMap (vc:ProxiedViewController) (o:Outlet) = 
        maybe {
            let! destination = vc.FindById(o.Destination) |> Option.ofObj
            return {Property=o.Property; ElementName= destination.Element.Name.LocalName }}

    let vOutletMap (v:ProxiedView) (o:Outlet) = 
        maybe {
            let! destination = v.FindById(o.Destination) |> Option.ofObj
            return {Property=o.Property; ElementName= destination.Element.Name.LocalName }}

    let actionMap (vc:ProxiedViewController) (ac:ActionConnection) =
        maybe {
            let! destination = vc.FindById(ac.Destination) |> Option.ofObj
            return {Selector=ac.Selector;ElementName= destination.Element.Name.LocalName}}

    let rec createView (view:ProxiedView) =
        view
        |> Option.ofObj
        |> Option.bind (fun v ->
            if not (String.IsNullOrWhiteSpace(view.CustomClass))
            then
                { View.CustomClass = view.CustomClass
                  XmlType = view.Element.Name.LocalName
                  Outlets = view.Outlets |> Seq.choose (vOutletMap view) |> Seq.toList
                  SubViews = view.Subviews
                             |> Seq.map createView
                             |> Seq.choose id
                             |> Seq.toList } |> Some
            else 
                None)

    let createScene (scene : MonoTouch.Design.Scene) =
        let vc = scene.ViewController

        let actions = maybe {
            let! view = vc.View |> Option.ofObj
            let! subviews = view.Subviews |> Option.ofObj
            return subviews
                   |> Seq.collect (fun sv -> sv.Actions)
                   |> Seq.distinct
                   |> Seq.choose (actionMap vc)
                   |> Seq.toList } |> Option.defaultValue List.empty

        let view = createView vc.View

        let newVc = {ViewController.XmlType = vc.Element.Name.LocalName
                     CustomClass = vc.CustomClass
                     Outlets = vc.Outlets |> Seq.choose (vcOutletMap vc) |> Seq.toList
                     Actions = actions
                     View = view}

        let scene = {ViewController = newVc }
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
                           //if String.IsNullOrWhiteSpace scene.CustomClass
                           //then None
                           Some (createScene scene))