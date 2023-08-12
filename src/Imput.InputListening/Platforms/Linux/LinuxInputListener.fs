namespace Imput.InputListening.Platforms.Linux

open System
open System.IO
open System.Reactive.Linq
open System.Runtime.InteropServices
open Microsoft.Extensions.Logging
open FSharp.Control.Reactive
open FsToolkit.ErrorHandling

open Imput
open Imput.InputListening

module Interop =

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type KernelTimeVal = {
        Sec: int64
        Usec: int64
    }

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type KernelInputEvent = {
        Time: KernelTimeVal
        Type: int16
        Code: int16
        Value: int32
    }

open Interop

type ILinuxKeyCodeMapper =
    abstract FromLinuxKeyCode: linuxKeyCode: int -> string

type LinuxDevInputEventInputListener(keyCodeMapper: ILinuxKeyCodeMapper, stream: Stream) =
    interface IInputListener with
        member this.Keys =
            Observable.FromAsync(fun () -> task {
                let buffer = Array.zeroCreate 24
                let! _ = stream.ReadAsync(buffer, 0, buffer.Length)
                let kernelInputEvent = MemoryMarshal.Read<KernelInputEvent>(buffer)
                return option {
                    do! Option.requireTrue (kernelInputEvent.Type = 1s)
                    let! keyState = kernelInputEvent.Value |> function 1 -> Some KeyState.Down | 0 -> Some KeyState.Up | _ -> None
                    let keycode = kernelInputEvent.Code + 8s |> int
                    return {
                        State = keyState
                        Code = keyCodeMapper.FromLinuxKeyCode(keycode)
                    }
                }
            })
            |> Observable.choose id
            |> Observable.repeat

type AggregateLinuxDevInputEventInputListener(logger: ILogger<AggregateLinuxDevInputEventInputListener>, keyCodeMapper: ILinuxKeyCodeMapper, ?eventFiles: string array) =
    interface IInputListener with
        member this.Keys =
            Observable.defer ^fun () ->
                let eventFiles = eventFiles |> Option.defaultWith (fun () -> Directory.GetFiles("/dev/input/", "event*"))
                eventFiles
                |> Seq.choose ^fun path ->
                    try
                        let stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                        Observable.using (fun () -> stream) ^fun stream ->
                            (LinuxDevInputEventInputListener(keyCodeMapper, stream) :> IInputListener).Keys
                        |> Some
                    with :? UnauthorizedAccessException ->
                        logger.LogDebug("Failed to access input event file: {Path}", path)
                        None
                |> Observable.mergeSeq
