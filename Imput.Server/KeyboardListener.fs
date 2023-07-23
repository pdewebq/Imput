namespace Imput

open System
open System.Diagnostics
open System.IO
open System.Reactive.Linq
open System.Runtime.InteropServices
open FSharp.Control.Reactive
open FsToolkit.ErrorHandling

[<RequireQualifiedAccess>]
type KeyAction =
    | Up
    | Down

type KeyEvent = {
    KeyCode: int
    Action: KeyAction
}

type IKeyboardListener =
    abstract Keys: IObservable<KeyEvent>

// ----

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

type LinuxDevInputEventKeyboardListener(eventId: int) =
    interface IKeyboardListener with
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

type XInputKeyboardListener(inputId: string) =
    interface IKeyboardListener with
        member this.Keys =
            let proc = new Process()
            proc.StartInfo.FileName <- "xinput"
            proc.StartInfo.Arguments <- $"test {inputId}"
            proc.StartInfo.RedirectStandardOutput <- true

            // TODO: Ensure race conditions
            proc.Start() |> function false -> failwith "Failed to start" | _ -> ()
            let lines =
                Observable.using
                    (fun () -> proc)
                    (fun proc ->
                        let stdoutReader = proc.StandardOutput
                        Observable.FromAsync(fun () -> stdoutReader.ReadLineAsync())
                        |> Observable.repeat
                        |> Observable.takeWhile (fun line -> line <> null)
                    )
            lines
            |> Observable.map ^fun line ->
                let keyAction =
                    if line.StartsWith("key press") then
                        KeyAction.Down
                    elif line.StartsWith("key release") then
                        KeyAction.Up
                    else
                        invalidOp $"Invalid xinput event: {line}"
                let keycode = line.Substring(12) |> int
                { KeyCode = keycode; Action = keyAction }
