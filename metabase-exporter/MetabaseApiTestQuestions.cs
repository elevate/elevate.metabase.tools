using System;
using System.Linq;
using System.Threading.Tasks;

namespace metabase_exporter
{
    public static class MetabaseApiTestQuestions
    {
        public static async Task TestQuestions(this MetabaseApi api)
        {
            var cards = await api.GetAllCards();

            var testResults = await cards.Traverse(api.TestCard);

            Console.WriteLine();
            Console.WriteLine("Passed: " + testResults.Count(x => x == TestResult.Passed));
            Console.WriteLine("Failed: " + testResults.Count(x => x == TestResult.Failed));
            Console.WriteLine("Crashed: " + testResults.Count(x => x == TestResult.Crashed));
        }

        enum TestResult
        {
            Passed,
            Failed,
            Crashed,
        }
        
        static async Task<TestResult> TestCard(this MetabaseApi api, Card card)
        {
            try
            {
                var result = await api.RunCard(card.Id);
                Console.Write($"'{card.Name}'...");
                if (result.Status == "completed")
                {
                    Console.WriteLine("Passed");
                    return TestResult.Passed;
                }
                else
                {
                    Console.WriteLine($"Failed:\n{result.Error}\n");
                    return TestResult.Failed;
                }
            }
            catch (MetabaseApiException e)
            {
                Console.WriteLine($"ERROR querying '{card.Name}': {e.Message}");
                return TestResult.Crashed;
            }
        }
    }
}