﻿namespace TestTP

open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open System.Reflection

module Helper =
    let doNothing() = ()
    let doNothingOneArg(_:int) = ()
    let doNothingTwoArg(_:int, _: int) = ()
    let doNothingTwoArgCurried(_:int) (_: int) = ()
    [<CompiledName "DoNothingReally">]
    let doNothingWithCompiledName() = ()
    let doNothingGeneric(_:'T) = ()
    let doNothingGenericWithConstraint(_: 'T when 'T: equality) = ()
    let doNothingGenericWithTypeConstraint(_: 'T when 'T :> _ seq) = ()

    let mutable moduleValue = 0
    
    type I =
        abstract DoNothing: unit -> unit
    
    type B() =
        abstract VirtualDoNothing: unit -> unit
        default this.VirtualDoNothing() = () 

    type C() = 
        inherit B()
        let mutable p = 0
        static member DoNothing() = ()
        static member DoNothingOneArg(_:int) = ()
        static member DoNothingOneArg(_:string) = ()
        static member DoNothingTwoArg(_:C, _:int) = ()
        static member DoNothingTwoArgCurried (_:C) (_:int) = ()
        static member DoNothingGeneric(_:'T) = ()
        [<CompiledName "DoNothingReally">]
        static member DoNothingWithCompiledName() = ()
        member __.InstanceDoNothing() = ()
        member __.InstanceDoNothingOneArg(_:int) = ()
        member __.InstanceDoNothingOneArg(_:string) = ()
        member __.InstanceDoNothingTwoArg(_:C, _:int) = ()
        member __.InstanceDoNothingTwoArgCurried(_:C) (_:int) = ()
        member __.InstanceDoNothingGeneric(_:'T) = ()
        [<CompiledName "DoNothingReallyInst">]
        member __.InstanceDoNothingWithCompiledName() = ()
        override __.VirtualDoNothing() = ()

        member __.Property with get() = p and set v = p <- v
        member val AutoProperty = 0 with get, set
        static member val StaticAutoProperty = 0 with get, set

        interface I with 
            member this.DoNothing() = ()

    type G<'U>() = 
        static member DoNothing() = ()
        static member DoNothingOneArg(_:int) = ()
        static member DoNothingTwoArg(_:C, _:int) = ()
        static member DoNothingGeneric(_:'T) = ()
        member __.InstanceDoNothing() = ()
        member __.InstanceDoNothingOneArg(_:int) = ()
        member __.InstanceDoNothingTwoArg(_:C, _:int) = ()
        member __.InstanceDoNothingGeneric(_:'U) = ()

    type R = { A : int; mutable B : int }

open FSharp.Compiler.Service.Tests

[<TypeProvider>]
type BasicProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces ()

    // resolve CSharp_Analysis from referenced assemblies
    do  System.AppDomain.CurrentDomain.add_AssemblyResolve(fun _ args ->
            let name = AssemblyName(args.Name).Name.ToLowerInvariant()
            let an =
                config.ReferencedAssemblies
                |> Seq.tryFind (fun an ->
                    System.IO.Path.GetFileNameWithoutExtension(an).ToLowerInvariant() = name)
            match an with
            | Some f -> Assembly.LoadFrom f
            | None -> null
        )

    let ns = "ErasedWithConstructor.Provided"
    let asm = Assembly.GetExecutingAssembly()

    let createTypes () =
        let myType = ProvidedTypeDefinition(asm, ns, "MyType", Some typeof<obj>)

        let ctor = ProvidedConstructor([], InvokeCode = fun _ -> <@@ "My internal state" :> obj @@>)
        myType.AddMember(ctor)

        let ctor2 = ProvidedConstructor(
                        [ProvidedParameter("InnerState", typeof<string>)],
                        InvokeCode = fun args -> <@@ (%%(args.[0]):string) :> obj @@>)
        myType.AddMember(ctor2)

        let innerState = ProvidedProperty("InnerState", typeof<string>,
                            GetterCode = fun args -> <@@ (%%(args.[0]) :> obj) :?> string @@>)
        myType.AddMember(innerState)

        let someMethod = ProvidedMethod("DoNothing", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.doNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingOneArg", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.doNothingOneArg(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingTwoArg", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.doNothingTwoArg(3, 4) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingTwoArgCurried", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.doNothingTwoArgCurried 3 4 @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingWithCompiledName", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.doNothingWithCompiledName() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingGeneric", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.doNothingGeneric(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingGenericWithConstraint", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.doNothingGenericWithConstraint(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingGenericWithTypeConstraint", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.doNothingGenericWithTypeConstraint([3]) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothing", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C.DoNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothingGeneric", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C.DoNothingGeneric(3) @@>)

        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothingOneArg", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C.DoNothingOneArg(3) @@>)

        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothingTwoArg", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C.DoNothingTwoArg(Helper.C(), 3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothingTwoArgCurried", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C.DoNothingTwoArgCurried (Helper.C()) 3 @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothingWithCompiledName", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C.DoNothingWithCompiledName() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothing", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C().InstanceDoNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothingGeneric", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C().InstanceDoNothingGeneric(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothingOneArg", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C().InstanceDoNothingOneArg(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothingTwoArg", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C().InstanceDoNothingTwoArg(Helper.C(), 3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothingTwoArgCurried", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C().InstanceDoNothingTwoArgCurried (Helper.C()) 3 @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothingWithCompiledName", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C().InstanceDoNothingWithCompiledName() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("InterfaceDoNothing", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ (Helper.C() :> Helper.I).DoNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("OverrideDoNothing", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.C().VirtualDoNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("GenericClassDoNothing", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.G<int>.DoNothing() @@>)
        myType.AddMember(someMethod)

        // These do not seem to compile correctly when used in provided expressions:
        //Helper.G<int>.DoNothingGeneric(3)

        // These do not seem to compile correctly when used in provided expressions:
        //Helper.G<int>().InstanceDoNothingGeneric(3)
                                                         
        let someMethod = ProvidedMethod("GenericClassDoNothingOneArg", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.G<int>.DoNothingOneArg(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("GenericClassDoNothingTwoArg", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.G<int>.DoNothingTwoArg(Helper.C(), 3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("GenericClassInstanceDoNothing", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.G<int>().InstanceDoNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("GenericClassInstanceDoNothingOneArg", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.G<int>().InstanceDoNothingOneArg(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("GenericClassInstanceDoNothingTwoArg", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ Helper.G<int>().InstanceDoNothingTwoArg(Helper.C(), 3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("OptionConstructionAndMatch", [], typeof<int>,
                            InvokeCode = fun _ -> <@@ match Some 1 with None -> 0 | Some x -> x @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ChoiceConstructionAndMatch", [], typeof<int>,
                            InvokeCode = fun _ -> <@@ match Choice1Of2 1 with Choice2Of2 _ -> 0 | Choice1Of2 _ -> 1 @@>)
            // TODO: fix type checker to recognize union generated subclasses coming from TPs
//                            InvokeCode = fun args -> <@@ match Choice1Of2 1 with Choice2Of2 _ -> 0 | Choice1Of2 x -> x @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("RecordConstructionAndFieldGetSet", [], typeof<int>,
                            InvokeCode = fun _ -> <@@ let r : Helper.R = { A = 1; B = 0 } in r.B <- 1; r.A @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("TupleConstructionAndGet", [], typeof<int>,
                            InvokeCode = fun _ -> <@@ let t = (1, 2, 3) in (let (_, i, _) = t in i) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethod", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ CSharpClass(0).Method("x") @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethodOptionalParam", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ CSharpClass(0).Method2("x") + CSharpClass(0).Method2() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethodParamArray", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ CSharpClass(0).Method3("x", "y") @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethodGeneric", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ CSharpClass(0).GenericMethod<int>(2) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethodGenericWithConstraint", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ CSharpClass(0).GenericMethod2<obj>(obj()) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethodGenericWithTypeConstraint", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ CSharpClass(0).GenericMethod3<int>(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpExplicitImplementationMethod", [], typeof<unit>,
                            InvokeCode = fun _ -> <@@ (CSharpClass(0) :> ICSharpExplicitInterface).ExplicitMethod("x") @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ModuleValue", [], typeof<int>,
                            InvokeCode = fun _ -> <@@ Helper.moduleValue <- 1; Helper.moduleValue @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassProperty", [], typeof<int>,
                            InvokeCode = fun _ -> <@@ let x = Helper.C() in x.Property <- 1; x.Property @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassAutoProperty", [], typeof<int>,
                            InvokeCode = fun _ -> <@@ let x = Helper.C() in x.AutoProperty <- 1; x.AutoProperty @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassStaticAutoProperty", [], typeof<int>,
                            InvokeCode = fun _ -> <@@ Helper.C.StaticAutoProperty <- 1; Helper.C.StaticAutoProperty @@>)
        myType.AddMember(someMethod)

        [myType]  

    do
        this.AddNamespace(ns, createTypes())

[<assembly:TypeProviderAssembly>]
do ()