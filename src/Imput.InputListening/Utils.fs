[<AutoOpen>]
module Imput.Utils

open System
open System.Threading

let inline ( ^ ) f x = f x

[<Obsolete("TODO")>]
let todo<'a> = failwith<'a> "todo"

[<RequireQualifiedAccess>]
module Option =

    let requireTrue (value: bool) : unit option =
        if value then Some () else None

type SyncIVar<'T>() =
    let mutable _value: 'T option = None
    let _waitHandle = new ManualResetEvent(false)

    member _.Set(value: 'T): unit =
        if _value.IsSome then invalidOp "Cannot set value twice"
        _value <- Some value
        _waitHandle.Set() |> ignore

    member _.WaitValue(): 'T =
        _waitHandle.WaitOne() |> ignore
        _value.Value

    interface IDisposable with
        member this.Dispose() = _waitHandle.Dispose()
