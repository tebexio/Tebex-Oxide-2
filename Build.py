#!/usr/bin/env python3
import BuildConfig
import time
import websocket
from websocket import create_connection
import json
import threading
import argparse
import os

# websocket to dev rust server
ws = None

# whether we detected a successful compilation via rcon
reload_successful = False

def main():
    parser = argparse.ArgumentParser(description='Manage build operations for Rust.')
    
    parser.add_argument('--TestRemoteReload', action='store_true', help='Connect to the dev Rust server and check if the plugin compiles/reloads.')
    parser.add_argument('--DeployTest', action='store_true', help='Runs a deployment script.')
    parser.add_argument('--OpenDevConsole', action='store_true', help='Connect to the dev Rust server and open an interactive console.')
    
    
    args = parser.parse_args()

    # Merge is the main build action
    merge_source_files()

    if args.DeployTest:
        print("Deploying to test server...")
        os.system("./DeployTest.sh")
        
    if args.TestRemoteReload or args.OpenDevConsole:
        threading.Thread(target=connect_websocket_and_read,
            args=(f"ws://{BuildConfig.dev_rust_server_ip}:{BuildConfig.dev_rust_server_rcon_port}/{BuildConfig.dev_rust_server_rcon_password}",), daemon=True).start()

        time.sleep(1)
        if args.TestRemoteReload:
            test_remote_reload()
        
        if args.OpenDevConsole:
            open_dev_console()

def merge_source_files():
    print('Merging source files...')

    output = {}
    for file_name in os.listdir(BuildConfig.source_dir):
        if file_name not in BuildConfig.source_files:
            continue

        file_path = f'{BuildConfig.source_dir}/{file_name}'
        with open(file_path, 'r') as file:
            inside_namespace = False
            inside_block = 0

            for line in file:
                stripped_line = line.strip()

                if stripped_line.startswith('namespace'):
                    inside_namespace = True
                elif inside_namespace:
                    if '{' in stripped_line:
                        inside_block += 1
                    if '}' in stripped_line:
                        inside_block -= 1

                    if inside_block == 0:
                        inside_namespace = False
                        continue

                    if inside_block > 0:
                        # Don't print opening braces for the namespace block
                        if inside_block == 1 and (stripped_line == "{"):
                            continue

                        if file_name in output.keys():
                            output[file_name] += line
                        else:
                            output[file_name] = line

    with open(BuildConfig.output_file, 'w') as file:
        file.write(BuildConfig.rust_plugin_header + "\n")
        file.write("namespace Oxide.Plugins\n")
        file.write("{\n")
        
        # Write the plugin first as our classes are defined inside of it.
        file.write(output["Tebex.cs"][:-2]) # remove the close curly brace to leave class definition open
        
        # Write the rest of our files
        for sourceFile in output.keys():
            if sourceFile != "Tebex.cs":
                file.write(output[sourceFile])
        
        # Closing braces
        file.write("\t}\n}\n")

def test_remote_reload():
    print('Checking if the plugin compiles/reloads...')
    send_rcon_command("oxide.reload Tebex")
    time.sleep(2)
    if reload_successful:
        print("Successfully reloaded plugin on remote server.")
    else:
        print("Failed to reload plugin.")


def open_dev_console():
    print('Opening development RCON console on remote. Enter `exit` to close.')
    while True:
        command = input(">>> ")
        if command == "exit":
            break
        else:
            send_rcon_command(command)

def connect_websocket_and_read(wsUrl):
    global ws
    print(f"Connecting via {wsUrl}")
    ws = create_connection(wsUrl)
    ws.settimeout(5)
    while True:
        try:
            response = ws.recv()
            if response:
                parsed_response = json.loads(response)
                on_rcon_response(parsed_response)
        except websocket.WebSocketTimeoutException:
            continue


def on_rcon_response(parsed_response):
    global reload_successful

    message = parsed_response["Message"]
    print("> ", parsed_response)

    if "TebexDonate was compiled successfully" in message:
        reload_successful = True


def send_rcon_command(command):
    global ws
    message = json.dumps({
        "Identifier": -1,
        "Message": command,
        "Name": "WebRcon"
    })
    ws.send(message)


if __name__ == '__main__':
    main()
    