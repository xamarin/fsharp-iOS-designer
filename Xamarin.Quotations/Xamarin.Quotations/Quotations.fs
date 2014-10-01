namespace Xamarin.Quotations

open System
open System.Reflection
open System.Collections.Generic

open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.DerivedPatterns
open Microsoft.FSharp.Quotations.Patterns

open Mono.Cecil
open Mono.Cecil.Cil

module Quotation =

    let lambdaCount = ref 0

    [<AbstractClass>]
    type internal Variable() =
        abstract Type : TypeReference
        abstract EmitLoad : ILProcessor -> unit
        abstract EmitPreStore : ILProcessor -> unit
        abstract EmitStore : ILProcessor -> unit

    type internal Local(loc : VariableDefinition) =
        inherit Variable()
        override x.Type = loc.VariableType
        override x.EmitLoad il =
            match loc.Index with
            | 0 -> il.Emit(OpCodes.Ldloc_0)
            | 1 -> il.Emit(OpCodes.Ldloc_1)
            | 2 -> il.Emit(OpCodes.Ldloc_2)
            | 3 -> il.Emit(OpCodes.Ldloc_3)
            | s when s >= -127 && s <= 128 -> il.Emit(OpCodes.Ldloc_S, loc)
            | _ -> il.Emit(OpCodes.Ldloc, loc)
        override x.EmitPreStore il = ()
        override x.EmitStore il =
            match loc.Index with
            | 0 -> il.Emit(OpCodes.Stloc_0)
            | 1 -> il.Emit(OpCodes.Stloc_1)
            | 2 -> il.Emit(OpCodes.Stloc_2)
            | 3 -> il.Emit(OpCodes.Stloc_3)
            | s when s >= -127 && s <= 128 -> il.Emit(OpCodes.Stloc_S, loc)
            | _ -> il.Emit(OpCodes.Stloc, loc)

    type internal ClosureField(fld : FieldReference) =
        inherit Variable()
        override x.Type = fld.FieldType
        override x.EmitLoad il =
            il.Emit(OpCodes.Ldarg_0)
            il.Emit(OpCodes.Ldfld, fld)
        override x.EmitPreStore il = il.Emit(OpCodes.Ldarg_0)
        override x.EmitStore il = il.Emit(OpCodes.Stfld, fld)

    type internal Argument(i : int, t : TypeReference) =
        inherit Variable()
        override x.Type = t
        override x.EmitLoad il = il.Emit(OpCodes.Ldarg, i)
        override x.EmitPreStore il = ()
        override x.EmitStore il = il.Emit(OpCodes.Starg, i)

    type internal Locals(meth : MethodDefinition) =
        let vars = Dictionary<Var,Variable>()

        let forType (t : Type) =
            let tr = meth.Module.Import(t)
            let def = VariableDefinition(tr)
            meth.Body.Variables.Add(def)
            Local(def) :> Variable

        let forVar v =
            match vars.TryGetValue(v) with
            | true, var -> var
            | false, _ ->
                let var = forType v.Type
                vars.Add(v, var)
                var
 
        abstract member For : Var -> Variable
        default x.For v = forVar v
        member x.Add v1 v2 = vars.Add(v1, v2)
        member x.Of t = forType t
        member x.Has v = vars.ContainsKey v

    type internal ClosureLocals(meth : MethodDefinition, parent : Locals, generateField : Variable -> FieldDefinition) =
        inherit Locals(meth)
        let fields = Dictionary<Var,ClosureField>()
        member x.Fields = fields.Values :> seq<ClosureField>
        member x.EmitLoads il =
            for v in fields.Keys do
                parent.For(v).EmitLoad(il)
        override x.For v =
            match fields.TryGetValue(v) with
            | true, field -> field :> Variable
            | false, _ ->
                match parent.Has(v) with
                | false -> base.For v
                | true ->
                    let closureField = new ClosureField(generateField(parent.For(v)))
                    fields.Add(v, closureField)
                    closureField :> Variable

    let rec compile (meth : MethodDefinition) (tb : TypeDefinition) (expr : Expr) =
        let locs = Locals(meth)
        let argIndex = ref 0
        let rec iter = function
        | Lambda(var, body) ->
            locs.Add var (Argument(!argIndex, meth.Module.Import(var.Type)))
            argIndex := !argIndex + 1
            iter body
        | other -> compileLambda meth tb locs other
        iter expr
        meth.Body.GetILProcessor().Emit(OpCodes.Ret)
    and internal compileLambda (meth : MethodDefinition) (tb : TypeDefinition) (local : Locals) (expr : Expr) =
        let il = meth.Body.GetILProcessor()
        let rec visit = function
        // Method calls
        //  Instance
        | Call(Some(target), methd, args) -> visit target; visitAll args; call OpCodes.Callvirt methd
        | PropertyGet(Some(target), pi, args) -> visit target; visitAll args; call OpCodes.Callvirt (pi.GetGetMethod())
        | PropertySet(Some(target), pi, args, value) -> visit target; visitAll args; visit value; call OpCodes.Callvirt (pi.GetSetMethod())
        //  Static
        | Call(None, methd, args) -> visitAll args; call OpCodes.Call methd
        | PropertyGet(None, pi, args) -> visitAll args; call OpCodes.Call (pi.GetGetMethod())
        | PropertySet(None, pi, args, value) -> visitAll args; visit value; call OpCodes.Call (pi.GetSetMethod())
        | Application(fn, arg) -> apply arg fn
        | NewObject(ctor, args) -> visitAll args; il.Emit(OpCodes.Newobj, meth.Module.Import(ctor))


        // Control flow
        | Sequential(a, b) -> visit a; visit b
        | ForIntegerRangeLoop(var, a, b, body) ->
            stvar (local.For(var)) a
            preLoop <@@ %%Expr.Var(var) <= (%%b : int) @@> body (Some(Expr.VarSet(var, <@@ %%Expr.Var(var) + %%Expr.Value(1, var.Type) @@>)))

        // Constant values:
        | Value(null,t)
        | Value(_,t) when t = typeof<unit> -> il.Emit(OpCodes.Ldnull)
        | Int32 v -> ldc_i4 v
        | Int64 v  -> il.Emit(OpCodes.Ldc_I8, v)
        | Double v -> il.Emit(OpCodes.Ldc_R8, v)
        | Single v -> il.Emit(OpCodes.Ldc_R4, v)
        | Byte v -> ldc_i4 (int v)
        | Char v -> ldc_i4 (int v)
        | SByte v -> ldc_i4 (int v)
        | UInt16 v -> ldc_i4 (int v)
        | UInt32 v -> ldc_i4 (int v)
        | UInt64 v -> il.Emit(OpCodes.Ldc_I8, int64 v)
        | Bool true -> ldc_i4 1
        | Bool false -> ldc_i4 0
        | String v -> il.Emit(OpCodes.Ldstr, v)

        // Variable values:
        | Let(var, expr, body) -> stvar (local.For(var)) expr; visit body
        | Var(var) -> local.For(var).EmitLoad(il)
        | VarSet(var, expr) -> stvar (local.For(var)) expr
        | FieldGet(Some(instance), field) -> visit instance; il.Emit(OpCodes.Ldfld, meth.Module.Import(field))
        | FieldGet(None, field) -> il.Emit(OpCodes.Ldsfld, meth.Module.Import(field))
        | FieldSet(Some(instance), field, value) -> visit instance; visit value; il.Emit(OpCodes.Stfld, meth.Module.Import(field))
        | FieldSet(None, field, value) -> visit value; il.Emit(OpCodes.Stsfld, meth.Module.Import(field))
        | Coerce(expr, targetType) -> coerce targetType expr
        | Lambda(var, body) -> makeFun var body

        | other -> raise <| NotSupportedException(other.ToString())

        and visitAll exprs = for e in exprs do visit e
        and call op (m : MethodInfo) =
            il.Emit(op, meth.Module.Import(m))
            if m.ReturnType = typeof<unit> then
                il.Emit(OpCodes.Pop)
        and apply arg fn =
            visit fn
            visit arg
            let invoke = fn.Type.GetMethod("Invoke")
            il.Emit(OpCodes.Callvirt, meth.Module.Import(invoke))
            if invoke.ReturnType = typeof<unit> then
                il.Emit(OpCodes.Pop)
        and coerce (targetType : Type) expr =
            visit expr
            // FIXME: is this sufficient here?
            if not (targetType.IsAssignableFrom(expr.Type)) then
                il.Emit(OpCodes.Unbox_Any, meth.Module.Import(targetType))
        and stvar (var : Variable) value =
            var.EmitPreStore il
            visit value
            var.EmitStore il
        and ldc_i4 = function
            | 0  -> il.Emit(OpCodes.Ldc_I4_0)
            | 1  -> il.Emit(OpCodes.Ldc_I4_1)
            | 2  -> il.Emit(OpCodes.Ldc_I4_2)
            | 3  -> il.Emit(OpCodes.Ldc_I4_3)
            | 4  -> il.Emit(OpCodes.Ldc_I4_4)
            | 5  -> il.Emit(OpCodes.Ldc_I4_5)
            | 6  -> il.Emit(OpCodes.Ldc_I4_6)
            | 7  -> il.Emit(OpCodes.Ldc_I4_7)
            | 8  -> il.Emit(OpCodes.Ldc_I4_8)
            | -1 -> il.Emit(OpCodes.Ldc_I4_M1)
            | s when s >= -127 && s <= 128 -> il.Emit(OpCodes.Ldc_I4_S, sbyte s)
            | n -> il.Emit(OpCodes.Ldc_I4, n)
        and goto (label : Instruction) =
            // FIXME: use short form if possible
            il.Emit(OpCodes.Br, label)
        and _if (test : Expr) (body : Expr) =
            let testFail = Instruction.Create(OpCodes.Nop)
            visit test
            il.Emit(OpCodes.Brfalse, testFail)
            visit body
            il.Append(testFail)
        and preLoop (test : Expr) (body : Expr) (postBody : Expr option) =
            let top = Instruction.Create(OpCodes.Nop)
            let exit = Instruction.Create(OpCodes.Nop)
            il.Append(top)
            visit test
            il.Emit(OpCodes.Brfalse, exit)
            visit body
            if postBody.IsSome then
                visit postBody.Value
            il.Emit(OpCodes.Br, top)
            il.Append(exit)
        and makeFun (arg : Var) (body : Expr) =
            let funcType = FSharpType.MakeFunctionType(arg.Type, body.Type)
            let returnType = body.Type |> meth.Module.Import
            let argType = arg.Type |> meth.Module.Import
            let klass = TypeDefinition("", "lambda" + (string !lambdaCount),
                                            TypeAttributes.Class ||| TypeAttributes.NestedAssembly ||| TypeAttributes.Serializable ||| TypeAttributes.BeforeFieldInit,
                                            meth.Module.Import(funcType))
            klass.DeclaringType <- tb
            tb.NestedTypes.Add(klass)
            lambdaCount := !lambdaCount + 1

            let invoke = MethodDefinition("Invoke", MethodAttributes.Public ||| MethodAttributes.Virtual, returnType)
            invoke.Parameters.Add(ParameterDefinition(argType))
            invoke.DeclaringType <- klass
            invoke.IsReuseSlot <- true
            invoke.IsHideBySig <- true
            let closureCount = ref 0
            let closure = ClosureLocals(invoke, local, fun v ->
                let fld = FieldDefinition("closure" + (string !closureCount), FieldAttributes.Private, meth.Module.Import(v.Type))
                fld.DeclaringType <- klass
                klass.Fields.Add(fld)
                closureCount := !closureCount + 1
                fld
            )
            closure.Add arg (Argument(1, argType))
            body |> compileLambda invoke tb closure
            let invokeIl = invoke.Body.GetILProcessor()
            if body.Type = typeof<unit> then
                invokeIl.Emit(OpCodes.Ldnull)
            invokeIl.Emit(OpCodes.Ret)
            klass.Methods.Add(invoke)

            let ctor = MethodDefinition(".ctor",
                                        MethodAttributes.Public ||| MethodAttributes.HideBySig ||| MethodAttributes.SpecialName ||| MethodAttributes.RTSpecialName,
                                        meth.Module.TypeSystem.Void)
            ctor.DeclaringType <- klass
            for t in closure.Fields do
                ctor.Parameters.Add(ParameterDefinition(t.Type))
            let ctorIl = ctor.Body.GetILProcessor()
            ctorIl.Emit(OpCodes.Ldarg_0)
            ctorIl.Emit(OpCodes.Call, funcType.GetConstructor(BindingFlags.Instance ||| BindingFlags.NonPublic, Type.DefaultBinder, Type.EmptyTypes, [||]) |> meth.Module.Import)
            let mutable i = 1
            for field in closure.Fields do
                field.EmitPreStore ctorIl
                ctorIl.Emit(OpCodes.Ldarg, i)
                field.EmitStore ctorIl
            ctorIl.Emit(OpCodes.Ret)
            klass.Methods.Add(ctor)

            closure.EmitLoads il
            il.Emit(OpCodes.Newobj, ctor)
        visit expr
