namespace Imput

open System
open System.Reactive.Linq
open System.Reflection
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Reactive
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Builder

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

type InputHub(logger: ILogger<InputHub>) =
    inherit Hub()
    override this.OnConnectedAsync() = task {
        logger.LogInformation("New client {ConnectionId} connected", this.Context.ConnectionId)
    }
    override this.OnDisconnectedAsync(_ex) = task {
        logger.LogInformation("Client {ConnectionId} disconnected", this.Context.ConnectionId)
    }

type InputNotifier(hub: IHubContext<InputHub>, inputListener: IInputListener) =
    inherit BackgroundService()
    override this.ExecuteAsync(stoppingToken) = task {
        try
            do! inputListener.Keys
                |> Observable.flatmapTask ^fun keyEvent -> task {
                    let keyStateStr =
                        match keyEvent.State with
                        | KeyState.Up -> "up"
                        | KeyState.Down -> "down"
                    do! hub.Clients.All.SendAsync("ReceiveKey", keyEvent.Code, keyStateStr, keyEvent.NativeCode)
                }
                |> fun obs -> Observable.ForEachAsync(obs, ignore, stoppingToken)
        with :? OperationCanceledException ->
            ()
    }

module Program =

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

        builder.Services.AddSignalR() |> ignore

        builder.Services.AddHostedService<InputNotifier>() |> ignore

        let app = builder.Build()

        app.Logger.LogInformation("Version {Version}", Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion)

        app.MapHub<InputHub>("/input") |> ignore

        app.UseStaticFiles() |> ignore

        app.Run()
        0
