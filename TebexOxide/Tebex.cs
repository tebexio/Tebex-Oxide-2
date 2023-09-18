using Newtonsoft.Json;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;
using Oxide.Plugins;
using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust;
using Tebex.Adapters;
using Tebex.API;
using Tebex.Triage;

namespace Oxide.Plugins
{
    [Info("Tebex", "Tebex", "2.0.0b")]
    [Description("Official support for the Tebex server monetization platform")]
    public class Tebex : CovalencePlugin
    {
        private static TebexOxideAdapter _adapter;

        public static string GetPluginVersion()
        {
            return "2.0.2b";
        }
        
        private void Init()
        {
            // Setup our API and adapter
            _adapter = new TebexOxideAdapter(this);
            TebexApi.Instance.InitAdapter(_adapter);
            
            BaseTebexAdapter.PluginConfig = Config.ReadObject<BaseTebexAdapter.TebexConfig>();
            if (!Config.Exists())
            {
                //Creates new config file
                LoadConfig();
            }

            // Register permissions
            permission.RegisterPermission("tebex.secret", this);
            permission.RegisterPermission("tebex.sendlink", this);
            permission.RegisterPermission("tebex.forcecheck", this);
            permission.RegisterPermission("tebex.refresh", this);
            permission.RegisterPermission("tebex.report", this);
            permission.RegisterPermission("tebex.ban", this);
            permission.RegisterPermission("tebex.lookup", this);
            
            // Register user permissions
            permission.RegisterPermission("tebex.info", this);
            permission.RegisterPermission("tebex.categories", this);
            permission.RegisterPermission("tebex.packages", this);
            permission.RegisterPermission("tebex.checkout", this);
            permission.RegisterPermission("tebex.stats", this);
            
            // Check if auto reporting is disabled and show a warning if so.
            if (!BaseTebexAdapter.PluginConfig.AutoReportingEnabled)
            {
                _adapter.LogInfo("Auto reporting issues to Tebex is disabled.");
                _adapter.LogInfo("To enable, please set 'AutoReportingEnabled' to 'true' in config/Tebex.json");
            }
            
            // Check if secret key has been set. If so, get store information and place in cache
            if (BaseTebexAdapter.PluginConfig.SecretKey != "your-secret-key-here")
            {
                // No-op, just to place info in the cache for any future triage events
                _adapter.FetchStoreInfo((info => { }));
                return;    
            }
            
            _adapter.LogInfo("Tebex detected a new configuration file.");
            _adapter.LogInfo("Use tebex:secret <secret> to add your store's secret key.");
            _adapter.LogInfo("Alternatively, add the secret key to 'Tebex.json' and reload the plugin.");
        }
        
        public WebRequests WebRequests()
        {
            return webrequest;
        }

        public IPlayerManager PlayerManager()
        {
            return players;
        }

        public PluginTimers PluginTimers()
        {
            return timer;
        }

        public IServer Server()
        {
            return server;
        }

        public string GetGame()
        {
            return game;
        }
        
        public void Warn(string message)
        {
            LogWarning("{0}", message);
        }

        public void Error(string message)
        {
            LogError("{0}", message);
        }

        public void Info(string info)
        {
            Puts("{0}", info);
        }
        private void OnUserConnected(IPlayer player)
        {
            // Check for default config and inform the admin that configuration is waiting.
            if (player.IsAdmin && BaseTebexAdapter.PluginConfig.SecretKey == "your-secret-key-here")
            {
                player.Command("chat.add", 0, player.Id, "Tebex is not configured. Use tebex:secret <secret> from the F1 menu to add your key.");
                player.Command("chat.add", 0, player.Id, "Get your secret key by logging in at:");
                player.Command("chat.add", 0, player.Id, "https://tebex.io/");
            }

            _adapter.LogDebug($"Player login event: {player.Id}@{player.Address}");
            _adapter.OnUserConnected(player.Id, player.Address);
        }
        
        private void OnServerShutdown()
        {
            // Make sure join queue is always empties on shutdown
            _adapter.ProcessJoinQueue();
        }
        
        private void PrintCategories(IPlayer player, List<TebexApi.Category> categories)
        {
            // Index counter for selecting displayed items
            var categoryIndex = 1;
            var packIndex = 1;

            // Line separator for category response
            _adapter.ReplyPlayer(player, "---------------------------------");

            // Sort categories in order and display
            var orderedCategories = categories.OrderBy(category => category.Order).ToList();
            for (int i = 0; i < categories.Count; i++)
            {
                var listing = orderedCategories[i];
                _adapter.ReplyPlayer(player, $"[C{categoryIndex}] {listing.Name}");
                categoryIndex++;

                // Show packages for the category in order from API
                if (listing.Packages.Count > 0)
                {
                    var packages = listing.Packages.OrderBy(category => category.Order).ToList();
                    _adapter.ReplyPlayer(player, $"Packages");
                    foreach (var package in packages)
                    {
                        // Add additional flair on sales
                        if (package.Sale != null && package.Sale.Active)
                        {
                            _adapter.ReplyPlayer(player, $"-> [P{packIndex}] {package.Name} {package.Price - package.Sale.Discount} (SALE {package.Sale.Discount} off)");
                        }
                        else
                        {
                            _adapter.ReplyPlayer(player, $"-> [P{packIndex}] {package.Name} {package.Price}");
                        }

                        packIndex++;
                    }
                }
                // At the end of each category add a line separator
                _adapter.ReplyPlayer(player, "---------------------------------");
            }
        }

        private static void PrintPackages(IPlayer player, List<TebexApi.Package> packages)
        {
            // Index counter for selecting displayed items
            var packIndex = 1;

            _adapter.ReplyPlayer(player, "---------------------------------");
            _adapter.ReplyPlayer(player, "      PACKAGES AVAILABLE         ");
            _adapter.ReplyPlayer(player, "---------------------------------");

            // Sort categories in order and display
            var orderedPackages = packages.OrderBy(package => package.Order).ToList();
            for (var i = 0; i < packages.Count; i++)
            {
                var package = orderedPackages[i];
                // Add additional flair on sales
                _adapter.ReplyPlayer(player, $"[P{packIndex}] {package.Name}");
                _adapter.ReplyPlayer(player, $"Category: {package.Category.Name}");
                _adapter.ReplyPlayer(player, $"Description: {package.Description}");

                if (package.Sale != null && package.Sale.Active)
                {
                    _adapter.ReplyPlayer(player, $"Original Price: {package.Price} {package.GetFriendlyPayFrequency()}  SALE: {package.Sale.Discount} OFF!");
                }
                else
                {
                    _adapter.ReplyPlayer(player, $"Price: {package.Price} {package.GetFriendlyPayFrequency()}");
                }

                _adapter.ReplyPlayer(player, $"Purchase with 'tebex.checkout P{packIndex}' or 'tebex.checkout {package.Id}'");
                _adapter.ReplyPlayer(player, "--------------------------------");

                packIndex++;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true, "oxide/config/Tebex.json");
        }

        private BaseTebexAdapter.TebexConfig GetDefaultConfig()
        {
            return new BaseTebexAdapter.TebexConfig();
        }
        
        [Command("tebex.secret", "tebex:secret")]
        private void TebexSecretCommand(IPlayer player, string command, string[] args)
        {
            // Secret can only be ran as the admin
            if (!player.HasPermission("tebex.secret"))
            {
                _adapter.ReplyPlayer(player, "You do not have permission to run this command.");
                _adapter.ReplyPlayer(player, "If you are an admin, grant permission to use `tebex.secret`");
                return;
            }
            
            if (args.Length != 1)
            {
                _adapter.ReplyPlayer(player, "Invalid syntax. Usage: \"tebex.secret <secret>\"");
                return;
            }
            
            _adapter.ReplyPlayer(player, "Setting your secret key...");
            BaseTebexAdapter.PluginConfig.SecretKey = args[0];
            Config.WriteObject(BaseTebexAdapter.PluginConfig);

            // Reset store info so that we don't fetch from the cache
            BaseTebexAdapter.Cache.Instance.Remove("information");

            // Any failure to set secret key is logged to console automatically
            _adapter.FetchStoreInfo(info =>
            {
                _adapter.ReplyPlayer(player, $"Successfully set your secret key.");
                _adapter.ReplyPlayer(player, $"Store set as: {info.ServerInfo.Name} for the web store {info.AccountInfo.Name}");
            });
        }

        [Command("tebex.info", "tebex:info", "tebex.information", "tebex:information")]
        private void TebexInfoCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(command))
            {
                _adapter.ReplyPlayer(player, "You do not have permission to run that command.");
                return;
            }
            
            _adapter.ReplyPlayer(player, "Getting store information...");
            _adapter.FetchStoreInfo(info =>
            {
                _adapter.ReplyPlayer(player, "Information for this server:");
                _adapter.ReplyPlayer(player, $"{info.ServerInfo.Name} for webstore {info.AccountInfo.Name}");
                _adapter.ReplyPlayer(player, $"Server prices are in {info.AccountInfo.Currency.Iso4217}");
                _adapter.ReplyPlayer(player, $"Webstore domain {info.AccountInfo.Domain}");
            });
        }

        [Command("tebex.checkout", "tebex:checkout")]
        private void TebexCheckoutCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(command))
            {
                _adapter.ReplyPlayer(player, "You do not have permission to run that command.");
                return;
            }
            
            if (player.IsServer)
            {
                _adapter.ReplyPlayer(player, $"{command} cannot be executed via server. Use tebex:sendlink <username> <packageId> to specify a target player.");
                return;
            }

            // Only argument will be the package ID of the item in question
            if (args.Length != 1)
            {
                _adapter.ReplyPlayer(player, "Invalid syntax: Usage \"tebex.checkout <packageId>\"");
                return;
            }

            // Lookup the package by provided input and respond with the checkout URL
            var package = _adapter.GetPackageByShortCodeOrId(args[0].Trim());
            if (package == null)
            {
                _adapter.ReplyPlayer(player, "A package with that ID was not found.");
                return;
            }

            _adapter.ReplyPlayer(player, "Creating your checkout URL...");
            _adapter.CreateCheckoutUrl(player.Name, package, checkoutUrl =>
            {
                player.Command("chat.add", 0, player.Id, "Please visit the following URL to complete your purchase:");
                player.Command("chat.add", 0, player.Id, $"{checkoutUrl.Url}");
            }, error =>
            {
                _adapter.ReplyPlayer(player, $"{error.ErrorMessage}");
            });
        }

        [Command("tebex.help", "tebex:help")]
        private void TebexHelpCommand(IPlayer player, string command, string[] args)
        {
            _adapter.ReplyPlayer(player, "Tebex Commands Available:");
            if (player.IsAdmin) //Always show help to admins regardless of perms, for new server owners
            {
                _adapter.ReplyPlayer(player, "-- Administrator Commands --");
                _adapter.ReplyPlayer(player, "tebex.secret <secretKey>          - Sets your server's secret key.");
                _adapter.ReplyPlayer(player, "tebex.sendlink <player> <packId>  - Sends a purchase link to the provided player.");
                _adapter.ReplyPlayer(player, "tebex.forcecheck                  - Forces the command queue to check for any pending purchases.");
                _adapter.ReplyPlayer(player, "tebex.refresh                     - Refreshes store information, packages, categories, etc.");
                _adapter.ReplyPlayer(player, "tebex.report                      - Generates a report for the Tebex support team.");
                _adapter.ReplyPlayer(player, "tebex.ban <playerId>              - Bans a player from using your Tebex store.");
                _adapter.ReplyPlayer(player, "tebex.lookup <playerId>           - Looks up store statistics for the given player.");
            }

            _adapter.ReplyPlayer(player, "-- User Commands --");
            _adapter.ReplyPlayer(player, "tebex.info                       - Get information about this server's store.");
            _adapter.ReplyPlayer(player, "tebex.categories                 - Shows all item categories available on the store.");
            _adapter.ReplyPlayer(player, "tebex.packages <opt:categoryId>  - Shows all item packages available in the store or provided category.");
            _adapter.ReplyPlayer(player, "tebex.checkout <packId>          - Creates a checkout link for an item. Visit to purchase.");
            _adapter.ReplyPlayer(player, "tebex.stats                      - Gets your stats from the store, purchases, subscriptions, etc.");
        }

        [Command("tebex.forcecheck", "tebex:forcecheck")]
        private void TebexForceCheckCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(command))
            {
                _adapter.ReplyPlayer(player, "You do not have permission to run that command.");
                return;
            }

            _adapter.RefreshStoreInformation(true);
            _adapter.ProcessCommandQueue(true);
            _adapter.ProcessJoinQueue(true);
            _adapter.DeleteExecutedCommands(true);
        }

        [Command("tebex.refresh", "tebex:refresh")]
        private void TebexRefreshCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(command))
            {
                _adapter.ReplyPlayer(player, "You do not have permission to run that command.");
                return;
            }
            
            _adapter.ReplyPlayer(player, "Refreshing listings...");
            BaseTebexAdapter.Cache.Instance.Remove("packages");
            BaseTebexAdapter.Cache.Instance.Remove("categories");
            
            _adapter.RefreshListings((code, body) =>
            {
                if (BaseTebexAdapter.Cache.Instance.HasValid("packages") && BaseTebexAdapter.Cache.Instance.HasValid("categories"))
                {
                    var packs = (List<TebexApi.Package>)BaseTebexAdapter.Cache.Instance.Get("packages").Value;
                    var categories = (List<TebexApi.Category>)BaseTebexAdapter.Cache.Instance.Get("categories").Value;
                    _adapter.ReplyPlayer(player, $"Fetched {packs.Count} packages out of {categories.Count} categories");
                }
            });
        }

        [Command("tebex.report", "tebex:report")]
        private void TebexReportCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(command))
            {
                _adapter.ReplyPlayer(player, "You do not have permission to run that command.");
                return;
            }
            
            if (args.Length == 0) // require /confirm to send
            {
                _adapter.ReplyPlayer(player, "Please run `tebex.report confirm 'Your description here'` to submit your report. The following information will be sent to Tebex: ");
                _adapter.ReplyPlayer(player, "- Your game version, store id, and server IP.");
                _adapter.ReplyPlayer(player, "- Your username and IP address.");
                _adapter.ReplyPlayer(player, "- Please include a short description of the issue you were facing.");
            }

            if (args.Length == 2 && args[0] == "confirm")
            {
                _adapter.ReplyPlayer(player, "Sending your report to Tebex...");
                
                var triageEvent = new TebexTriage.ReportedTriageEvent();
                triageEvent.GameId = $"{game} {server.Version}|{server.Protocol}";
                triageEvent.FrameworkId = "Oxide";
                triageEvent.PluginVersion = GetPluginVersion();
                triageEvent.ServerIp = server.Address.ToString();
                triageEvent.ErrorMessage = "Player Report: " + args[1];
                triageEvent.Trace = "";
                triageEvent.Metadata = new Dictionary<string, string>()
                {
                
                };
                triageEvent.Username = player.Name + "/" + player.Id;
                triageEvent.UserIp = player.Address;
                
                _adapter.ReportManualTriageEvent(triageEvent, (code, body) =>
                {
                    _adapter.ReplyPlayer(player, "Your report has been sent. Thank you!");
                }, (code, body) =>
                {
                    _adapter.ReplyPlayer(player, "An error occurred while submitting your report. Please contact our support team directly.");
                    _adapter.ReplyPlayer(player, "Error: " + body);
                });
                
                return;
            }
            
            _adapter.ReplyPlayer(player, $"Usage: tebex.report <confirm> '<message>'");
        }

        [Command("tebex.ban", "tebex:ban")]
        private void TebexBanCommand(IPlayer commandRunner, string command, string[] args)
        {
            if (!commandRunner.HasPermission("tebex.ban"))
            {
                _adapter.ReplyPlayer(commandRunner, $"{command} can only be used by administrators.");
                return;
            }

            if (args.Length < 2)
            {
                _adapter.ReplyPlayer(commandRunner, $"Usage: tebex.ban <playerName> <reason>");
                return;
            }

            var player = players.FindPlayer(args[0].Trim());
            if (player == null)
            {
                _adapter.ReplyPlayer(commandRunner, $"Could not find that player on the server.");
                return;
            }

            var reason = string.Join(" ", args.Skip(1));
            _adapter.ReplyPlayer(commandRunner, $"Processing ban for player {player.Name} with reason '{reason}'");
            _adapter.BanPlayer(player.Name, player.Address, reason, (code, body) =>
            {
                _adapter.ReplyPlayer(commandRunner, "Player banned successfully.");
            }, error =>
            {
                _adapter.ReplyPlayer(commandRunner, $"Could not ban player. {error.ErrorMessage}");
            });
        }

        [Command("tebex.unban", "tebex:unban")]
        private void TebexUnbanCommand(IPlayer commandRunner, string command, string[] args)
        {
            if (!commandRunner.IsAdmin)
            {
                _adapter.ReplyPlayer(commandRunner, $"{command} can only be used by administrators.");
                return;
            }

            _adapter.ReplyPlayer(commandRunner, $"You must unban players via your webstore.");
        }

        [Command("tebex.categories", "tebex:categories", "tebex.listings", "tebex:listings")]
        private void TebexCategoriesCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(command))
            {
                _adapter.ReplyPlayer(player, "You do not have permission to run that command.");
                return;
            }
            
            _adapter.GetCategories(categories =>
            {
                PrintCategories(player, categories);
            });
        }

        [Command("tebex.packages", "tebex:packages")]
        private void TebexPackagesCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(command))
            {
                _adapter.ReplyPlayer(player, "You do not have permission to run that command.");
                return;
            }
            
            _adapter.GetPackages(packages =>
            {
                PrintPackages(player, packages);
            });
        }

        [Command("tebex.lookup", "tebex:lookup")]
        private void TebexLookupCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(command))
            {
                _adapter.ReplyPlayer(player, "You do not have permission to run that command.");
                return;
            }
            
            if (args.Length != 1)
            {
                _adapter.ReplyPlayer(player, $"Usage: tebex.lookup <playerId/playerUsername>");
                return;
            }

            // Try to find the given player
            var target = players.FindPlayer(args[0]);
            if (target == null)
            {
                _adapter.ReplyPlayer(player, $"Could not find a player matching the name or id {args[0]}.");
                return;
            }

            _adapter.GetUser(target.Id, (code, body) =>
            {
                var response = JsonConvert.DeserializeObject<TebexApi.UserInfoResponse>(body);
                _adapter.ReplyPlayer(player, $"Username: {response.Player.Username}");
                _adapter.ReplyPlayer(player, $"Id: {response.Player.Id}");
                _adapter.ReplyPlayer(player, $"Payments Total: ${response.Payments.Sum(payment => payment.Price)}");
                _adapter.ReplyPlayer(player, $"Chargeback Rate: {response.ChargebackRate}%");
                _adapter.ReplyPlayer(player, $"Bans Total: {response.BanCount}");
                _adapter.ReplyPlayer(player, $"Payments: {response.Payments.Count}");
            }, error =>
            {
                _adapter.ReplyPlayer(player, error.ErrorMessage);
            });
        }

        [Command("tebex.sendlink", "tebex:sendlink")]
        private void TebexSendLinkCommand(IPlayer commandRunner, string command, string[] args)
        {
            if (!commandRunner.HasPermission("tebex.sendlink"))
            {
                _adapter.ReplyPlayer(commandRunner, "You must be an administrator to run this command.");
                return;
            }
            
            if (args.Length != 2)
            {
                _adapter.ReplyPlayer(commandRunner, "Usage: tebex.sendlink <username> <packageId>");
                return;
            }

            var username = args[0].Trim();
            var package = _adapter.GetPackageByShortCodeOrId(args[1].Trim());
            if (package == null)
            {
                _adapter.ReplyPlayer(commandRunner, "A package with that ID was not found.");
                return;
            }

            _adapter.ReplyPlayer(commandRunner, $"Creating checkout URL with package '{package.Name}'|{package.Id} for player {username}");
            var player = players.FindPlayer(username);
            if (player == null)
            {
                _adapter.ReplyPlayer(commandRunner, $"Couldn't find that player on the server.");
                return;
            }

            _adapter.CreateCheckoutUrl(player.Name, package, checkoutUrl =>
            {
                player.Command("chat.add", 0, player.Id, "Please visit the following URL to complete your purchase:");
                player.Command("chat.add", 0, player.Id, $"{checkoutUrl.Url}");
            }, error =>
            {
                _adapter.ReplyPlayer(player, $"{error.ErrorMessage}");
            });
        }
	}
}