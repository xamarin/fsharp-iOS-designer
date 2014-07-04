namespace iOSDesignerTypeProvider

open System
open System.Collections.Generic
open System.Reflection
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes

module ProvidedTypes =
    module BindingFlags =
        let publicInstance = BindingFlags.Public ||| BindingFlags.Instance

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

    type ProvidedTypes() =
        static member ProvidedPropertyWithField(fieldName, propertyName, typ, ?parameters: ProvidedParameter list) =
            
            let field = ProvidedField( fieldName, typ)
            field.SetFieldAttributes FieldAttributes.Private

            let property = ProvidedProperty(propertyName, typ, defaultArg parameters [])
            property.GetterCode <- fun args -> Expr.FieldGet(args.[0], field)
            property.SetterCode <- fun args -> Expr.FieldSet(args.[0], field, args.[1])

            field,property

    module Expr =
        /// This helper makes working with Expr.Let a little easier and safer
        let LetVar(varName, expr:Expr, f) =  
            let var = Var(varName, expr.Type)
            Expr.Let(var, expr, f (Expr.Var var))

        //creates an empty expression in the form of a unit or ()
        let emptyInvoke = fun _ -> <@@ () @@>