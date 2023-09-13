using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;
using Tebex.API;
using Oxide.Plugins;
using Tebex.Triage;

namespace Tebex.Adapters
{
    public class TebexOxideAdapter : BaseTebexAdapter
    {
        public static Oxide.Plugins.Tebex Plugin { get; private set; }

        public TebexOxideAdapter(Oxide.Plugins.Tebex plugin)
        {
            Plugin = plugin;
        }

        public override void Init()
        {
            // Initialize timers, hooks, etc. here
            Plugin.PluginTimers().Every(121.0f, ProcessCommandQueue);
            Plugin.PluginTimers().Every(60.0f, DeleteExecutedCommands);
            Plugin.PluginTimers().Every(60.0f, ProcessJoinQueue);
            Plugin.PluginTimers().Every(60.0f * 30, RefreshStoreInformation); // Every 30 minutes for store info
        }

        public override void LogWarning(string message)
        {
            Plugin.Warn(message);
        }

        public override void LogError(string message)
        {
            Plugin.Error(message);
        }

        public override void LogInfo(string message)
        {
            Plugin.Info(message);
        }

        public override void LogDebug(string message)
        {
            if (PluginConfig.DebugMode)
            {
                Plugin.Error($"[DEBUG] {message}");    
            }
        }

        /**
             * Sends a web request to the Tebex API. This is just a wrapper around webrequest.Enqueue, but passes through
             * multiple callbacks that can be used to interact with each API function based on the response received.
             */
        public override void MakeWebRequest(string endpoint, string body, TebexApi.HttpVerb verb,
            TebexApi.ApiSuccessCallback onSuccess, TebexApi.ApiErrorCallback onApiError,
            TebexApi.ServerErrorCallback onServerError)
        {
            // Use Oxide request method for the webrequests call. We use HttpVerb in the api so as not to depend on
            // Oxide.
            RequestMethod method;
            Enum.TryParse<RequestMethod>(verb.ToString(), true, out method);
            if (method == null)
            {
                LogError($"Unknown HTTP method!: {verb.ToString()}");
                LogError($"Failed to interpret HTTP method on request {verb} {endpoint}| {body}");
            }

            var headers = new Dictionary<string, string>
            {
                { "X-Tebex-Secret", PluginConfig.SecretKey },
                { "Content-Type", "application/json" }
            };

            var url = endpoint;
            var logOutStr = $"-> {method.ToString()} {url} | {body}";
            LogDebug(logOutStr);

            Plugin.WebRequests().Enqueue(url, body, (code, response) =>
            {
                var logInStr = $"{code} <- {method.ToString()} {url}";
                LogDebug(logInStr);

                if (code == 200 || code == 201 || code == 202 || code == 204)
                {
                    onSuccess?.Invoke(code, response);
                }
                else if (code == 403) // Admins get a secret key warning on any command that's rejected
                {
                    LogInfo("Your server's secret key is either not set or incorrect.");
                    LogInfo("tebex.secret <key>\" to set your secret key to the one associated with your webstore.");
                    LogInfo("Set up your store and get your secret key at https://tebex.io/");
                }
                else if (code == 500)
                {
                    ReportAutoTriageEvent(TebexTriage.CreateAutoTriageEvent("Internal server error from Plugin API",
                        new Dictionary<string, string>
                        {
                            { "request", logInStr },
                            { "response", logOutStr },
                        }));
                    LogError(
                        "Internal Server Error from Tebex API. Please try again later. Error details follow below.");
                    LogError(response);
                    onServerError?.Invoke(code, response);
                }
                else if (code == 0)
                {
                    ReportAutoTriageEvent(TebexTriage.CreateAutoTriageEvent("Request timeout to Plugin API",
                        new Dictionary<string, string>
                        {
                            { "request", logInStr },
                            { "response", logOutStr },
                        }));
                    LogError("Request Timeout from Tebex API. Please try again later.");
                }
                else // This should be a general failure error message with a JSON-formatted response from the API.
                {
                    try
                    {
                        var error = JsonConvert.DeserializeObject<TebexApi.TebexError>(response);
                        if (error != null)
                        {
                            ReportAutoTriageEvent(TebexTriage.CreateAutoTriageEvent(
                                "Plugin API reported general failure", new Dictionary<string, string>
                                {
                                    { "request", logInStr },
                                    { "response", logOutStr },
                                }));
                            onApiError?.Invoke(error);
                        }
                        else
                        {
                            ReportAutoTriageEvent(TebexTriage.CreateAutoTriageEvent(
                                "Plugin API error could not be interpreted!", new Dictionary<string, string>
                                {
                                    { "request", logInStr },
                                    { "response", logOutStr },
                                }));
                            LogError($"Failed to unmarshal an expected error response from API.");
                            onServerError?.Invoke(code, response);
                        }

                        LogDebug($"Request to {url} failed with code {code}.");
                        LogDebug(response);
                    }
                    catch (Exception e) // Something really unexpected with our response and it's likely not JSON
                    {
                        ReportAutoTriageEvent(TebexTriage.CreateAutoTriageEvent(
                            "Did not handle error response from API", new Dictionary<string, string>
                            {
                                { "request", logInStr },
                                { "response", logOutStr },
                            }));
                        LogError("Could not gracefully handle error response.");
                        LogError($"Response from remote {response}");
                        LogError(e.ToString());
                        onServerError?.Invoke(code, $"{e.Message}: {response}");
                    }
                }
            }, Plugin, method, headers, 10.0f);
        }

        public override void ReplyPlayer(object player, string message)
        {
            var playerInstance = player as IPlayer;
            if (playerInstance != null)
            {
                playerInstance.Reply("{0}", "", message);
            }
        }

        public override void ExecuteOfflineCommand(TebexApi.Command command, string commandName, string[] args)
        {
            if (command.Conditions.Delay > 0)
            {
                // Command requires a delay, use built-in plugin timer to wait until callback
                // in order to respect game threads
                Plugin.PluginTimers().Once(command.Conditions.Delay,
                    () =>
                    {
                        Plugin.Server().Command(commandName, args);
                        ExecutedCommands.Add(command);
                    });
            }
            else // No delay, execute immediately
            {
                Plugin.Server().Command(commandName, args);
                ExecutedCommands.Add(command);
            }
        }

        public override bool IsPlayerOnline(string playerRefId)
        {
            IPlayer iPlayer = GetPlayerRef(playerRefId) as IPlayer;
            if (iPlayer == null) //Ensure the lookup worked
            {
                LogError($"Attempted to look up online player, but no reference found: {playerRefId}");
                return false;
            }

            return iPlayer.IsConnected;
        }

        public override object GetPlayerRef(string playerId)
        {
            return Plugin.PlayerManager().FindPlayer(playerId);
        }

        public override void ExecuteOnlineCommand(TebexApi.Command command, object playerObj, string commandName,
            string[] args)
        {
            // Cast down to the base player in order to get inventory slots available.
            try
            {
                Player player = playerObj as Player;

                var slotsAvailable = player.Inventory(player.FindById(command.Player.Id)).containerMain.availableSlots
                    .Count;

                // Some commands have slot requirements, don't execute those if the player can't accept it
                if (slotsAvailable < command.Conditions.Slots)
                {
                    LogInfo(
                        $"> Player has command {command.CommandToRun} but not enough main inventory slots. Need {command.Conditions.Slots} empty slots.");
                    return;
                }
            }
            catch (Exception e)
            {
                ReportAutoTriageEvent(TebexTriage.CreateAutoTriageEvent("Caused exception while checking command conditions", new Dictionary<string, string>()
                {
                    {"command", command.CommandToRun},
                    {"exception", e.Message},
                    {"trace", e.StackTrace},
                }));
                LogError("Failed to run check for command conditions.");
                LogError(e.ToString());
            }

            // Pass through to offline command as it also verifies timing conditions
            LogInfo($"> Executing command {commandName}");
            ExecuteOfflineCommand(command, commandName, args);
        }

        public override string ExpandOfflineVariables(string input, TebexApi.PlayerInfo info)
        {
            string parsed = input;
            parsed.Replace("{id}", info.Id);
            parsed.Replace("{username}", info.Username);
            parsed.Replace("{name}", info.Username);

            if (parsed.Contains("{") || parsed.Contains("}"))
            {
                LogDebug($"Detected lingering curly braces after expanding offline variables!");
                LogDebug($"Input: {input}");
                LogDebug($"Parsed: {parsed}");
            }

            return parsed;
        }

        public override string ExpandUsernameVariables(string input, object playerObj)
        {
            IPlayer iPlayer = playerObj as IPlayer;
            if (iPlayer == null)
            {
                LogError($"Could not cast player instance when expanding username variables: {playerObj}");
                return input;
            }

            if (string.IsNullOrEmpty(iPlayer.Id) || string.IsNullOrEmpty(iPlayer.Name))
            {
                LogError($"Player ID or name is null while expanding username?!: {iPlayer}");
                LogError($"Base player object: {playerObj}");
                LogError($"Input command: {input}");
                return input;
            }

            string parsed = input;
            parsed.Replace("{id}", iPlayer.Id);
            parsed.Replace("{username}", iPlayer.Name);
            parsed.Replace("{name}", iPlayer.Name);

            if (parsed.Contains("{") || parsed.Contains("}"))
            {
                LogDebug($"Detected lingering curly braces after expanding username variables!");
                LogDebug($"Input: {input}");
                LogDebug($"Parsed: {parsed}");
            }

            return parsed;
        }

        public List<string> ReadLastLinesFromFile(string filePath, int nLines)
        {
            List<string> result = new List<string>(nLines);
            Queue<string> lineQueue = new Queue<string>(nLines);

            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (lineQueue.Count == nLines)
                        {
                            lineQueue.Dequeue();
                        }

                        lineQueue.Enqueue(line);
                    }
                }

                result.AddRange(lineQueue);
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine($"File not found: {e.Message}");
            }
            catch (IOException e)
            {
                Console.WriteLine($"IO error occurred: {e.Message}");
            }

            return result;
        }

        public override TebexTriage.AutoTriageEvent FillAutoTriageParameters(TebexTriage.AutoTriageEvent partialEvent)
        {
            partialEvent.GameId = $"{Plugin.GetGame()} {Plugin.Server().Version} | {Plugin.Server().Protocol}";
            partialEvent.FrameworkId = "Oxide";
            partialEvent.PluginVersion = Oxide.Plugins.Tebex.GetPluginVersion();
            partialEvent.ServerIp = Plugin.Server().Address.ToString();
            
            return partialEvent;
        }
    }
}