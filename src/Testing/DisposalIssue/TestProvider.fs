namespace DisposalIssue

open System
open System.IO
open System.Reflection
open FSharp.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Swensen.Unquote.Extensions

[<TypeProvider>] 
type TestProvider(config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    do
        let ns = "Test"
        let asm = Assembly.GetExecutingAssembly()
        
        let providedAssembly = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
        let rootType = ProvidedTypeDefinition(asm, ns, "TestProvider", None, HideObjectMethods = true, IsErased = false)



        let buildTypes typeName (parameterValues: obj []) =
            //create the tests type
            let testType = ProvidedTypeDefinition(asm, ns, typeName, Some(typeof<obj>), IsErased = false)
            let ctor = ProvidedConstructor([], InvokeCode=fun _ -> <@@ () @@>)
            testType.AddMember ctor
            
            //create three provided fields, all of which imnplement IDisposable
            let field1 = ProvidedField("Field1", typeof<System.Drawing.Brush>)
            let field2 = ProvidedField("Field2", typeof<System.Drawing.Font>)
            let field3 = ProvidedField("Field3", typeof<System.Drawing.Graphics>)
            
            //set tyhe attributes as puvlic, just to simplify this example
            field1.SetFieldAttributes FieldAttributes.Public
            field2.SetFieldAttributes FieldAttributes.Public
            field3.SetFieldAttributes FieldAttributes.Public
            
            //Add fields to Test type
            testType.AddMembers [field1;field2;field3]
            
            //create a provided Method that will call dispose on all the fields
            let disposer = ProvidedMethod("Dispose", [], typeof<Void>)
            disposer.InvokeCode <- fun args -> let instance = args.[0]
                                               let get = Expr.FieldGet(instance, field1)
                                               let field = Expr.Coerce(get, typeof<obj>)
                                               let works = <@@ if %%field <>  null then
                                                                   ((%%field:obj) :?> IDisposable).Dispose() @@>
                                               //works

                                               let operators = Type.GetType("Microsoft.FSharp.Core.Operators, FSharp.Core")
                                               let intrinsicFunctions = Type.GetType("Microsoft.FSharp.Core.LanguagePrimitives+IntrinsicFunctions, FSharp.Core")
                                               let inequality = operators.GetMethod("op_Inequality")
                                               let genineqtyped = ProvidedTypeBuilder.MakeGenericMethod(inequality, [typeof<obj>;typeof<obj>])

                                               let unboxGenericMethod = intrinsicFunctions.GetMethod("UnboxGeneric")
                                               let unboxGenericMethodTyped = ProvidedTypeBuilder.MakeGenericMethod(unboxGenericMethod, [typeof<IDisposable>])

                                               let disposeMethod = typeof<IDisposable>.GetMethod("Dispose")


                                               let coerceToObj = Expr.Coerce(get, typeof<obj>)
                                               let guard = Expr.Call(genineqtyped, [coerceToObj; Expr.Value(null) ])
                                               let trueblock = Expr.Call(Expr.Call(unboxGenericMethodTyped, [Expr.Coerce(get, typeof<obj>)]), disposeMethod, [])
                                             
                                               let newAttempt = Expr.IfThenElse(guard, trueblock, <@@ () @@>)
                                               let worksdecomp = works.Decompile()
                                               let newdecomp = newAttempt.Decompile()

                                               works
                                              

            //add field disposer
            testType.AddMember disposer

            //pump types into the correct assembly
            providedAssembly.AddTypes [testType]

            //return our created types
            testType

        rootType.DefineStaticParameters([ProvidedStaticParameter("Dummy", typeof<string>)], buildTypes)
            
        
        //add the root type provider and namespace 
        this.AddNamespace(ns, [rootType])
    
[<assembly:TypeProviderAssembly()>] 
do()