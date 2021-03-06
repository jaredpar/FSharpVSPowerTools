﻿namespace FSharpVSPowerTools

[<RequireQualifiedAccess>]
module Option =
    open System

    let inline ofNull value =
        if obj.ReferenceEquals(value, null) then None else Some value

    let inline ofNullable (value : Nullable<'a>) =
        if value.HasValue then Some value.Value else None

    let inline toNullable (value : 'a option) =
        match value with
        | Some x -> Nullable<_> x
        | None -> Nullable<_> ()

    let inline attempt (f: unit -> 'a) = try Some <| f() with _ -> None

    /// Gets the value associated with the option or the supplied default value.
    let inline getOrElse v =
        function
        | Some x -> x
        | None -> v

    /// Gets the option if Some x, otherwise the supplied default value.
    let inline orElse v =
        function
        | Some x -> Some x
        | None -> v
    
/// Maybe computation expression builder, copied from ExtCore library
/// https://github.com/jack-pappas/ExtCore/blob/master/ExtCore/Control.fs
[<Sealed>]
type MaybeBuilder () =
    // 'T -> M<'T>
    member inline __.Return value : 'T option =
        Some value

    // M<'T> -> M<'T>
    member inline __.ReturnFrom value : 'T option =
        value

    // unit -> M<'T>
    member inline __.Zero () : unit option =
        Some ()     // TODO : Should this be None?

    // (unit -> M<'T>) -> M<'T>
    member __.Delay (f : unit -> 'T option) : 'T option =
        f ()

    // M<'T> -> M<'T> -> M<'T>
    // or
    // M<unit> -> M<'T> -> M<'T>
    member inline __.Combine (r1, r2 : 'T option) : 'T option =
        match r1 with
        | None ->
            None
        | Some () ->
            r2

    // M<'T> * ('T -> M<'U>) -> M<'U>
    member inline __.Bind (value, f : 'T -> 'U option) : 'U option =
        Option.bind f value

    // 'T * ('T -> M<'U>) -> M<'U> when 'U :> IDisposable
    member this.Using (resource : ('T :> System.IDisposable), body : _ -> _ option) : _ option =
        try body resource
        finally
            if not <| obj.ReferenceEquals (null, box resource) then
                resource.Dispose ()

    // (unit -> bool) * M<'T> -> M<'T>
    member this.While (guard, body : _ option) : _ option =
        if guard () then
            // OPTIMIZE : This could be simplified so we don't need to make calls to Bind and While.
            this.Bind (body, (fun () -> this.While (guard, body)))
        else
            this.Zero ()

    // seq<'T> * ('T -> M<'U>) -> M<'U>
    // or
    // seq<'T> * ('T -> M<'U>) -> seq<M<'U>>
    member this.For (sequence : seq<_>, body : 'T -> unit option) : _ option =
        // OPTIMIZE : This could be simplified so we don't need to make calls to Using, While, Delay.
        this.Using (sequence.GetEnumerator (), fun enum ->
            this.While (
                enum.MoveNext,
                this.Delay (fun () ->
                    body enum.Current)))

[<AutoOpen; CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module Pervasive =
    let inline debug msg = Printf.kprintf System.Diagnostics.Debug.WriteLine msg
    let inline fail msg = Printf.kprintf System.Diagnostics.Debug.Fail msg
    let maybe = MaybeBuilder()
    
    let tryCast<'a> (o: obj) : 'a option = 
        match o with
        | null -> None
        | :? 'a as a -> Some a
        | _ -> fail "Cannot cast %O to %O" (o.GetType()) typeof<'a>.Name; None

