namespace Imput.InputListening

open System.Collections.Generic
open System.IO

type KeyCodeMapper(keycodesFile: string) =
    let linuxKeyCodeMap = Dictionary<int, string>()
    let windowsKeyCodeMap = Dictionary<int, string>()
    member this.Load() = task {
        let! lines = File.ReadAllLinesAsync(keycodesFile)
        let rows = lines |> Seq.skip 1
        for row in rows do
            let tokens = row.Split(',', 3)
            let code, linuxKeyCode, windowsKeyCode = tokens.[0], tokens.[1], tokens.[2]
            if linuxKeyCode <> "TODO" then
                linuxKeyCodeMap.[int linuxKeyCode] <- code
            if windowsKeyCode <> "TODO" then
                windowsKeyCodeMap.[int windowsKeyCode] <- code
    }
    member this.FromLinuxKeyCode(linuxKeyCode: int): string =
        match linuxKeyCodeMap.TryGetValue(linuxKeyCode) with
        | true, code -> code
        | false, _ -> "TODO"
    member this.FromWindowsKeyCode(windowsKeyCode: int): string =
        match windowsKeyCodeMap.TryGetValue(windowsKeyCode) with
        | true, code -> code
        | false, _ -> "TODO"
