#!/bin/bash

BLUE='\033[1;34m'
RESET='\033[0m'

info() {
    echo -e "${BLUE}$*${RESET}"
}

# Install SteamCMD if not present
if [ ! -d "./SteamCMD" ]; then
  info "Installing SteamCMD..."
  mkdir ./SteamCMD && cd ./SteamCMD
  curl -sqL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_osx.tar.gz" | tar zxvf -
  chmod +x steamcmd.sh
  cd ..
fi

info "Installing/updating Rust..."
INSTALL_DIR="$(pwd)/rust-dedicated"
./SteamCMD/steamcmd.sh  +@sSteamCmdForcePlatformType linux +force_install_dir "$INSTALL_DIR" +login anonymous +app_update 258550 validate +quit

cd rust-dedicated
info "Installing Oxide..."
OXIDE_DOWNLOAD_URL="https://umod.org/games/rust/download/develop"
curl -sSL "$OXIDE_DOWNLOAD_URL" -o "Oxide.zip"
unzip -o "Oxide.zip" -d "$INSTALL_DIR"

info "Update completed."