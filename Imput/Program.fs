namespace Imput

open System
open System.Net.WebSockets
open System.Reactive.Linq
open System.Reflection
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Reactive
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

open Imput
open Imput.Platforms.Linux
open Imput.Platforms.Windows

type InputLogger(logger: ILogger<InputLogger>, inputListener: IInputListener) =
    inherit BackgroundService()
    override this.ExecuteAsync(stoppingToken) = task {
        use _ =
            inputListener.Keys
            |> Observable.subscribe ^fun ev ->
                logger.LogInformation("Key {State} {Code} (native: {NativeCode})", ev.State, ev.Code, ev.NativeCode)
        do! Task.Delay(Timeout.Infinite, stoppingToken)
    }

module Program =

    let sendKeys (ctx: HttpContext) (webSocket: WebSocket) = task {
        let inputListener = ctx.RequestServices.GetRequiredService<IInputListener>()
        let applicationLifetime = ctx.RequestServices.GetRequiredService<IHostApplicationLifetime>()
        try
            do! inputListener.Keys
                |> Observable.flatmapTask ^fun keyEvent -> task {
                    let keyStateStr =
                        match keyEvent.State with
                        | KeyState.Up -> "up"
                        | KeyState.Down -> "down"
                    let data = JsonSerializer.Serialize({| keyState = keyStateStr; code = keyEvent.Code; nativeCode = keyEvent.NativeCode |})
                    let buffer = ReadOnlyMemory(Encoding.UTF8.GetBytes(data))
                    do! webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None)
                }
                |> fun obs -> Observable.ForEachAsync(obs, ignore, applicationLifetime.ApplicationStopping)
        with :? OperationCanceledException -> ()
    }

    let getInputListener (config: IConfigurationSection) (services: IServiceProvider) : IInputListener =
        let listenerType = config.GetValue("Type", "Auto")
        match listenerType with
        | "Auto" ->
            if OperatingSystem.IsLinux() then
                AggregateLinuxDevInputEventInputListener(services.GetRequiredService<_>(), services.GetRequiredService<_>())
            elif OperatingSystem.IsWindows() then
                WindowsInputListener(services.GetRequiredService<_>(), services.GetRequiredService<_>())
            else
                raise (PlatformNotSupportedException())
        | "LinuxDevInput" ->
            let eventFilesSect = config.GetRequiredSection("EventFiles")
            let eventFiles =
                if eventFilesSect.Value = "*" then
                    None
                else
                    let eventFiles = eventFilesSect.GetChildren() |> Seq.map (fun s -> s.Value) |> Seq.toArray
                    Some eventFiles
            AggregateLinuxDevInputEventInputListener(
                services.GetRequiredService<_>(), services.GetRequiredService<_>(),
                ?eventFiles=eventFiles
            )
        | "WindowsHook" ->
            WindowsInputListener(services.GetRequiredService<_>(), services.GetRequiredService<_>())
        | _ ->
            failwith $"Invalid InputListener type: {listenerType}"

    [<EntryPoint>]
    let main args =
        let builder = WebApplication.CreateBuilder(args)

        builder.Logging.AddSimpleConsole(fun formatter ->
            formatter.SingleLine <- true
        ) |> ignore

        builder.Services.AddSingleton<KeyCodeMapper>(fun services ->
            let keycodesFile = builder.Environment.ContentRootFileProvider.GetFileInfo("./keycodes.csv")
            let mapper = KeyCodeMapper(keycodesFile.PhysicalPath)
            mapper.Load().GetAwaiter().GetResult()
            mapper
        ) |> ignore
        builder.Services.AddTransient<IInputListener>(fun services ->
            getInputListener (builder.Configuration.GetSection("InputListener")) services
        ) |> ignore
        if builder.Configuration.GetValue("InputLogger:Enable", false) then
            builder.Services.AddHostedService<InputLogger>() |> ignore

        let app = builder.Build()

        app.Logger.LogInformation("Version {Version}", Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion)

        app.UseWebSockets() |> ignore
        app.Use(fun ctx (next: RequestDelegate) -> (task {
            if ctx.Request.Path = PathString("/ws/keys") then
                if ctx.WebSockets.IsWebSocketRequest then
                    let! webSocket = ctx.WebSockets.AcceptWebSocketAsync()
                    app.Logger.LogInformation("New client connected")
                    return! sendKeys ctx webSocket
                else
                    ctx.Response.StatusCode <- StatusCodes.Status400BadRequest
            else
                return! next.Invoke(ctx)
        } :> Task)) |> ignore

        app.UseStaticFiles() |> ignore

        app.Run()
        0
