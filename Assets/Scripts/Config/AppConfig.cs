using System;
using UnityEngine;

namespace Morphis.Config
{
    [Serializable]
    public class AppConfigData
    {
        public string GameServerAddress;
        public int GameServerPort;
        public string ApiBaseUrl;
        public string DefaultWorldId;
    }

    public static class AppConfig
    {
        public static AppConfigData Instance { get; internal set; }
    }
}

