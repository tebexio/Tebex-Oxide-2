# Tebex Oxide Plugin for Rust

## Description
[Tebex](https://tebex.io/) provides a monetization and donation platform for game servers, allowing server owners to manage in-game purchases, subscriptions, and donations with ease.

This plugin acts as a bridge between your Rust game server and the Tebex platform, enabling you to offer a wide range of virtual items, packages, and services to your players.

## Commands
The following commands are available through the Tebex Rust Plugin:

### Admin Commands
- `/tebex.secret <secret>`: Set your server's secret key
- `/tebex.sendlink <player> <packageId>`: Send a purchase link for a package to a player.
- `/tebex.forcecheck`: Force run any due online and offline commands.
- `/tebex.refresh`: Refresh your store's listings.
- `/tebex.report`: Prepare a report that can be submitted to our support team.
- `/tebex.ban`: Ban a player from using the store. **Players can only be unbanned from the webstore UI.**
- `/tebex.lookup`: Display information about a customer.

### User Commands
- `/tebex.help`: Display a list of available commands and their descriptions.
- `/tebex.info`: Display public store information.
- `/tebex.categories`: View all store categories.
- `/tebex.packages`: View all store packages.
- `/tebex.checkout <packageId>`: Create a checkout link for a package.
- `/tebex.stats`: View your own player statistics in the store.

## Installation
To install the Tebex Rust Plugin, follow these steps:

1. Download the latest release of this plugin from [Tebex.io](https://docs.tebex.io/plugin/official-plugins).
2. Upload the plugin .cs source file to the `rust_dedicated/oxide/plugins` directory of your Rust game server.
3. Run `oxide.reload TebexDonate` to load the plugin.

## Dev Environment Setup
If you wish to contribute to the development of the plugin, you can set up your development environment as follows:

**Requirements:**
- Python 3
- dotnet
- [Oxide for Rust](https://umod.org/games/rust)

**Setup Instructions:**
1. Clone the repository to an empty folder.
2. Download [Oxide for Rust](https://umod.org/games/rust) and unzip it.
3. Add the assemblies `Oxide.Core`, `Oxide.CSharp`, `Oxide.MySql`, `Oxide.Rust`, and `Facebunch.UnityEngine`, `Assembly-CSharp` as a minimum to the project.

## Building and Testing
Oxide plugins are basic .cs source files - we do not build a .dll or an executable. Instead, we combine our source files
together then ensure it can compile and run on a real Rust server. You can configure a test server in `BuildConfig.py`

1. Ensure your development environment is properly set up per the instructions above.
2. Using `BuildConfig.py.example`, make a `BuildConfig.py` and fill the appropriate values.
3. Run `python3 Build.py`.

This will merge any source files configured in `BuildConfig.py` together into the final plugin in `Build/TebexDonate.cs`

### Build Arguments
The full build and test suite can be ran sequentially with these additional arguments:

- `--DeployTest`: Runs a deployment script, ideally uploads to a test server.
- `--TestRemoteReload`: Test connect to see if a remote Rust server can reload the plugin.
- `--OpenDevConsole`: Open interactive RCON console on a test rust server.

Each run of the build script will always merge and output the final plugin source file.

## Contributions
We welcome contributions from the community. Please refer to the `CONTRIBUTING.md` file for more details. By submitting code to us, you agree to the terms set out in the CONTRIBUTING.md file

## Support
This repository is only used for bug reports via GitHub Issues. If you have found a bug, please [open an issue](https://github.com/tebexio/Tebex-Rust/issues).

If you are a user requiring support for Tebex, please contact us at https://www.tebex.io/contact