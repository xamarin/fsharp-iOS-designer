namespace Xamarin.UIProviders.DesignTime

open System
open System.IO
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
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
    //TODO add option for ObservableSource<NSObject>, potentially unneeded as outlets exposes this with observable...
    
    let buildAction (runtimeBinding:RunTime.RunTimeBinding) (action:Xamarin.UIProviders.DesignTime.Action) =
        //create a backing field fand property or the action

        let actionAttributeType = runtimeBinding.Assembly.GetType("Foundation.ActionAttribute", true)
        let nsObj = runtimeBinding.Assembly.GetType("Foundation.NSObject")
        let callbackUntyped = typedefof<FSharpFunc<_, unit>>
        let callbackTyped = callbackUntyped.MakeGenericType([|nsObj;typeof<unit>|])
        let opt = typedefof<Option<_>>
        let optCallback = opt.MakeGenericType([|callbackTyped|])
        let reflectiveCast t (e:Expr) =
            let expr = typeof<Expr>
            let meth = expr.GetMethod("Cast")
            let genericMeth = meth.MakeGenericMethod([|t|])
            genericMeth.Invoke(null, [|e|])
        
        let actionField, actionProperty =
            ProvidedTypes.ProvidedPropertyWithField (Sanitise.makeFieldName action.Selector,
                                                     Sanitise.makePascalCase action.Selector,
                                                     optCallback)
        let actionBinding =
            ProvidedMethod(methodName = Sanitise.makeSelectorMethodName action.Selector, 
                           parameters = [ProvidedParameter("sender", nsObj) ], 
                           returnType = typeof<Void>, 
                           InvokeCode = fun args ->
                                            let callback = reflectiveCast optCallback (Expr.FieldGet(args.[0], actionField)) :?> Expr
                                            let arg = reflectiveCast nsObj args.[1] :?> Expr
                                            <@@ %%callback |> Option.iter (fun f -> f %%arg) @@>)
                                                    

        actionBinding.AddCustomAttribute
            (CustomAttributeDataExt.Make (actionAttributeType.GetConstructor (typeof<string>),
                                          [| CustomAttributeTypedArgument (actionAttributeType, action.Selector) |]))
        actionBinding.SetMethodAttrs MethodAttributes.Private

        [actionField :> MemberInfo
         actionProperty :> _
         actionBinding  :> _]

    let buildOutlet (bindingType:RunTime.RunTimeBinding) (vc:ViewController, outlet:Outlet) =
            //let uiProxy = vc.Storyboard.FindById (outlet.Destination) :?> ProxiedUiKitObject
            let xmlType = outlet.ElementName
            let outletField, outletProperty =
                ProvidedTypes.ProvidedPropertyWithField (Sanitise.makeFieldName outlet.Property,
                                                         Sanitise.cleanTrailing outlet.Property,
                                                         TypeMapper.getTypeMap bindingType xmlType)
            outletProperty.AddCustomAttribute <| CustomAttributeDataExt.Make (bindingType.Assembly.GetType("Foundation.OutletAttribute", true).GetUnitConstructor ())

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

    let buildController (bindingType: RunTime.RunTimeBinding) (vcs: ViewController seq) isAbstract addUnitCtor register (config:TypeProviderConfig) =

        //get the real type of the controller proxy
        let vcsTypes = vcs |> Seq.map (fun vc -> TypeMapper.getTypeMap bindingType vc.XmlType)
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
            let register = CustomAttributeDataExt.Make (bindingType.Assembly.GetType("Foundation.RegisterAttribute", true).GetConstructor (typeof<string>), [| CustomAttributeTypedArgument (typeof<string>, customClass) |])
            providedController.AddCustomAttribute (register)

        //Add a little helper that has the "CustomClass" available, this can be used to register without knowing the CustomClass
        providedController.AddMember (mkProvidedLiteralField "CustomClass" customClass)

        //actions
        //NOTE: Actions are disabled due to quotation casting issues
        //let actionProvidedMembers = vc.Actions |> List.collect (buildAction bindingType)
        //providedController.AddMembers actionProvidedMembers 
      
        //outlets
        let providedOutlets =
            [for vc in vcs do
                 for outlet in vc.Outlets do
                     yield vc, outlet ]
            |> List.distinctBy (fun (_, outlet) -> outlet.Property)
            |> List.map (buildOutlet bindingType)

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