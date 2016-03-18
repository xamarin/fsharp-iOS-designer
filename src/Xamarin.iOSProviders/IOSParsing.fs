namespace Xamarin.UIProviders.DesignTime
open System
open System.IO
open System.Xml
open System.Xml.Linq
open System.Linq
open ExtCore
open ExtCore.Control
open MonoTouch.Design

module IOS =
    let scenesFromXDoc (xdoc:XDocument) =
        let scenes =
            match Parser.Instance.Parse(xdoc.Root, DeviceFamily.Undefined) with
            | :? Storyboard as sb -> sb.Scenes
            | :? Xib as _xib -> failwith "Xib files are currently not supported"
            | _ -> failwith "Could not parse file, no supported files were found"
        scenes
        |> Seq.map (fun scene ->
                        let vc = scene.ViewController
                        let outlets = vc.Outlets
                        
                        let newOutlets =
                            outlets
                            |> Seq.map (fun o -> {Property=o.Property; ElementName=o.Element.Name.LocalName } )
                            |> Seq.toList
                            
                        let actions = [
                            match vc.View with
                            | null -> ()
                            | view ->
                                match view.Subviews with
                                | null -> ()
                                | subviews ->
                                    yield! subviews
                                           |> Seq.collect (fun sv -> sv.Actions)
                                           |> Seq.distinct
                                           |> Seq.map (fun ac -> {Selector=ac.Selector;ElementName=ac.Element.Name.LocalName} ) ]
                        
                        let newVc = {XmlType=vc.Element.Name.LocalName; CustomClass=vc.CustomClass; Outlets = newOutlets; Actions = actions}
                        let scene = {ViewController=newVc}
                        scene)