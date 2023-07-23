[<AutoOpen>]
module Imput.Utils

open System
open FsToolkit.ErrorHandling

let inline ( ^ ) f x = f x

[<Obsolete("TODO")>]
let todo<'a> = failwith<'a> "todo"

[<RequireQualifiedAccess>]
module Option =

    let requireTrue (value: bool) : unit option =
        if value then Some () else None
