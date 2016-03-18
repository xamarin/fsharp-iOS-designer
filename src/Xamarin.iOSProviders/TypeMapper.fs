namespace Xamarin.UIProviders.DesignTime
open System
open System.Reflection
open System.IO
open System.Xml
open System.Xml.Linq

module internal MacMappings =
    let rawmap =
        dict [
            // The following lists are grabbed from Xcode4's widget pad and put here in the same order that they appear there.
            // Controllers & Objects
            ( "viewController",                 "NSViewController" )
            ( "windowController",               "NSWindowController" )
            ( "pageController",                 "NSPageController" )
            ( "verticalSplitViewController",    "NSSplitViewController" )
            ( "horizontalSplitViewController",  "NSSplitViewController" )
            ( "tabViewController",              "NSTabViewController" )
            ( "objectController",               "NSObjectController" )
            ( "arrayController",                "NSArrayController" )
            ( "dictionaryController",           "NSDictionaryController" )
            ( "treeController",                 "NSTreeController" )
            ( "userDefaultController",          "NSUserDefaultController" )
            ( "textFinder",                     "NSTextFinder" )
    
            // Controls
            ( "button",                         "NSButton" )
            ( "popUpButton",                    "NSPopUpButton" )
            ( "label",                          "NSLabel" )
            ( "textField",                      "NSTextField" )
            ( "searchField",                    "NSSearchField" )
            ( "comboBox",                       "NSComboBox" )
            ( "segmentedControl",               "NSSegmentedControl" )
            ( "slider",                         "NSSlider" )
            ( "levelIndicator",                 "NSLevelIndicator" )
            ( "progressIndicator",              "NSProgressIndicator" )
            ( "stepper",                        "NSStepper" )
            ( "colorWell",                      "NSColorWell" )
            ( "radioGroup",                     "NSMatrix" )
    
            // Data Views
            ( "datePicker",                     "NSDatePicker" )
            ( "tableView",                      "NSTableView" )
            ( "tableViewCell",                  "NSTableViewCell" )
            ( "collectionView",                 "NSCollectionView" )
            ( "collectionViewCell",             "NSCollectionViewCell" )
            ( "collectionViewItem",             "NSCollectionViewItem" )
            ( "imageView",                      "NSImageView" )
            ( "textView",                       "NSTextView" )
            ( "webView",                        "WebView" )
            ( "mapView",                        "MKMapView" )
            ( "view",                           "NSView" )
            ( "containerView",                  "NSView" )
            ( "customView",                     "NSView" )
            ( "scrollView",                     "NSScrollView" )
            ( "form",                           "NSForm" )
    
            // Gesture Recognizers
            ( "clickGestureRecognizer",         "NSClickGestureRecognizer" )
            ( "magnificationGestureRecognizer", "NSMagnificationGestureRecognizer" )
            ( "panGestureRecognizer",           "NSPanGestureRecognizer" )
            ( "pressGestureRecognizer",         "NSPressGestureRecognizer" )
            ( "rotationGestureRecognizer",      "NSRotationGestureRecognizer" ) ]

module TypeMapper =
    open RunTime
    let getTypeMap (bindingType:RunTime.RunTimeBinding) (xmlType:string) =
        //TODO: Expand this to also search in user assemblies
        let objCType = 
            match bindingType.Type with
            | IOS
            | TVOS -> MonoTouch.Design.TypeSystem.XmlTypeToObjCType(xmlType, false)
            | MACOS ->
                match MacMappings.rawmap.TryGetValue xmlType with
                | true, v -> v
                | false, _ -> ""
            
        let hasRegisterAttribute (typ:Type) =
          query {for ca in typ.CustomAttributes do
                   exists (ca.AttributeType = bindingType.Assembly.GetType("Foundation.RegisterAttribute", true)) }

        let hasMatchingAttributeName (typ:Type) proxyClassName = 
          query {for ca in typ.CustomAttributes do
                  exists (match ca.ConstructorArguments |> Seq.map (fun ca -> ca.Value) |> Seq.toList with   
                          | [:? string as name] -> name = proxyClassName
                          | [:? string as name; :? bool as _isWrapper] -> name = proxyClassName
                          | _ -> false) }

//debug
        //let allTypes =
        //  query { for typ in bindingType.Assembly.ExportedTypes do
        //            where ((hasRegisterAttribute typ))
        //            select typ } |> Seq.toArray


        //let typeAndCa =
        //  query { for typ in allTypes do
        //            let cs = typ.CustomAttributes |> Seq.find (fun ca -> ca.AttributeType = bindingType.Assembly.GetType("Foundation.RegisterAttribute", true) )
        //            select (typ.Name, cs) } |> Seq.toArray

        //let justRegister = 
        //  typeAndCa |> Array.map (fun (a,b) -> match b.ConstructorArguments |> Seq.map (fun ca -> ca.Value) |> Seq.toList  with
        //                                       | [:? string as name] -> name
        //                                       | [:? string as name; :? bool as _isWrapper] -> name
        //                                       | _-> "invalid") |> Array.sort

        //let justType =
        //  typeAndCa |> Array.map (fun (a, b) -> a) |> Array.sort

        //------------------------------
        let matches =
          query { for typ in bindingType.Assembly.ExportedTypes do
                    where ((hasRegisterAttribute typ) && (hasMatchingAttributeName typ objCType))
                    select typ
                    exactlyOne }
        matches
        

