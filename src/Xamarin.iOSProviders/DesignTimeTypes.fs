namespace Xamarin.iOSProviders
open System
open System.Xml
open System.Xml.Linq
open System.Linq

[<AutoOpenAttribute>]
module Xml =
    let inline xn name  = XName.op_Implicit(name)

type IosAction =
    {selector:string; destination:string; eventType:string; id:string}
    static member Parse(action: XElement) =
        {selector = action.Attribute(xn "selector").Value
         destination = action.Attribute(xn "destination").Value
         eventType = action.Attribute(xn "eventType").Value
         id = action.Attribute(xn "id").Value }

type Outlet =
    {Name:string;Type:Type;Xml:string}

    static member Parse(outlet:XElement) =
        let name = outlet.Attribute(xn "property").Value
        let destination = outlet.Attribute(xn "destination").Value
        let destinationElement =
            let viewcontroller = outlet.Parent.Parent
            let view = outlet.Parent.Parent.Descendants(xn "view") |> Seq.toArray
            let subviews = view.Descendants(xn "subviews") |> Seq.toArray
            subviews.Descendants()
            |> Seq.tryFind (fun xx -> let id = xx.Attribute(xn "id")
                                      if id = null then false 
                                      else id.Value = destination)
        match destinationElement with
        | Some element -> 
            match TypeSystem.typeMap.TryFind element.Name.LocalName with
            | Some typ -> {Name=name;Type=typ;Xml= outlet.ToString()}
            | None -> failwithf "Unknown Outlet type: %s" element.Name.LocalName
        | None -> failwithf "Could not find outlet destination: %s" destination

type ViewController =
    {id:string; customClass:string; sceneMemberID:string}
    static member Parse(vc: XElement) =
        {id = vc.Attribute(xn "id").Value
         customClass = vc.Attribute(xn "customClass").Value
         sceneMemberID = vc.Attribute(xn "sceneMemberID").Value}
