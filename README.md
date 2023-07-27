# Imput

## Description

The Imput daemon is a tool that captures all OS inputs and forwards them to designated listeners.
One of these listeners is the webui that integrates with the daemon. The webui, accessible through any modern web browser,
acts as an observer of input events, rendering them as they occur.

The webui is designed to be lightweight and straightforward, with the HTML files themselves serving as its configuration.
Its focus is on simplicity, dedicating almost 99% of its makeup to input layout and styling. This minimalist approach
allows for efficient handling of input events, providing users with a seamless and visually appealing experience.

## Getting started

### Dependencies

- [.NET 7.0](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
    - .NET Runtime 7.0
        - [Linux](https://learn.microsoft.com/dotnet/core/install/linux)
        - [Windows](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-7.0.9-windows-x64-installer)
    - ASP.NET Core Runtime 7.0
        - [Linux](https://learn.microsoft.com/dotnet/core/install/linux)
        - [Windows](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-aspnetcore-7.0.9-windows-x64-installer)

## Usage

### Layout and styling

Layouts are HTML files located in `./wwwroot/layouts/`.
These HTML files allow you to use HTML, CSS, and JavaScript to create the desired layout. You can find some example layouts in this repository for reference.

### Running daemon

To capture user's input the daemon should be run.

> ### Linux
>
> Open a terminal in the application directory and execute the following command:
>
> ```bash
> ./Imput
> ```

> ### Windows
>
> Open a terminal in the application directory and execute the following command:
>
> ```powershell
> ./Imput.exe
> ```
>
> Or just double-click on the `Imput.bat` file. A terminal window will open with the above command executed.

You should keep the daemon running for the webui to function.

### Running WebUI

Once the daemon is running, open your preferred web browser and navigate to `http://localhost:5063/layouts/<YOUR_LAYOUT>.html` to access the specific layout.
You can then test the functionality by pressing keys to ensure everything is functioning as expected.

## Integration with OBS

To integrate the Input Daemon with OBS, simply add a Browser source with the URL pointing to the desired layout, as explained above.
If needed, you can apply custom CSS to the layout, such as `body { zoom: 2; };`, to enlarge the page content to suit your preferences.
