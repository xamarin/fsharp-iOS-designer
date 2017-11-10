namespace Xamarin.UIProviders.DesignTime

open System
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProvidedTypes

[<RequireQualifiedAccess>]
type Generated =
    | ViewControllers of ViewController seq
    | Views of View seq

type Settings = {IsAbstract : bool; AddUnitCtor : bool; Register : bool; BindingType: RunTime.RunTimeBinding; GenerationType : Generated }

module String =
    let trimEnd trimChars (s:string) =
        s.TrimEnd trimChars

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
    
    let template (callback:Expr<('a -> unit) option>) (arg:Expr<'a>) =
        <@@ %callback |> Option.iter (fun f -> f %arg) @@>
        
    let buildAction (runtimeBinding:RunTime.RunTimeBinding) (action:Xamarin.UIProviders.DesignTime.Action) =
        //create a backing field fand property or the action
        let actionAttributeType = runtimeBinding.Assembly.GetType("Foundation.ActionAttribute", true)
        let xmlType = action.ElementName
        let nsObj = TypeMapper.getTypeMap runtimeBinding xmlType
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
                                            let callback = reflectiveCast optCallback (Expr.FieldGet(args.[0], actionField))
                                            let arg = reflectiveCast nsObj args.[1]
                                            let typeBuilder = Type.GetType("Xamarin.UIProviders.DesignTime.TypeBuilder, Xamarin.UIProviders.DesignTime")
                                            let template = typeBuilder.GetMethod("template")
                                            let genericTemplate = template.MakeGenericMethod([|nsObj|])
                                            let quoted = genericTemplate.Invoke(null, [|callback; arg|]) :?> Expr
                                            quoted)

        actionBinding.AddCustomAttribute
            (CustomAttributeDataExt.Make (actionAttributeType.GetConstructor (typeof<string>),
                                          [| CustomAttributeTypedArgument (actionAttributeType, action.Selector) |]))
        actionBinding.SetMethodAttrs MethodAttributes.Private

        [actionField :> MemberInfo
         actionProperty :> _
         actionBinding  :> _]

    let buildOutlet (bindingType:RunTime.RunTimeBinding) (outlet:Outlet) =
        let xmlType = outlet.ElementName
        let outletField, outletProperty =
            ProvidedTypes.ProvidedPropertyWithField (Sanitise.makeFieldName outlet.Property,
                                                     Sanitise.cleanTrailing outlet.Property,
                                                     TypeMapper.getTypeMap bindingType xmlType)
        outletProperty.AddCustomAttribute <| CustomAttributeDataExt.Make (bindingType.Assembly.GetType("Foundation.OutletAttribute", true).GetUnitConstructor ())

        //Add the property and backing fields to the Control
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

    //get the real type of the controller proxy
    let getControlType generatedType bindingType =
        match generatedType with
        | Generated.ViewControllers vcs -> vcs |> Seq.map (fun vc -> TypeMapper.getTypeMap bindingType vc.XmlType)
        | Generated.Views vcs -> vcs |> Seq.map (fun vc -> TypeMapper.getTypeMap bindingType vc.XmlType)
        |> Seq.head

    let getCustomClass generatedType =
        match generatedType with
        | Generated.ViewControllers vcs ->
            let firstVc = vcs |> Seq.head
            firstVc.CustomClass

        | Generated.Views v ->
            let firstV = v |> Seq.head
            firstV.CustomClass

    let buildControls (settings:Settings) (config:TypeProviderConfig) =
        let controllerType = getControlType settings.GenerationType settings.BindingType
        let customClass = getCustomClass settings.GenerationType
        let className = if settings.IsAbstract then customClass + "Base" else customClass

        let providedController = ProvidedTypeDefinition (className, Some controllerType, IsErased = false )
        providedController.SetAttributes (if settings.IsAbstract then TypeAttributes.Public ||| TypeAttributes.Class ||| TypeAttributes.Abstract
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
        if settings.AddUnitCtor then
            match controllerType.TryGetUnitConstructor() with
            | None -> failwithf "No empty constructor found for type: %s" controllerType.Name
            | Some ctor -> let emptyctor = ProvidedConstructor([], InvokeCode = Expr.emptyInvoke, BaseConstructorCall = fun args -> ctor, args)
                           providedController.AddMember (emptyctor)

        //If register set and not isAbstract, then automatically registers using [<Register>]
        if settings.Register && not settings.IsAbstract then
            let register =
               let registerAttribute = settings.BindingType.Assembly.GetType("Foundation.RegisterAttribute", true)
               CustomAttributeDataExt.Make (registerAttribute.GetConstructor (typeof<string>), [| CustomAttributeTypedArgument (typeof<string>, customClass) |])
            providedController.AddCustomAttribute (register)

        //Add a little helper that has the "CustomClass" available, this can be used to register without knowing the CustomClass
        providedController.AddMember (mkProvidedLiteralField "CustomClass" customClass)

        //actions are only currently generated on view controllers
        match settings.GenerationType with
        | Generated.ViewControllers vc ->
            let firstVc = Seq.head vc
            let actionProvidedMembers = firstVc.Actions |> List.collect (buildAction settings.BindingType)
            providedController.AddMembers actionProvidedMembers 
        | _ -> ()
      
        //outlets
        let providedOutlets =
            match settings.GenerationType with
            | Generated.ViewControllers vcs ->
                [for vc in vcs do
                     for outlet in vc.Outlets do
                         yield outlet ]
                |> List.distinctBy (fun outlet -> outlet.Property)
                |> List.map (buildOutlet settings.BindingType)

            | Generated.Views views ->
                [for view in views do
                     for outlet in view.Outlets do
                         yield outlet ]
                |> List.distinctBy (fun outlet -> outlet.Property)
                |> List.map (buildOutlet settings.BindingType)

        for (field, property) in providedOutlets do
            providedController.AddMembers [field :> MemberInfo; property :> _]

        let releaseOutletsMethod = buildReleaseOutletsMethod (providedOutlets |> List.map fst)

        //add outlet release                                                                       
        providedController.AddMember releaseOutletsMethod
        providedController