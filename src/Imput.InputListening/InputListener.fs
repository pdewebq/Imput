namespace Imput.InputListening

open System

[<RequireQualifiedAccess>]
type KeyState =
    | Up
    | Down

type KeyEvent = {
    Code: string
    State: KeyState
}

type IInputListener =
    abstract Keys: IObservable<KeyEvent>
