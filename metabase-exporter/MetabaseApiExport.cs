using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace metabase_exporter
{
    public static class MetabaseApiExport
    {
        public static async Task<MetabaseState> Export(this MetabaseApi api)
        {
            var cards = await api.GetAllCards();
            var nonArchivedCards = cards.Where(x => x.Archived == false).ToArray();

            var dashboards = await api.GetAllDashboards();
            var nonArchivedDashboards = dashboards.Where(x => x.Archived == false).ToArray();

            var collections = await api.GetAllCollections();
            var nonArchivedCollections = collections.Where(x => x.Archived == false).ToArray();

            var state = new MetabaseState
            {
                Cards = nonArchivedCards,
                Dashboards = nonArchivedDashboards,
                Collections = nonArchivedCollections,
            };

            return state;
        }
    }
}
