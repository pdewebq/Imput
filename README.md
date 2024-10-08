# Imput

## Description

The Imput daemon is a tool that captures all OS inputs and forwards them to designated listeners.
One of these listeners is the webui that integrates with the daemon. The webui, accessible through any modern web browser,
acts as an observer of input events, rendering them as they occur.

The webui is designed to be lightweight and straightforward, with the HTML files themselves serving as its configuration.
Its focus is on simplicity, dedicating almost 99% of its makeup to input layout and styling. This minimalist approach
allows for efficient handling of input events, providing users with a seamless and visually appealing experience.

## Getting started

### Installation

> ### Windows
>
> Download the latest release in the [releases page](https://github.com/pdewebq/Imput/releases). Choose `win-x64` or `win-x64-self-contained`.
> - `win-x64` — you will need to install [.NET 8.0 Runtime and ASP.NET Core Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-aspnetcore-8.0.8-windows-hosting-bundle-installer) runtime. The binaries size is much lower.
> - `win-x64-self-contained` — nothing is needed to install. The binaries size is much larger.

> ### Linux
>
> Install [.NET 8.0 Runtime and ASP.NET Core Runtime 8.0](https://learn.microsoft.com/dotnet/core/install/linux).
> Download the latest release in the [releases page](https://github.com/pdewebq/Imput/releases). Choose `linux-x64`.

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
