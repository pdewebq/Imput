namespace Imput.InputListening

open System

[<RequireQualifiedAccess>]
type KeyAction =
    | Up
    | Down

type KeyEvent = {
    KeyCode: int
    Action: KeyAction
}

type IInputListener =
    abstract Keys: IObservable<KeyEvent>
