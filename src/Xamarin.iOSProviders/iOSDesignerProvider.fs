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
open iOSDesignerTypeProvider.ProvidedTypes
open MonoTouch.Design 

module Sanitise =
    let cleanTrailing = String.trimEnd [|':'|]
    let makeFieldName (name:string) = 
        "__" +
        (name |> String.uncapitalize |> String.trimEnd [|':'|])
    
    let makePropertyName (name:string) = 
        name |> String.capitalize |> String.trimEnd [|':'|]

    let makeMethodName (name:string) = 
        name |> String.capitalize |> String.trimEnd [|':'|] 

    let makeSelectorMethodName (name:string) = 
        (makeMethodName name) + "Selector"

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

    let buildTypes (designerFile:Uri) (vc: ProxiedViewController) isAbstract addUnitCtor register (config:TypeProviderConfig) =
            let actions =
                match vc.View with
                | null -> Seq.empty
                | view ->
                    match view.Subviews with
                    | null -> Seq.empty
                    | subviews -> subviews |> Seq.map (fun sv -> sv.Actions) |> Seq.concat

            let controllerType = typeMap vc

            let providedController = ProvidedTypeDefinition(vc.CustomClass + "Base", Some controllerType, IsErased=false )
            providedController.SetAttributes (if isAbstract then TypeAttributes.Public ||| TypeAttributes.Class ||| TypeAttributes.Abstract
                                              else TypeAttributes.Public ||| TypeAttributes.Class)

            //Were relying on the fact all controller have the IntPtr and unit constructors, At least we propagate our own errors here.
            //Type lookups based on Controler type may be needed to find and add the correct ConstructorInfo

            //IntPtr ctor
            match controllerType.TryGetConstructor(typeof<IntPtr>) with
            | None -> failwithf "No IntPtr constructor found for type: %s" controllerType.Name
            | Some ctor -> let intPtrCtor = ProvidedConstructor([ProvidedParameter("handle", typeof<IntPtr>)], InvokeCode=Expr.emptyInvoke, BaseConstructorCall = fun args -> ctor, args)
                           providedController.AddMember(intPtrCtor)

            //unit ctor
            if addUnitCtor then
                match controllerType.TryGetUnitConstructor() with
                | None -> failwithf "No empty constructor found for type: %s" controllerType.Name
                | Some ctor -> let emptyctor = ProvidedConstructor([], InvokeCode=Expr.emptyInvoke, BaseConstructorCall = fun args -> ctor, args)
                               providedController.AddMember(emptyctor)

            if register then
                let register = Attributes.MakeRegisterAttributeData vc.CustomClass
                providedController.AddCustomAttribute(register)

            providedController.AddMember <| ProvidedLiteralField("CustomClass", typeof<string>, vc.CustomClass)

            //actions mutable assignment style----------------------------
            //TODO add option for ObservableSource<NSObject>, potentially unneeded as outlets exposes this with observable...
            for action in actions do
                //create a backing field fand property or the action
                let actionField, actionProperty = ProvidedTypes.ProvidedPropertyWithField(Sanitise.makeFieldName action.Selector,
                                                                                          Sanitise.makeMethodName action.Selector,
                                                                                          typeof<Action<NSObject>>)
                
                let actionBinding =
                    ProvidedMethod(methodName=Sanitise.makeSelectorMethodName action.Selector, 
                                   parameters=[ProvidedParameter("sender", typeof<NSObject>)], 
                                   returnType=typeof<Void>, 
                                   InvokeCode = fun args -> let instance = Expr.Cast<Action<NSObject>>(Expr.FieldGet(args.[0], actionField))
                                                            <@@ if %instance <> null then (%instance).Invoke(%%args.[1]) @@>)

                actionBinding.AddCustomAttribute(Attributes.MakeActionAttributeData(action.Selector))
                actionBinding.SetMethodAttrs MethodAttributes.Private

                providedController.AddMember actionField
                providedController.AddMember actionProperty
                providedController.AddMember actionBinding
            //end actions-----------------------------------------

            let makeReleaseOutletsExpr (instance: Expr) (outlets:(Expr -> Expr) array)=
                match outlets with
                | [|single|] -> single instance
                | lots -> lots 
                          |> Array.map (fun o -> o instance) 
                          |> Array.reduce (fun one two -> Expr.Sequential(one, two))

            //outlets-----------------------------------------
            let providedOutlets = 
                vc.Outlets
                |> Array.map (fun outlet ->
                    //type lookup here
                    let uiProxy = vc.Storyboard.FindById(outlet.Destination) :?> ProxiedUiKitObject
                    let outletField, outletProperty = ProvidedTypes.ProvidedPropertyWithField(Sanitise.makeFieldName outlet.Property,
                                                                                              Sanitise.makePropertyName outlet.Property,
                                                                                              typeMap uiProxy)
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
                    providedController.AddMember outletField
                    providedController.AddMember outletProperty

                    disposal)       


            let releaseOutletsMethod =
                ProvidedMethod("ReleaseDesignerOutlets", [], typeof<Void>, 
                               InvokeCode = function
                                            | [instance] -> if Array.isEmpty providedOutlets then Expr.emptyInvoke ()
                                                            else makeReleaseOutletsExpr instance providedOutlets
                                            | _ -> invalidOp "Too many arguments")
                                                                                     
            providedController.AddMember releaseOutletsMethod
            //outlets-----------------------------------------

            //static helpers
            //TODO: Onlt the root controller should have this, maybe a Property on the root provided namespace should provide this?
            let staticHelper =
                let storyboardName = designerFile.AbsolutePath |> Path.GetFileNameWithoutExtension
                ProvidedMethod("CreateInitialViewController", [], providedController,
                               IsStaticMethod = true,
                               InvokeCode = fun _ -> let viewController = 
                                                        <@@ let mainStoryboard = UIStoryboard.FromName (storyboardName, null)
                                                            mainStoryboard.InstantiateInitialViewController () @@>
                                                     Expr.Coerce (viewController, providedController) )

            providedController.AddMember staticHelper
            providedController

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

        let scenes = 
            match Parser.Instance.Parse(xdoc.Root) with
            | :? Storyboard as sb ->
                sb.Scenes
            //TODO: Support Xibs!
            | :? Xib as xib -> failwith "Xib files are currently not supported"
            | _ -> failwith "Could not parse file, no storyboard or xib types were found"

        let types = 
            scenes 
            |> Seq.map (fun controller -> TypeBuilder.buildTypes designerFile controller.ViewController isAbstract addUnitCtor register config)
        
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