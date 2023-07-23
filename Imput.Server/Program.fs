module Imput.Server.Program

open System
open System.Net.WebSockets
open System.Reactive.Linq
open System.Text
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Reactive
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

open Imput

type KeyboardListenerHostedService(logger: ILogger<KeyboardListenerHostedService>, keyboardListener: IKeyboardListener) =
    inherit BackgroundService()
    override this.ExecuteAsync(stoppingToken) = task {
        use _ =
            keyboardListener.Keys
            |> Observable.subscribe ^fun ev ->
                logger.LogInformation("{Action} {KeyCode}", ev.Action, ev.KeyCode)
        do! Task.Delay(Timeout.Infinite, stoppingToken)
    }

let sendKeys (applicationLifetime: IHostApplicationLifetime) (keyboardListener: IKeyboardListener) (webSocket: WebSocket) = task {
    try
        do! keyboardListener.Keys
            |> Observable.flatmapTask ^fun keyEvent -> task {
                let keyActionStr =
                    match keyEvent.Action with
                    | KeyAction.Up -> "up"
                    | KeyAction.Down -> "down"
                let data = $"%s{keyActionStr},%i{keyEvent.KeyCode}"
                let buffer = ReadOnlyMemory(Encoding.UTF8.GetBytes(data))
                do! webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None)
            }
            |> fun obs -> Observable.ForEachAsync(obs, ignore, applicationLifetime.ApplicationStopping)
    with :? OperationCanceledException -> ()
}

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Logging.AddSimpleConsole(fun formatter ->
        formatter.SingleLine <- true
    ) |> ignore

    // builder.Services.AddTransient<IKeyboardListener, XInputKeyboardListener>(fun _ -> XInputKeyboardListener("13")) |> ignore
    builder.Services.AddTransient<IKeyboardListener, LinuxDevInputEventKeyboardListener>(fun _ -> LinuxDevInputEventKeyboardListener(17)) |> ignore
    builder.Services.AddHostedService<KeyboardListenerHostedService>() |> ignore

    let app = builder.Build()

    app.UseWebSockets() |> ignore
    app.Use(fun ctx (next: RequestDelegate) -> (task {
        if ctx.Request.Path = PathString("/ws/keys") then
            if ctx.WebSockets.IsWebSocketRequest then
                let! webSocket = ctx.WebSockets.AcceptWebSocketAsync()
                let keyboardListener = ctx.RequestServices.GetRequiredService<IKeyboardListener>()
                let applicationLifetime = ctx.RequestServices.GetRequiredService<IHostApplicationLifetime>()
                app.Logger.LogInformation("New client connected")
                return! sendKeys applicationLifetime keyboardListener webSocket
            else
                ctx.Response.StatusCode <- StatusCodes.Status400BadRequest
        else
            return! next.Invoke(ctx)
    } :> Task)) |> ignore

    app.UseStaticFiles() |> ignore

    app.Run()
    0
