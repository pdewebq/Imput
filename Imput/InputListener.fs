namespace Imput

open System

[<RequireQualifiedAccess>]
type KeyAction =
    | Up
    | Down

type KeyEvent = {
    Code: string
    NativeCode: int
    Action: KeyAction
}

type IInputListener =
    abstract Keys: IObservable<KeyEvent>
