namespace Xamarin.UIProviders.DesignTime

open System
open System.IO
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
//open Foundation
//open UIKit
open MonoTouch.Design
open ProvidedTypes
open ExtCore.Control
open Swensen.Unquote

module Sanitise =
    let cleanTrailing = String.trimEnd [|':'|]
    let makeFieldName (name:string) = 
        "__" +
        (name |> String.uncapitalize |> String.trimEnd [|':'|])
    
    let makePascalCase (name:string) = 
        name |> String.capitalize |> String.trimEnd [|':'|]

    let makeSelectorMethodName (name:string) = 
        (makePascalCase name) + "Selector"

module TypeBuilder =
    
    let getTypeMap (assembly:Assembly) (proxy:ProxiedUiKitObject) =
        //TODO: Expand this to also search in user assemblies
        let hasRegisterAttribute (typ:Type) =
          query {for ca in typ.CustomAttributes do
                   exists (ca.AttributeType = assembly.GetType("Foundation.RegisterAttribute", true)) }

        let hasMatchingAttributeName (typ:Type) proxyClassName = 
          query {for ca in typ.CustomAttributes do
                  exists (match ca.ConstructorArguments |> Seq.map (fun ca -> ca.Value) |> Seq.toList with   
                          | [:? string as name] -> name = proxyClassName
                          | [:? string as name; :? bool as _isWrapper] -> name = proxyClassName
                          | _ -> false) }

//debug
//        let allTypes =
//          query { for typ in monotouchAssembly.ExportedTypes do
//                    where ((hasRegisterAttribute typ))
//                    select typ } |> Seq.toArray
//

//        let typeAndCa =
//          query { for typ in allTypes do
//                    let cs = typ.CustomAttributes |> Seq.find (fun ca -> ca.AttributeType = typeof<RegisterAttribute> )
//                    select (typ.Name, cs) } |> Seq.toArray
//
//        let justRegister = 
//          typeAndCa |> Array.map (fun (a,b) -> match b.ConstructorArguments |> Seq.map (fun ca -> ca.Value) |> Seq.toList  with
//                                      toList         | [:? string as name] -> name
//                                               | [:? string as name; :? bool as _isWrapper] -> name
//                                               | _-> "invalid") |> Array.sort
//
//        let justType =
//          typeAndCa |> Array.map (fun (a, b) -> a) |> Array.sort

        //------------------------------
        let matches =
          query { for typ in assembly.ExportedTypes do
                    where ((hasRegisterAttribute typ) && (hasMatchingAttributeName typ proxy.CodeGenerationBaseClass))
                    select typ
                    exactlyOne }
        matches

    //TODO add option for ObservableSource<NSObject>, potentially unneeded as outlets exposes this with observable...
    let buildAction (assembly:Assembly) (action:ActionConnection) =
        //create a backing field fand property or the action

        let actionAttributeType = assembly.GetType("Foundation.ActionAttribute", true)
        
        let actionField, actionProperty =
            ProvidedTypes.ProvidedPropertyWithField (Sanitise.makeFieldName action.Selector,
                                                     Sanitise.makePascalCase action.Selector,
                                                     typeof<option<obj -> unit>>)
        let actionBinding =
            ProvidedMethod(methodName = Sanitise.makeSelectorMethodName action.Selector, 
                           parameters = [ProvidedParameter("sender", typeof<obj>) ], 
                           returnType = typeof<Void>, 
                           InvokeCode = fun args ->
                                            let callback = Expr.FieldGet(args.[0], actionField)
                                            let arg = args.[1]
                                            <@@ %%callback |> Option.iter (fun f -> f %%arg) @@>)
                                                    

        actionBinding.AddCustomAttribute
            (CustomAttributeDataExt.Make (actionAttributeType.GetConstructor (typeof<string>),
                                          [| CustomAttributeTypedArgument (actionAttributeType, action.Selector) |]))
        actionBinding.SetMethodAttrs MethodAttributes.Private

        [actionField :> MemberInfo
         actionProperty :> _
         actionBinding  :> _]

    let buildOutlet (assembly:Assembly) (vc:ProxiedViewController, outlet:Outlet) =
            let uiProxy = vc.Storyboard.FindById (outlet.Destination) :?> ProxiedUiKitObject
            let outletField, outletProperty =
                ProvidedTypes.ProvidedPropertyWithField (Sanitise.makeFieldName outlet.Property,
                                                         Sanitise.cleanTrailing outlet.Property,
                                                         getTypeMap assembly uiProxy)
            outletProperty.AddCustomAttribute <| CustomAttributeDataExt.Make (assembly.GetType("Foundation.OutletAttribute", true).GetUnitConstructor ())

            //Add the property and backing fields to the view controller
            outletField, outletProperty
    
    //takes an instance returns a disposal expresion
    let buildDisposalExpr instance outletField =
        let get = Expr.FieldGet (instance, outletField)
        let field = Expr.Coerce (get, typeof<obj>)
        <@@ if %%field <>  null then
               ((%%field:obj) :?> IDisposable).Dispose() @@>

    let buildReleaseOutletsExpr (instance: Expr) (outlets: _ list)=
        match outlets with
        | [single] -> buildDisposalExpr instance single
        | lots -> lots 
                  |> List.map (fun o -> buildDisposalExpr instance o) 
                  |> List.reduce (fun one two -> Expr.Sequential (one, two))

    let buildReleaseOutletsMethod fields =
        ProvidedMethod("ReleaseDesignerOutlets", [], typeof<Void>, 
                       InvokeCode = function
                                    | [instance] -> if List.isEmpty fields then Expr.emptyInvoke ()
                                                    else buildReleaseOutletsExpr instance fields
                                    | _ -> invalidOp "Too many arguments")

    let buildController (assembly:Assembly) (vcs: ProxiedViewController seq) isAbstract addUnitCtor register (config:TypeProviderConfig) =

        //get the real type of the controller proxy
        let vcsTypes = vcs |> Seq.map (getTypeMap assembly)
        let vc = Seq.head vcs
        let controllerType = Seq.head vcsTypes
        let className = if isAbstract then vc.CustomClass + "Base" else vc.CustomClass
        let customClass = vc.CustomClass

        let providedController = ProvidedTypeDefinition (className, Some controllerType, IsErased = false )
        providedController.SetAttributes (if isAbstract then TypeAttributes.Public ||| TypeAttributes.Class ||| TypeAttributes.Abstract
                                          else TypeAttributes.Public ||| TypeAttributes.Class)

        //Were relying on the fact all controller have the IntPtr and unit constructors, At least we propagate our own errors here.
        //Type lookups based on Controler type may be needed to find and add the correct ConstructorInfo

        //IntPtr ctor
        match controllerType.TryGetConstructor (typeof<IntPtr>) with
        | None -> failwithf "No IntPtr constructor found for type: %s" controllerType.Name
        | Some ctor -> let intPtrCtor =
                           ProvidedConstructor ([ProvidedParameter ("handle", typeof<IntPtr>)],
                                                InvokeCode = Expr.emptyInvoke, BaseConstructorCall = fun args -> ctor, args)
                       providedController.AddMember (intPtrCtor)

        //if set adds a () constructor
        if addUnitCtor then
            match controllerType.TryGetUnitConstructor() with
            | None -> failwithf "No empty constructor found for type: %s" controllerType.Name
            | Some ctor -> let emptyctor = ProvidedConstructor([], InvokeCode = Expr.emptyInvoke, BaseConstructorCall = fun args -> ctor, args)
                           providedController.AddMember (emptyctor)

        //If register set and not isAbstract, then automatically registers using [<Register>]
        if register && not isAbstract then
            let register = CustomAttributeDataExt.Make (assembly.GetType("Foundation.RegisterAttribute", true).GetConstructor (typeof<string>), [| CustomAttributeTypedArgument (typeof<string>, customClass) |])
            providedController.AddCustomAttribute (register)

        //Add a little helper that has the "CustomClass" available, this can be used to register without knowing the CustomClass
        providedController.AddMember (mkProvidedLiteralField "CustomClass" customClass)

        //actions
        let actionProvidedMembers =
            [for vc in vcs do
                match vc.View with
                | null -> ()
                | view ->
                    match view.Subviews with
                    | null -> ()
                    | subviews ->
                        yield! subviews
                               |> Seq.collect (fun sv -> sv.Actions)
                               |> Seq.distinct
                               |> Seq.collect (buildAction assembly)]

        providedController.AddMembers actionProvidedMembers 
      
        //outlets
        let providedOutlets =
            [for vc in vcs do
                 for outlet in vc.Outlets do
                     yield vc, outlet ]
            |> List.distinctBy (fun (_, outlet) -> outlet.Property)
            |> List.map (buildOutlet assembly)

        for (field, property) in providedOutlets do
            providedController.AddMembers [field :> MemberInfo; property :> _]

        let releaseOutletsMethod = buildReleaseOutletsMethod (providedOutlets |> List.map fst)

        //add outlet release                                                                       
        providedController.AddMember releaseOutletsMethod

        providedController

    // add InitialViewController to container as property, only the root controller should have this
//    let buildInitialController providedController (designerFile:Uri) = 
//        let storyboardName = designerFile.AbsolutePath |> Path.GetFileNameWithoutExtension
//        ProvidedMethod("CreateInitialViewController", [], providedController,
//                       IsStaticMethod = true,
//                       InvokeCode = fun _ -> let viewController = 
//                                                <@@ let mainStoryboard = UIStoryboard.FromName (storyboardName, null)
//                                                    mainStoryboard.InstantiateInitialViewController () @@>
//                                             Expr.Coerce (viewController, providedController) )