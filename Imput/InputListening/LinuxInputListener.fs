namespace Imput.InputListening.Linux

open System.IO
open System.Reactive.Linq
open System.Runtime.InteropServices
open FSharp.Control.Reactive
open FsToolkit.ErrorHandling

open Imput
open Imput.InputListening


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

type LinuxDevInputEventInputListener(eventId: int) =
    interface IInputListener with
        member this.Keys =
            // let buffer = Array.zeroCreate 24
            let lines =
                Observable.using (fun () -> new FileStream($"/dev/input/event%i{eventId}", FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) ^fun stream ->
                    Observable.FromAsync(fun () -> task {
                        let buffer = Array.zeroCreate 24
                        let! _ = stream.ReadAsync(buffer, 0, buffer.Length)
                        let kernelInputEvent = MemoryMarshal.Read<KernelInputEvent>(buffer)
                        return option {
                            do! Option.requireTrue (kernelInputEvent.Type = 1s)
                            let! keyAction = kernelInputEvent.Value |> function 1 -> Some KeyAction.Down | 0 -> Some KeyAction.Up | _ -> None
                            let keycode = kernelInputEvent.Code + 8s |> int
                            return { Action = keyAction; KeyCode = keycode }
                        }
                    })
                    |> Observable.choose id
                    |> Observable.repeat
            lines
