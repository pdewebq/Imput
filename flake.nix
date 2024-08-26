{
  description = "Imput";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-parts.url = "github:hercules-ci/flake-parts";
  };

  outputs = inputs@{ flake-parts, ... }:
    flake-parts.lib.mkFlake { inherit inputs; } {
      systems = [ "x86_64-linux" "aarch64-linux" "aarch64-darwin" "x86_64-darwin" ];
      perSystem = { pkgs, ... }:
        let
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_8_0;
          dotnet-runtime = pkgs.dotnetCorePackages.aspnetcore_8_0;
        in
        rec {
          packages.imput = pkgs.buildDotnetModule {
            pname = "Imput";
            version = "0.2.1";
            src = ./.;
            projectFile = "./src/Imput/Imput.fsproj";
            nugetDeps = ./src/Imput/nugetDeps.nix;

            inherit dotnet-sdk;
            inherit dotnet-runtime;
          };
          packages.default = packages.imput;

          devShells.default = pkgs.mkShell {
            name = "imput-env";
            packages = [
              dotnet-sdk
            ];
          };
        };
    };
}

