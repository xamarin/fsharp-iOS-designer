﻿namespace Xamarin.UIProviders.DesignTime
open System
open System.IO
open System.Xml
open System.Xml.Linq
open System.Linq
open ExtCore
open ExtCore.Control

[<AutoOpen>]
module XmlHelpers =
    let xn = XName.op_Implicit

    let tryGetAttribute name (element:XElement)  =
        element.Attributes()
        |> Seq.tryFind(fun a -> a.Name = xn name)

module Mac =
    let elementsById (element:XElement) =
        element
        |> tryGetAttribute "id"
        |> Option.map (fun attrib -> attrib.Value, element)
                                         
    let createIdLookup (xdoc:XDocument) =
        let ids =
            xdoc.Descendants()
            |> Seq.choose elementsById
            |> dict
        fun (id:string) -> match ids.TryGetValue id with
                           | true, v -> Some v
                           | false, _ -> None
                           
    let outletMapping tryLookup outlet = maybe {
        let! prop = outlet |> tryGetAttribute "property"
        let! dest = outlet |> tryGetAttribute "destination"
        let! (destElement : XElement) = tryLookup dest.Value
        let elementName = destElement.Name.LocalName
        
        return {Property    = prop.Value
                ElementName = elementName } }
                        
    let viewControllerMapping tryLookup (vc:XElement) = maybe {
        let! customClass = vc |> tryGetAttribute "customClass"
        let outlets =
            vc.Elements(xn "connections")
            |> Seq.collect (fun conn -> conn.Elements(xn "outlet"))
            |> Seq.choose (outletMapping tryLookup)
            |> Seq.toList
        let actions = []
        return {XmlType = vc.Name.LocalName
                CustomClass = customClass.Value
                Outlets = outlets
                Actions = actions} }
    
    let scenesFromXDoc (xdoc:XDocument) =
        let tryLookup = createIdLookup xdoc
        xdoc.Descendants(xn "scene")
        |> Seq.collect (fun scene -> let vcElement = scene.Descendants(xn "viewController")
                                     vcElement
                                     |> Seq.choose (viewControllerMapping tryLookup)
                                     |> Seq.map (fun vc -> {ViewController=vc}))
                                     
    let scenesFromStoryBoardFileName (sb:string) =
        let xdoc = XDocument.Load(new StreamReader(sb, true))
        scenesFromXDoc xdoc