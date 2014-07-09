﻿namespace Xamarin.iOSProviders

open System
open System.IO
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations      
open MonoTouch.Foundation
open MonoTouch.UIKit
open iOSDesignerTypeProvider.ProvidedTypes
open MonoTouch.Design

module Sanitise =
    let cleanTrailing = String.trimEnd [|':'|]
    let makeFieldName (name:string) = 
        "__" +
        (name |> String.uncapitalize |> String.trimEnd [|':'|])
    
    let makePascalCase (name:string) = 
        name |> String.capitalize |> String.trimEnd [|':'|]

    let makeSelectorMethodName (name:string) = 
        (makePascalCase name) + "Selector"

module Attributes =
    let MakeActionAttributeData(argument:string) =
        CustomAttributeDataExt.Make(typeof<ActionAttribute>.GetConstructor(typeof<string>),
                                    [| CustomAttributeTypedArgument(typeof<ActionAttribute>, argument) |])
            
    let MakeRegisterAttributeData(argument:string) = 
        CustomAttributeDataExt.Make(typeof<RegisterAttribute>.GetConstructor(typeof<string>),
                                    [| CustomAttributeTypedArgument(typeof<string>, argument) |])

    let MakeOutletAttributeData() = 
        CustomAttributeDataExt.Make(typeof<OutletAttribute>.GetUnitConstructor())

module TypeBuilder =

    let typeMap (proxy:ProxiedUiKitObject) =
        //TODO: Expand this to also search in user assemblies
        let monotouchAssembly = typeof<UIButton>.Assembly
        query { for typ in monotouchAssembly.ExportedTypes do
                where (query {for ca in typ.CustomAttributes do
                              exists (ca.AttributeType = typeof<RegisterAttribute> && 
                                      match ca.ConstructorArguments |> Seq.map (fun ca -> ca.Value) |> Seq.toList with   
                                      | [:? string as name; :? bool as isWrapper] -> name = proxy.ClassName
                                      | _ -> false)})
                select typ
                exactlyOne }

    //TODO add option for ObservableSource<NSObject>, potentially unneeded as outlets exposes this with observable...
    let buildAction (action:ActionConnection) =
        //create a backing field fand property or the action
        let actionField, actionProperty =
            ProvidedTypes.ProvidedPropertyWithField(Sanitise.makeFieldName action.Selector,
                                                    Sanitise.makePascalCase action.Selector,
                                                    typeof<Action<NSObject>>)
        
        let actionBinding =
            ProvidedMethod(methodName=Sanitise.makeSelectorMethodName action.Selector, 
                           parameters=[ProvidedParameter("sender", typeof<NSObject>)], 
                           returnType=typeof<Void>, 
                           InvokeCode = fun args -> let instance = Expr.Cast<Action<NSObject>>(Expr.FieldGet(args.[0], actionField))
                                                    <@@ if %instance <> null then (%instance).Invoke(%%args.[1]) @@>)

        actionBinding.AddCustomAttribute(Attributes.MakeActionAttributeData(action.Selector))
        actionBinding.SetMethodAttrs MethodAttributes.Private

        [actionField :> MemberInfo; actionProperty :> _; actionBinding :> _]

    let buildOutlet (vc:ProxiedViewController) (outlet:Outlet) =
            let uiProxy = vc.Storyboard.FindById(outlet.Destination) :?> ProxiedUiKitObject
            let outletField, outletProperty =
                ProvidedTypes.ProvidedPropertyWithField(Sanitise.makeFieldName outlet.Property,
                                                        Sanitise.makePascalCase outlet.Property,
                                                        typeMap uiProxy)
            outletProperty.AddCustomAttribute <| Attributes.MakeOutletAttributeData()

            //Add the property and backing fields to the view controller
            outletField, outletProperty
    
    //takes an instance returns a disposal expresion
    let buildDisposalExpr instance outletField =
        let get = Expr.FieldGet(instance, outletField)
        let field = Expr.Coerce(get, typeof<obj>)
        <@@ if %%field <>  null then
               ((%%field:obj) :?> IDisposable).Dispose() @@>

    let buildReleaseOutletsExpr (instance: Expr) (outlets: _ array)=
        match outlets with
        | [|single|] -> buildDisposalExpr instance single
        | lots -> lots 
                  |> Array.map (fun o -> buildDisposalExpr instance o) 
                  |> Array.reduce (fun one two -> Expr.Sequential(one, two))

    let buildReleaseOutletsMethod fields =
        ProvidedMethod("ReleaseDesignerOutlets", [], typeof<Void>, 
                       InvokeCode = function
                                    | [instance] -> if Array.isEmpty fields then Expr.emptyInvoke ()
                                                    else buildReleaseOutletsExpr instance fields
                                    | _ -> invalidOp "Too many arguments")

    let buildController (designerFile:Uri) (vc: ProxiedViewController) isAbstract addUnitCtor register (config:TypeProviderConfig) =

        //get the real type of the controller proxy
        let controllerType = typeMap vc

        let providedController = ProvidedTypeDefinition( (if isAbstract then vc.CustomClass + "Base" else vc.CustomClass), Some controllerType, IsErased=false )
        providedController.SetAttributes (if isAbstract then TypeAttributes.Public ||| TypeAttributes.Class ||| TypeAttributes.Abstract
                                          else TypeAttributes.Public ||| TypeAttributes.Class)

        //Were relying on the fact all controller have the IntPtr and unit constructors, At least we propagate our own errors here.
        //Type lookups based on Controler type may be needed to find and add the correct ConstructorInfo

        //IntPtr ctor
        match controllerType.TryGetConstructor(typeof<IntPtr>) with
        | None -> failwithf "No IntPtr constructor found for type: %s" controllerType.Name
        | Some ctor -> let intPtrCtor = ProvidedConstructor([ProvidedParameter("handle", typeof<IntPtr>)], InvokeCode=Expr.emptyInvoke, BaseConstructorCall = fun args -> ctor, args)
                       providedController.AddMember(intPtrCtor)

        //if set adds a () constructor
        if addUnitCtor then
            match controllerType.TryGetUnitConstructor() with
            | None -> failwithf "No empty constructor found for type: %s" controllerType.Name
            | Some ctor -> let emptyctor = ProvidedConstructor([], InvokeCode=Expr.emptyInvoke, BaseConstructorCall = fun args -> ctor, args)
                           providedController.AddMember(emptyctor)

        //If register set and nor isAbstract, then automatically registers using [<Register>]
        if register && not isAbstract then
            let register = Attributes.MakeRegisterAttributeData vc.CustomClass
            providedController.AddCustomAttribute(register)

        //Add a little helper that has the "CustomClass" available, this can be used to register without knowing the CustomClass
        providedController.AddMember <| ProvidedLiteralField("CustomClass", typeof<string>, vc.CustomClass)

        //actions
        match vc.View with
        | null -> Seq.empty
        | view ->
            match view.Subviews with
            | null -> Seq.empty
            | subviews -> subviews 
                          |> Seq.map (fun sv -> sv.Actions)
                          |> Seq.concat
        |> Seq.map buildAction 
        |> Seq.iter providedController.AddMembers
      
        //outlets
        let providedOutlets = vc.Outlets |> Array.map (buildOutlet vc)
        for (field, property) in providedOutlets do
            providedController.AddMembers [field:>MemberInfo;property:>_]
        let releaseOutletsMethod = buildReleaseOutletsMethod (providedOutlets |> Array.map fst)

        //add outlet release                                                                       
        providedController.AddMember releaseOutletsMethod
         
        providedController

    // add InitialViewController to container as property, only the root controller should have this
    let buildInitialController providedController (designerFile:Uri) = 
        let storyboardName = designerFile.AbsolutePath |> Path.GetFileNameWithoutExtension
        ProvidedMethod("CreateInitialViewController", [], providedController,
                       IsStaticMethod = true,
                       InvokeCode = fun _ -> let viewController = 
                                                <@@ let mainStoryboard = UIStoryboard.FromName (storyboardName, null)
                                                    mainStoryboard.InstantiateInitialViewController () @@>
                                             Expr.Coerce (viewController, providedController) )