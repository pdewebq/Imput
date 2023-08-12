namespace Imput.InputListening

open System.Collections.Generic
open System.IO
open Microsoft.Extensions.Logging
open Imput.InputListening.Platforms.Linux
open Imput.InputListening.Platforms.Windows

type CsvTableMultiplatformKeyCodeMapper(logger: ILogger<CsvTableMultiplatformKeyCodeMapper>, keycodesFile: string) =
    let unidentifiedKey = "Unidentified"

    let linuxKeyCodeMap = Dictionary<int, string>()
    let windowsKeyCodeMap = Dictionary<int, string>()
    member this.Load() = task {
        let! lines = File.ReadAllLinesAsync(keycodesFile)
        let rows = lines |> Seq.skip 1
        for row in rows do
            let tokens = row.Split(',', 3)
            let code, linuxKeyCode, windowsKeyCode = tokens.[0], tokens.[1], tokens.[2]
            match linuxKeyCode with
            | "TODO" | "-" -> ()
            | _ ->
                linuxKeyCodeMap.[int linuxKeyCode] <- code
            match windowsKeyCode with
            | "TODO" | "-" -> ()
            | _ ->
                windowsKeyCodeMap.[int windowsKeyCode] <- code
    }

    interface ILinuxKeyCodeMapper with
        member this.FromLinuxKeyCode(linuxKeyCode) =
            match linuxKeyCodeMap.TryGetValue(linuxKeyCode) with
            | true, code -> code
            | false, _ ->
                logger.LogWarning("Unidentified Linux key, native keycode: {NativeKeyCode}", linuxKeyCode)
                unidentifiedKey

    interface IWindowsKeyCodeMapper with
        member this.FromWindowsKeyCode(windowsKeyCode) =
            match windowsKeyCodeMap.TryGetValue(windowsKeyCode) with
            | true, code -> code
            | false, _ ->
                logger.LogWarning("Unidentified Windows key, native keycode: {NativeKeyCode}", windowsKeyCode)
                unidentifiedKey
