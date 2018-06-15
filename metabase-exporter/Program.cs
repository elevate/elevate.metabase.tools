using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace metabase_exporter
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var api = await InitApi();
                       
            //var state = await api.Export();
            //File.WriteAllText("metabase-state.json", state);
                        
            var rawState = File.ReadAllText("metabase-state.json");
            var state = JsonConvert.DeserializeObject<MetabaseState>(rawState);
            await api.Import(state, new Dictionary<int, int> { { 2, 2} });
            
        }

        static async Task<MetabaseApi> InitApi()
        {
            const string filename = "metabase-token.txt";
            string GetInitialToken()
            {
                try
                {
                    return File.ReadAllText(filename);
                }
                catch (Exception e)
                {
                    return null;
                }
            }

            var settings = new MetabaseApiSettings
            {
                MetabaseApiUrl = "https://metabase-local.elevatedirect.com:32443",
                MetabaseApiUsername = "mauricio@elevatedirect.com",
                MetabaseApiPassword = "123456789",

                // get an existing token if available to work around Metabase throttling
                // https://github.com/metabase/metabase/issues/4979
                MetabaseInitialToken = GetInitialToken(),
            };
            var metabaseSession = new MetabaseSessionTokenManager(settings);
            var token = await metabaseSession.CurrentToken();
            File.WriteAllText(filename, token);

            var api = new MetabaseApi(metabaseSession);
            return api;
        }
    }
}
