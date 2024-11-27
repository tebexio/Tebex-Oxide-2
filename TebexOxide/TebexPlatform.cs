

namespace Tebex.Triage
{
    public class TebexPlatform
    {
        private String _gameId;
        private String _pluginVersion;
        private TebexTelemetry _telemetry;
        
        public TebexPlatform(String gameId, String pluginVersion, TebexTelemetry _telemetry)
        {
            this._pluginVersion = pluginVersion;
            this._telemetry = _telemetry;
            this._gameId = gameId;
        }
    
        public TebexTelemetry GetTelemetry()
        {
            return _telemetry;
        }

        public string GetPluginVersion()
        {
            return _pluginVersion;
        }

        public string GetGameId()
        {
            return _gameId;
        }
    }    
}
