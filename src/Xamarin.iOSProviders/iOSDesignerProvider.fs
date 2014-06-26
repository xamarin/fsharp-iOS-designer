namespace Xamarin.iOSProviders

open System
open System.IO
open System.Reflection
open System.Linq
open System.Xml.Linq
open System.Collections.Generic
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations      
open MonoTouch.Foundation
open MonoTouch.UIKit
open Microsoft.FSharp.Compatibility.OCaml

module Option =
    let fromBoolAndOut (success,value) =
        if success then Some(value) else None

module Sanitise =
    let cleanTrailing = String.trimEnd [|':'|]
    let makeFieldName (name:string) = 
        "__" +
        (name |> String.uncapitalize |> String.trimEnd [|':'|])
    
    let makePropertyName (name:string) = 
        name |> String.capitalize |> String.trimEnd [|':'|]

    let makeMethodName (name:string) = 
        (name |> String.capitalize |> String.trimEnd [|':'|])  + "Selector"

module Expr =
    /// This helper makes working with Expr.Let a little easier and safer
    let LetVar(varName, expr:Expr, f) =  
        let var = Var(varName, expr.Type)
        Expr.Let(var, expr, f (Expr.Var var))

    //creates an empty expression in the form of a unit or ()
    let emptyInvoke = fun _ -> <@@ () @@>

module BindingFlags =
    let publicInstance = BindingFlags.Public ||| BindingFlags.Instance

type ProvidedTypes() =
    static member ProvidedPropertyWithField(name, typ, ?parameters: ProvidedParameter list) =
        
        let field = ProvidedField( Sanitise.makePropertyName name, typ)
        field.SetFieldAttributes FieldAttributes.Private

        let property = ProvidedProperty(Sanitise.makePropertyName name, typ, defaultArg parameters [])
        property.GetterCode <- fun args -> Expr.FieldGet(args.[0], field)
        property.SetterCode <- fun args -> Expr.FieldSet(args.[0], field, args.[1])

        field,property



[<AutoOpenAttribute>]
module TypeExt =
    type Type with
        member x.GetConstructor(typ) =
            x.GetConstructor([|typ|])

        member x.TryGetConstructor(typ:Type) =
            x.GetConstructor(typ) |> function null -> None | v -> Some v

        member x.GetUnitConstructor() =
            x.GetConstructor([||])

        member x.TryGetUnitConstructor() =
            x.GetUnitConstructor() |> function null -> None | v -> Some v

        member x.GetVirtualMethods() = 
            x.GetMethods (BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly) 
            |> Seq.filter (fun m -> m.IsVirtual)       
          
type CustomAttributeDataExt =
    static member Make(ctorInfo, ?args, ?namedArgs) = 
        #if FX_NO_CUSTOMATTRIBUTEDATA
        { new IProvidedCustomAttributeData with 
        #else
        { new CustomAttributeData() with 
        #endif
            member __.Constructor =  ctorInfo
            member __.ConstructorArguments = defaultArg args [||] :> IList<_>
            member __.NamedArguments = defaultArg namedArgs [||] :> IList<_> }
         
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
    let buildTypes (designerFile:Uri) (viewControllerElement: XElement) isAbstract addUnitCtor register =
            let actions = 
                viewControllerElement.Descendants(Xml.xn "action") 
                |> Seq.map IosAction.Parse

            let outlets =
                viewControllerElement.Descendants(Xml.xn "outlet") 
                |> Seq.map Outlet.Parse |> Seq.toArray

            let viewController = ViewController.Parse(viewControllerElement)
                
            // Generate the required type
            let controllerType =
                TypeSystem.typeMap 
                |> Map.filter (fun k v -> k.EndsWith("Controller"))
                |> Map.tryFind viewController.sceneMemberID
                |> function Some(t) -> t | _ -> failwith "No valid type found"

            let viewControllerType = ProvidedTypeDefinition(viewController.customClass + "Base", Some controllerType, IsErased=false )
            viewControllerType.SetAttributes (if isAbstract then TypeAttributes.Public ||| TypeAttributes.Class ||| TypeAttributes.Abstract
                                              else TypeAttributes.Public ||| TypeAttributes.Class)

            //Were relying on the fact all controller have the IntPtr and unit constructors, At least we propagate our own errors here.
            //Type lookups based on Controler type may be needed to find and add the correct ConstructorInfo

            //IntPtr ctor
            match controllerType.TryGetConstructor(typeof<IntPtr>) with
            | None -> failwithf "No IntPtr constructor found for type: %s" controllerType.Name
            | Some ctor -> let intPtrCtor = ProvidedConstructor([ProvidedParameter("handle", typeof<IntPtr>)], InvokeCode=Expr.emptyInvoke, BaseConstructorCall = fun args -> ctor, args)
                           viewControllerType.AddMember(intPtrCtor)

            //unit ctor
            if addUnitCtor then
                match controllerType.TryGetUnitConstructor() with
                | None -> failwithf "No empty constructor found for type: %s" controllerType.Name
                | Some ctor -> let emptyctor = ProvidedConstructor([], InvokeCode=Expr.emptyInvoke, BaseConstructorCall = fun args -> ctor, args)
                               viewControllerType.AddMember(emptyctor)

            if register then
                let register = Attributes.MakeRegisterAttributeData viewController.customClass
                viewControllerType.AddCustomAttribute(register)

            viewControllerType.AddMember <| ProvidedLiteralField("CustomClass", typeof<string>, viewController.customClass)

            //actions mutable assignment style----------------------------
            //TODO add option for ObservableSource<NSObject>, potentially unneeded as outlets exposes this with observable...
            for action in actions do
                //create a backing field fand property or the action
                let actionField, actionProperty = ProvidedTypes.ProvidedPropertyWithField( action.selector, typeof<Action<NSObject>>)
                
                let actionBinding =
                    ProvidedMethod(methodName=Sanitise.makeMethodName action.selector, 
                                   parameters=[ProvidedParameter("sender", typeof<NSObject>)], 
                                   returnType=typeof<Void>, 
                                   InvokeCode = fun args -> let instance = Expr.Cast<Action<NSObject>>(Expr.FieldGet(args.[0], actionField))
                                                            <@@ if %instance <> null then (%instance).Invoke(%%args.[1]) @@>)

                actionBinding.AddCustomAttribute(Attributes.MakeActionAttributeData(action.selector))
                actionBinding.SetMethodAttrs MethodAttributes.Private

                viewControllerType.AddMember actionField
                viewControllerType.AddMember actionProperty
                viewControllerType.AddMember actionBinding
            //end actions-----------------------------------------

            let makeReleaseOutletsExpr (instance: Expr) (outlets:(Expr -> Expr) array)=
                match outlets with
                | [|single|] -> single instance
                | lots -> lots 
                          |> Array.map (fun o -> o instance) 
                          |> Array.reduce (fun one two -> Expr.Sequential(one, two))

            //outlets-----------------------------------------
            let providedOutlets = 
                outlets
                |> Array.map (fun outlet ->
                    let outletField, outletProperty = ProvidedTypes.ProvidedPropertyWithField( outlet.Name, outlet.Type)
                    outletProperty.AddCustomAttribute <| Attributes.MakeOutletAttributeData()

                    ///takes an instance returns a disposal expresion
                    let disposal(instance) =
                        let get = Expr.FieldGet(instance, outletField)
                        let field = Expr.Coerce(get, typeof<obj>)
                        <@@ if %%field <>  null then
                               ((%%field:obj) :?> IDisposable).Dispose() @@>

                    //This is Expr equivelent of the above
                    //let operators = Type.GetType("Microsoft.FSharp.Core.Operators, FSharp.Core")
                    //let intrinsicFunctions = Type.GetType("Microsoft.FSharp.Core.LanguagePrimitives+IntrinsicFunctions, FSharp.Core")
                    //let inequality = operators.GetMethod("op_Inequality")
                    //let genineqtyped = ProvidedTypeBuilder.MakeGenericMethod(inequality, [typeof<obj>;typeof<obj>])
                    //
                    //let unboxGenericMethod = intrinsicFunctions.GetMethod("UnboxGeneric")
                    //let unboxGenericMethodTyped = ProvidedTypeBuilder.MakeGenericMethod(unboxGenericMethod, [typeof<IDisposable>])
                    //
                    //let disposeMethod = typeof<IDisposable>.GetMethod("Dispose")
                    //
                    //
                    //let coerceToObj = Expr.Coerce(get, typeof<obj>)
                    //let guard = Expr.Call(genineqtyped, [coerceToObj; Expr.Value(null) ])
                    //let trueblock = Expr.Call(Expr.Call(unboxGenericMethodTyped, [Expr.Coerce(get, typeof<obj>)]), disposeMethod, [])
                    //
                    //Expr.IfThenElse(guard, trueblock, <@@ () @@>)

                    //Add the property and backing fields to the view controller
                    viewControllerType.AddMember outletField
                    viewControllerType.AddMember outletProperty

                    disposal)       


            let releaseOutletsMethod =
                ProvidedMethod("ReleaseDesignerOutlets", [], typeof<Void>, 
                               InvokeCode = function
                                            | [instance] -> if Array.isEmpty providedOutlets then Expr.emptyInvoke ()
                                                            else makeReleaseOutletsExpr instance providedOutlets
                                            | _ -> invalidOp "Too many arguments")
                                                                                     
            viewControllerType.AddMember releaseOutletsMethod
            //outlets-----------------------------------------

            //static helpers
            let staticHelper =
                let storyboardName = designerFile.AbsolutePath |> Path.GetFileNameWithoutExtension
                ProvidedMethod("CreateInitialViewController", [], viewControllerType,
                               IsStaticMethod = true,
                               InvokeCode = fun _ -> let viewController = 
                                                        <@@ let mainStoryboard = UIStoryboard.FromName (storyboardName, null)
                                                            mainStoryboard.InstantiateInitialViewController () @@>
                                                     Expr.Coerce (viewController, viewControllerType) )

            viewControllerType.AddMember staticHelper
            viewControllerType

[<TypeProvider>] 
type iOSDesignerProvider(config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    //TODO figure out how to find this properly, must be a env var or something.
    let assemblybase = "/Developer/MonoTouch/usr/lib/mono/2.1/"

    do this.RegisterProbingFolder assemblybase    

    let ns = "Xamarin.iOSProviders"
    let asm = Assembly.GetExecutingAssembly()
    let rootType = ProvidedTypeDefinition(asm, ns, "UIProvider", None, HideObjectMethods = true, IsErased = false)
    let watchedFile = ref  Unchecked.defaultof<_>
    let buildTypes typeName (parameterValues: obj []) =

        let designerFile =
            let filename = parameterValues.[0] :?> string
            if Path.IsPathRooted filename then Uri(filename)
            else Uri (Path.Combine (config.ResolutionFolder, filename))

        let register = parameterValues.[1] :?> bool
        let isAbstract = parameterValues.[2] :?> bool
        let addUnitCtor = parameterValues.[3] :?> bool

        let stream, watcherDisposer = IO.openWithWatcher designerFile this.Invalidate
        watchedFile := watcherDisposer
        let xdoc = XDocument.Load(stream)
        stream.Dispose()

        //generate storyboard container
        let container = ProvidedTypeDefinition(asm, ns, typeName, Some(typeof<obj>), IsErased=false)

        //TODO try to use MonoTouch.Design parsing, extract the models action/outlets etc
        //let parsed = MonoTouch.Design.ClientParser.Instance.Parse(xdoc.Root)
        let viewControllerElements = xdoc.Descendants(Xml.xn "viewController")
        let types = 
            viewControllerElements 
            |> Seq.map (fun vc -> TypeBuilder.buildTypes designerFile vc isAbstract addUnitCtor register)
        
        //Add the types to the container
        for pt in types do
            container.AddMember pt

        //pump types into the correct assembly
        let assembly = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
        assembly.AddTypes [container]
        container

    do  rootType.DefineStaticParameters([ProvidedStaticParameter ("DesignerFile", typeof<string>)
                                         ProvidedStaticParameter ("IsRegistered", typeof<bool>, false)
                                         ProvidedStaticParameter ("IsAbstract",   typeof<bool>, false)
                                         ProvidedStaticParameter ("AddUnitCtor",  typeof<bool>, false)], buildTypes)

        this.AddNamespace(ns, [rootType])
        this.Disposing.Add (fun _ -> if !watchedFile <> null then (!watchedFile).Dispose())

[<assembly:TypeProviderAssembly()>] 
do()