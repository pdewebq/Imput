namespace Imput.InputListening.Windows

open System
open System.ComponentModel
open System.Diagnostics
open System.Reactive.Subjects
open System.Runtime.InteropServices
open FSharp.Control.Reactive
open FsToolkit.ErrorHandling

open Imput
open Imput.InputListening

// https://github.com/ThoNohT/NohBoard/tree/master/NohBoard/Hooking/Interop

module Interop =

    module Defines =

        // # Hooks

        let [<Literal>] WH_KEYBOARD_LL: int = 13

        // # Keyboard messages

        let [<Literal>] WM_KEYDOWN: int = 0x0100

        let [<Literal>] WM_KEYUP: int = 0x0101

        let [<Literal>] WM_SYSKEYUP: int = 0x0104

        let [<Literal>] WM_SYSKEYDOWN: int = 0x0105

        // # Key codes

        // ## Keyboard

        /// The enter key.
        let [<Literal>] VK_RETURN: byte = 0xDuy

        // # Flags

        /// The extended-key flag.
        let [<Literal>] KF_EXTENDED: int = 0x0100

        /// Test the extended-key flag.
        let LLKHF_EXTENDED: int = KF_EXTENDED >>> 8

    module Structs =

        [<Struct; StructLayout(LayoutKind.Sequential)>]
        type KeyboardHookStruct = {
            VirtualKeyCode: int32
            ScanCode: int32
            Flags: int32
            Time: int32
            ExtraInfo: int32
        }

    type HookProc = delegate of nCode: int * wParam: IntPtr * lParam: IntPtr -> IntPtr

    module FunctionsImports =

        [<DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)>]
        extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId)

        [<DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)>]
        [<return: MarshalAs(UnmanagedType.Bool)>]
        extern bool UnhookWindowsHookEx(IntPtr idHook)

        [<DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)>]
        extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam)

        [<DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)>]
        extern IntPtr GetModuleHandle(string lpModuleName)

open Interop
open Interop.Defines
open Interop.Structs
open Interop.FunctionsImports

type WindowsInputListener() =

    interface IInputListener with
        member this.Keys =
            Observable.defer ^fun () ->
                let keySubject = new System.Reactive.Subjects.Subject<KeyEvent>()
                Observable.using
                <| fun () ->
                    let mutable procId: IntPtr = Unchecked.defaultof<_>
                    let proc = HookProc(fun nCode wParam lParam ->
                        let info = Marshal.PtrToStructure<KeyboardHookStruct>(lParam)
                        let extended = (info.Flags &&& LLKHF_EXTENDED) <> 0
                        let keyCode = if extended && info.VirtualKeyCode = int32 VK_RETURN then 1025 else info.VirtualKeyCode
                        let keyEvent = option {
                            let! keyAction =
                                match int32 wParam with
                                | WM_KEYDOWN
                                | WM_SYSKEYDOWN ->
                                    Some KeyAction.Down
                                | WM_KEYUP
                                | WM_SYSKEYUP ->
                                    Some KeyAction.Up
                                | _ ->
                                    None
                            return { KeyCode = keyCode; Action = keyAction }
                        }
                        keyEvent |> Option.iter ^fun keyEvent ->
                            keySubject.OnNext(keyEvent)
                        CallNextHookEx(procId, nCode, wParam, lParam)
                    )
                    use currProcess = Process.GetCurrentProcess()
                    use currModule = currProcess.MainModule
                    procId <- SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(currModule.ModuleName), 0u)
                    Disposable.create ^fun () ->
                        let res = UnhookWindowsHookEx(procId)
                        if not res then
                            raise (Win32Exception(Marshal.GetLastWin32Error()))
                <| fun _ ->
                    keySubject
