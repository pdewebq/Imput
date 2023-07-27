namespace Imput.Platforms.Windows

open System
open System.ComponentModel
open System.Reactive.Subjects
open System.Runtime.InteropServices
open System.Threading
open Microsoft.Extensions.Logging
open FSharp.Control.Reactive
open FsToolkit.ErrorHandling

open Imput

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

        // ----

        let [<Literal>] WM_DESTROY: uint = 0x0002u

    module Structs =

        type DWORD = int
        type WPARAM = IntPtr
        type LPARAM = IntPtr

        [<Struct; StructLayout(LayoutKind.Sequential)>]
        type KeyboardHookStruct = {
            VirtualKeyCode: int32
            ScanCode: int32
            Flags: int32
            Time: int32
            ExtraInfo: int32
        }

        [<Struct; StructLayout(LayoutKind.Sequential, Pack = 4)>]
        type POINT = {
            x: int32
            y: int32
        }

        [<Struct; StructLayout(LayoutKind.Sequential, Pack = 8)>]
        type MSG = {
            hwnd: IntPtr
            message: uint32
            wParam: WPARAM
            lParam: LPARAM
            time: uint32
            pt: POINT
        }

    open Structs

    type HookProc = delegate of nCode: int * wParam: WPARAM * lParam: LPARAM -> int

    module FunctionsImports =

        [<DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)>]
        extern int CallNextHookEx(int idHook, int nCode, WPARAM wParam, LPARAM lParam)

        [<DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)>]
        extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, DWORD dwThreadId)

        [<DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)>]
        extern int UnhookWindowsHookEx(int idHook)

        // ----

        [<DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)>]
        extern bool GetMessage(MSG& lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax)

        [<DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)>]
        extern int64 DispatchMessage(MSG& lpMsg)

        [<DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)>]
        extern bool TranslateMessage(MSG& lpMsg)

        // ----

        [<DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)>]
        extern DWORD GetCurrentThreadId()

        [<DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)>]
        extern bool PostThreadMessage(DWORD idThread, uint Msg, WPARAM wParam, LPARAM lParam)

open Interop
open Interop.Defines
open Interop.Structs
open Interop.FunctionsImports

type WindowsInputListener(logger: ILogger<WindowsInputListener>, keyCodeMapper: KeyCodeMapper) =

    interface IInputListener with
        member this.Keys =
            Observable.defer ^fun () ->
                let keySubject = new System.Reactive.Subjects.Subject<KeyEvent>()
                use messageLoopNativeTreadId = new SyncIVar<DWORD>()

                let mutable procId: int = Unchecked.defaultof<_>
                let messageLoopThread = Thread(fun () ->
                    try
                        messageLoopNativeTreadId.Set(GetCurrentThreadId())

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
                                return {
                                    Action = keyAction
                                    NativeCode = keyCode
                                    Code = keyCodeMapper.FromWindowsKeyCode(keyCode)
                                }
                            }
                            keyEvent |> Option.iter ^fun keyEvent ->
                                keySubject.OnNext(keyEvent)
                            CallNextHookEx(procId, nCode, wParam, lParam)
                        )

                        procId <- SetWindowsHookEx(WH_KEYBOARD_LL, proc, IntPtr.Zero, 0)
                        if procId = 0 then
                            raise (Win32Exception(Marshal.GetLastWin32Error()))

                        let mutable msg: MSG = Unchecked.defaultof<_>
                        let mutable doMessageLoop = true
                        while doMessageLoop && GetMessage(&msg, IntPtr.Zero, 0u, 0u) do
                            if msg.message = WM_DESTROY then
                                doMessageLoop <- false
                            else
                                TranslateMessage(&msg) |> ignore
                                DispatchMessage(&msg) |> ignore
                    with ex ->
                        logger.LogError(ex, "Unhandled exception in message loop")
                        reraise ()
                )
                messageLoopThread.Name <- $"{nameof(WindowsInputListener)} message loop thread"
                messageLoopThread.Start()

                Observable.using
                <| fun () ->
                    Disposable.create ^fun () ->
                        let res = UnhookWindowsHookEx(procId)
                        if res = 0 then
                            raise (Win32Exception(Marshal.GetLastWin32Error()))
                        PostThreadMessage(messageLoopNativeTreadId.WaitValue(), WM_DESTROY, WPARAM.Zero, LPARAM.Zero) |> ignore
                        messageLoopThread.Join()
                <| fun _ ->
                    keySubject
